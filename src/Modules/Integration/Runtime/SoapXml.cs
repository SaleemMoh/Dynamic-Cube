using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using DynamicCube.Modules.Integration.Domain;

namespace DynamicCube.Modules.Integration.Runtime;

/// <summary>A SOAP fault: its code (the most specific, without prefix), its text, and whether the provider says it failed itself.</summary>
internal sealed record SoapFault(string Code, string? Message, bool IsServer);

/// <summary>A SOAP response read as JSON: the content of the body's element, or the fault's.</summary>
internal sealed record SoapResponse(JsonNode? Data, SoapFault? Fault);

/// <summary>
/// SOAP 1.1 and 1.2 (ADR-0044): the envelope built from an operation's inputs, the response read as JSON so outputs,
/// response envelopes and code maps work as for REST, faults, and the WS-Security UsernameToken header.
/// </summary>
/// <remarks>
/// Reading XML as JSON: an element with text only is a string, an empty or <c>xsi:nil</c> one is null; an element with
/// children or attributes is an object whose properties are the children's and attributes' local names (namespaces
/// dropped), repeated children an array, text beside attributes <c>$text</c>.
/// </remarks>
internal static class SoapXml
{
    public const string Soap11 = "http://schemas.xmlsoap.org/soap/envelope/";
    public const string Soap12 = "http://www.w3.org/2003/05/soap-envelope";
    public const string Wsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
    public const string Wsu = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
    public const string PasswordTextType = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText";
    public const string PasswordDigestType = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordDigest";
    private const string Base64Binary = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary";
    private const string Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    /// <summary>The empty header of an envelope built before the credentials are added; no input value can produce it.</summary>
    private const string HeaderMarker = "<!--dc:soap-header-->";
    private const string EmptyHeader = "<soapenv:Header>" + HeaderMarker + "</soapenv:Header>";

    private const int MaxCharacters = 20_000_000;

    public static string EnvelopeNamespace(string version) => version == "1.2" ? Soap12 : Soap11;

    /// <summary>The request's content type: <c>text/xml</c> (1.1) or <c>application/soap+xml</c> with the action (1.2).</summary>
    public static string ContentType(SoapSettings soap) => soap.Version == "1.2"
        ? "application/soap+xml; charset=utf-8" + (string.IsNullOrEmpty(soap.Action) ? string.Empty : $"; action=\"{soap.Action}\"")
        : "text/xml; charset=utf-8";

    /// <summary>
    /// The envelope: the operation element in its namespace, the body inputs as its child elements (a dot path nests
    /// them; the same parent is used again), in the order given. The header is left for <see cref="WithHeader"/>.
    /// </summary>
    public static string Envelope(SoapSettings soap, IEnumerable<(string Target, string Value)> values)
    {
        ArgumentNullException.ThrowIfNull(soap);
        ArgumentNullException.ThrowIfNull(values);
        XNamespace env = EnvelopeNamespace(soap.Version);
        XNamespace ns = string.IsNullOrWhiteSpace(soap.Namespace) ? XNamespace.None : soap.Namespace.Trim();
        var child = soap.QualifiedElements ? ns : XNamespace.None;
        var operation = new XElement(ns + soap.OperationName);
        if (ns != XNamespace.None)
        {
            operation.Add(new XAttribute(XNamespace.Xmlns + "ns", ns.NamespaceName));
        }

        foreach (var (target, value) in values)
        {
            var parts = target.Split('.');
            var parent = operation;
            foreach (var part in parts[..^1])
            {
                var existing = parent.Elements(child + part).LastOrDefault();
                if (existing is null)
                {
                    existing = new XElement(child + part);
                    parent.Add(existing);
                }

                parent = existing;
            }

            parent.Add(new XElement(child + parts[^1], value));
        }

        var envelope = new XElement(
            env + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soapenv", env.NamespaceName),
            new XElement(env + "Header", new XComment("dc:soap-header")),
            new XElement(env + "Body", operation));
        return envelope.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>The envelope with a header block (e.g. WS-Security), or without a header when there is none.</summary>
    public static string WithHeader(string envelope, string? header)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return string.IsNullOrEmpty(header) ? envelope.Replace(EmptyHeader, string.Empty, StringComparison.Ordinal) : envelope.Replace(HeaderMarker, header, StringComparison.Ordinal);
    }

    /// <summary>A WS-Security header with a UsernameToken (OASIS username token profile 1.0), with a fresh nonce and time.</summary>
    public static string SecurityHeader(string version, string username, string password, bool digest, DateTimeOffset now)
    {
        XNamespace env = EnvelopeNamespace(version);
        XNamespace wsse = Wsse;
        XNamespace wsu = Wsu;
        var nonce = RandomNumberGenerator.GetBytes(16);
        var created = now.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        var security = new XElement(
            wsse + "Security",
            new XAttribute(XNamespace.Xmlns + "wsse", Wsse),
            new XAttribute(XNamespace.Xmlns + "wsu", Wsu),
            new XAttribute(env + "mustUnderstand", version == "1.2" ? "true" : "1"),
            new XElement(
                wsse + "UsernameToken",
                new XAttribute(wsu + "Id", "UsernameToken-" + Guid.NewGuid().ToString("N")),
                new XElement(wsse + "Username", username),
                new XElement(wsse + "Password", new XAttribute("Type", digest ? PasswordDigestType : PasswordTextType), digest ? Digest(nonce, created, password) : password),
                new XElement(wsse + "Nonce", new XAttribute("EncodingType", Base64Binary), Convert.ToBase64String(nonce)),
                new XElement(wsu + "Created", created)));
        return security.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>The profile's password digest: Base64(SHA-1(nonce + created + password)).</summary>
    public static string Digest(byte[] nonce, string created, string password)
    {
        ArgumentNullException.ThrowIfNull(nonce);
        byte[] bytes = [.. nonce, .. Encoding.UTF8.GetBytes(created), .. Encoding.UTF8.GetBytes(password)];
#pragma warning disable CA5350 // The UsernameToken profile defines the digest with SHA-1; the provider checks it so.
        return Convert.ToBase64String(SHA1.HashData(bytes));
#pragma warning restore CA5350
    }

    /// <summary>The response as JSON: the content of the body's element, or the fault; null when the text is not a SOAP envelope.</summary>
    public static SoapResponse? Read(string? body)
    {
        var document = Parse(body);
        var root = document?.Root;
        if (root is null || root.Name.LocalName != "Envelope" || root.Name.NamespaceName is not (Soap11 or Soap12))
        {
            return null;
        }

        var env = root.Name.Namespace;
        var content = root.Element(env + "Body")?.Elements().FirstOrDefault();
        if (content is null)
        {
            return root.Element(env + "Body") is null ? null : new SoapResponse(null, null);
        }

        return content.Name == env + "Fault" ? new SoapResponse(ToJson(content), Fault(content, env)) : new SoapResponse(ToJson(content), null);
    }

    /// <summary>Whether the text is a SOAP envelope carrying a fault.</summary>
    public static bool IsFault(string? body) => body is not null && body.Contains("Fault", StringComparison.Ordinal) && Read(body)?.Fault is not null;

    /// <summary>An element as JSON (see the remarks of <see cref="SoapXml"/>).</summary>
    public static JsonNode? ToJson(XElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (string.Equals((string?)element.Attribute(XName.Get("nil", Xsi)), "true", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var attributes = element.Attributes().Where(a => !a.IsNamespaceDeclaration && a.Name.NamespaceName != Xsi).ToList();
        var children = element.Elements().ToList();
        if (children.Count == 0 && attributes.Count == 0)
        {
            return element.Value.Length == 0 ? null : JsonValue.Create(element.Value);
        }

        var result = new JsonObject();
        foreach (var attribute in attributes)
        {
            result[attribute.Name.LocalName] = attribute.Value;
        }

        foreach (var group in children.GroupBy(c => c.Name.LocalName, StringComparer.Ordinal))
        {
            var items = group.Select(ToJson).ToList();
            result[group.Key] = items.Count == 1 ? items[0] : new JsonArray([.. items]);
        }

        if (children.Count == 0 && element.Value.Length > 0)
        {
            result["$text"] = element.Value;
        }

        return result;
    }

    /// <summary>The text with the values of elements and attributes named in <paramref name="fields"/> replaced by <c>***</c>; null when it is not XML.</summary>
    public static string? Mask(string body, IReadOnlySet<string> fields)
    {
        var document = Parse(body);
        if (document?.Root is null)
        {
            return null;
        }

        foreach (var element in document.Root.DescendantsAndSelf())
        {
            if (fields.Contains(element.Name.LocalName) && !element.HasElements)
            {
                element.Value = Masking.Mask;
            }

            foreach (var attribute in element.Attributes().Where(a => !a.IsNamespaceDeclaration && fields.Contains(a.Name.LocalName)))
            {
                attribute.Value = Masking.Mask;
            }
        }

        return document.Root.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>Whether a name can be an XML element (no namespace prefix).</summary>
    public static bool IsElementName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        try
        {
            XmlConvert.VerifyNCName(name);
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    /// <summary>Parses XML without DTDs or external resolution; null when the text is not XML.</summary>
    public static XDocument? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !text.TrimStart().StartsWith('<'))
        {
            return null;
        }

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxCharacters,
            IgnoreComments = false,
        };
        try
        {
            using var reader = XmlReader.Create(new StringReader(text), settings);
            return XDocument.Load(reader);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static SoapFault Fault(XElement fault, XNamespace env)
    {
        if (env == Soap12)
        {
            var code = fault.Element(env + "Code");
            var top = Local(code?.Element(env + "Value")?.Value);
            var specific = top;
            for (var sub = code?.Element(env + "Subcode"); sub is not null; sub = sub.Element(env + "Subcode"))
            {
                specific = Local(sub.Element(env + "Value")?.Value) ?? specific;
            }

            var reason = fault.Element(env + "Reason")?.Elements(env + "Text").FirstOrDefault()?.Value;
            return new SoapFault(specific ?? "FAULT", Blank(reason), top is "Receiver");
        }

        // SOAP 1.1: faultcode and faultstring are unqualified; "Server.Timeout" style codes keep their dotted detail.
        var faultCode = Local(fault.Elements().FirstOrDefault(e => e.Name.LocalName == "faultcode")?.Value);
        var faultString = fault.Elements().FirstOrDefault(e => e.Name.LocalName == "faultstring")?.Value;
        return new SoapFault(faultCode ?? "FAULT", Blank(faultString), faultCode?.StartsWith("Server", StringComparison.Ordinal) == true);
    }

    private static string? Local(string? qualified)
    {
        var text = qualified?.Trim();
        return string.IsNullOrEmpty(text) ? null : text[(text.LastIndexOf(':') + 1)..];
    }

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
