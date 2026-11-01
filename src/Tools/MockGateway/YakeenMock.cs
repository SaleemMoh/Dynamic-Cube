using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;

namespace DynamicCube.MockGateway;

/// <summary>
/// A Yakeen-like SOAP service (ADR-0044) at <c>/soap/yakeen</c>: its WSDL at <c>?wsdl</c> (document/literal, SOAP 1.1 and
/// 1.2 bindings), WS-Security UsernameToken checked (password as text or digest), citizen information and addresses from
/// the NIC sample, and SOAP faults with the service's own codes. Development only.
/// </summary>
internal static class YakeenMock
{
    public const string Namespace = "http://yakeen.mock/person";
    private const string Soap11 = "http://schemas.xmlsoap.org/soap/envelope/";
    private const string Soap12 = "http://www.w3.org/2003/05/soap-envelope";
    private const string Wsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
    private const string Wsu = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";

    private static readonly XNamespace Y = Namespace;

    public static void Map(WebApplication app, JsonNode people)
    {
        var config = app.Configuration.GetSection("Yakeen");
        var username = config["Username"] ?? "dev-yakeen-user";
        var password = config["Password"] ?? "dev-yakeen-pass";

        app.MapGet("/soap/yakeen", (HttpRequest request) =>
            request.Query.ContainsKey("wsdl") || request.Query.ContainsKey("WSDL")
                ? Results.Text(Wsdl($"{request.Scheme}://{request.Host}/soap/yakeen"), "text/xml; charset=utf-8")
                : Results.Text("POST a SOAP envelope; the description is at ?wsdl.", "text/plain"));

        app.MapPost("/soap/yakeen", async (HttpRequest request) =>
        {
            using var reader = new StreamReader(request.Body, Encoding.UTF8);
            var text = await reader.ReadToEndAsync();
            XDocument document;
            try
            {
                using var xml = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
                document = XDocument.Load(xml);
            }
            catch (XmlException)
            {
                return Fault(Soap11, "Client", "soap", "The request is not well-formed XML.");
            }

            var root = document.Root;
            var env = root?.Name.NamespaceName;
            if (root?.Name.LocalName != "Envelope" || env is not (Soap11 or Soap12))
            {
                return Fault(Soap11, "VersionMismatch", "soap", "The request is not a SOAP envelope.");
            }

            if (!Authenticated(root, username, password))
            {
                return Fault(env, "FailedAuthentication", "wsse", "The security token could not be authenticated.");
            }

            var operation = root.Element(XName.Get("Body", env))?.Elements().FirstOrDefault();
            return operation?.Name.LocalName switch
            {
                "GetCitizenInfo" => CitizenInfo(env, operation, people),
                "GetCitizenAddress" => CitizenAddress(env, operation, people),
                _ => Fault(env, "UnknownOperation", "y", $"The operation '{operation?.Name.LocalName}' is not offered."),
            };
        }).DisableAntiforgery();
    }

    private static IResult CitizenInfo(string env, XElement operation, JsonNode people)
    {
        var nin = Value(operation, "request", "nin");
        var dateOfBirth = Value(operation, "request", "dateOfBirth");
        if (nin is null || nin.Length != 10 || !nin.All(char.IsAsciiDigit) || nin[0] != '1')
        {
            return Fault(env, "InvalidNin", "y", "The national ID must be 10 digits starting with 1.");
        }

        if (people["byIdNumber"]?[nin] is not JsonObject person)
        {
            return Fault(env, "PersonNotFound", "y", "No citizen with this national ID.");
        }

        // The date of birth is checked as Yakeen does: Hijri (yyyy-MM or yyyy/MM) or Gregorian (yyyy-MM-dd).
        var hijri = person["birthDateHijri"]?.GetValue<string>() ?? string.Empty;
        var gregorian = person["birthDate"]?.GetValue<string>() ?? string.Empty;
        var given = (dateOfBirth ?? string.Empty).Replace('/', '-');
        if (given.Length == 0 || !(hijri.Replace('/', '-').StartsWith(given, StringComparison.Ordinal) || gregorian.StartsWith(given, StringComparison.Ordinal)))
        {
            return Fault(env, "DateOfBirthMismatch", "y", "The date of birth does not match the national ID.");
        }

        var arabic = (person["nameAr"]?.GetValue<string>() ?? string.Empty).Replace(" بن ", " ", StringComparison.Ordinal).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var english = (person["nameEn"]?.GetValue<string>() ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return Envelope(env, new XElement(
            Y + "GetCitizenInfoResponse",
            new XElement(
                Y + "return",
                new XElement(Y + "firstName", arabic.ElementAtOrDefault(0)),
                new XElement(Y + "fatherName", arabic.Length > 2 ? arabic[1] : null),
                new XElement(Y + "familyName", arabic.LastOrDefault()),
                new XElement(Y + "englishFirstName", english.ElementAtOrDefault(0)),
                new XElement(Y + "englishLastName", english.LastOrDefault()),
                new XElement(Y + "gender", person["gender"]?["code"]?.GetValue<string>()),
                new XElement(Y + "dateOfBirthG", gregorian),
                new XElement(Y + "dateOfBirthH", hijri),
                new XElement(Y + "nationality", person["nationality"]?["name"]?.GetValue<string>()),
                new XElement(Y + "nationalityCode", person["nationality"]?["code"]?.GetValue<string>()),
                new XElement(Y + "idStatus", person["idStatus"]?["name"]?.GetValue<string>()),
                new XElement(Y + "idExpiryDate", person["idExpiryDate"]?.GetValue<string>()))));
    }

    private static IResult CitizenAddress(string env, XElement operation, JsonNode people)
    {
        var nin = Value(operation, "nin");
        if (nin is null || people["byIdNumber"]?[nin] is not JsonObject person)
        {
            return Fault(env, "PersonNotFound", "y", "No citizen with this national ID.");
        }

        var city = person["city"]?.GetValue<string>() ?? "الرياض";
        return Envelope(env, new XElement(
            Y + "GetCitizenAddressResponse",
            new XElement(
                Y + "addresses",
                new XElement(Y + "address", new XElement(Y + "city", city), new XElement(Y + "district", "حي الملقا"), new XElement(Y + "streetName", "طريق الأمير محمد بن سلمان"), new XElement(Y + "buildingNumber", "8228"), new XElement(Y + "postalCode", "13521"), new XElement(Y + "isPrimary", "true")),
                new XElement(Y + "address", new XElement(Y + "city", "جدة"), new XElement(Y + "district", "حي الروضة"), new XElement(Y + "streetName", "شارع الأمير سلطان"), new XElement(Y + "buildingNumber", "2417"), new XElement(Y + "postalCode", "23435"), new XElement(Y + "isPrimary", "false")))));
    }

    /// <summary>The UsernameToken: the user name, and the password as text or as Base64(SHA-1(nonce + created + password)).</summary>
    private static bool Authenticated(XElement envelope, string username, string password)
    {
        var token = envelope.Descendants(XName.Get("UsernameToken", Wsse)).FirstOrDefault();
        if (token is null || token.Element(XName.Get("Username", Wsse))?.Value != username)
        {
            return false;
        }

        var given = token.Element(XName.Get("Password", Wsse));
        if (given?.Attribute("Type")?.Value.EndsWith("#PasswordDigest", StringComparison.Ordinal) != true)
        {
            return given?.Value == password;
        }

        var nonce = token.Element(XName.Get("Nonce", Wsse))?.Value;
        var created = token.Element(XName.Get("Created", Wsu))?.Value;
        if (nonce is null || created is null || !DateTimeOffset.TryParse(created, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)
            || Math.Abs((DateTimeOffset.UtcNow - at).TotalMinutes) > 5)
        {
            return false;
        }

        byte[] bytes;
        try
        {
            bytes = [.. Convert.FromBase64String(nonce), .. Encoding.UTF8.GetBytes(created), .. Encoding.UTF8.GetBytes(password)];
        }
        catch (FormatException)
        {
            return false;
        }

#pragma warning disable CA5350 // The UsernameToken profile defines the digest with SHA-1.
        return given.Value == Convert.ToBase64String(SHA1.HashData(bytes));
#pragma warning restore CA5350
    }

    private static string? Value(XElement operation, params string[] path)
    {
        XElement? node = operation;
        foreach (var name in path)
        {
            node = node?.Elements().FirstOrDefault(e => e.Name.LocalName == name);
        }

        return string.IsNullOrWhiteSpace(node?.Value) ? null : node.Value.Trim();
    }

    private static IResult Envelope(string env, XElement content)
    {
        XNamespace soap = env;
        var envelope = new XElement(soap + "Envelope", new XAttribute(XNamespace.Xmlns + "soap", env), new XAttribute(XNamespace.Xmlns + "y", Namespace), new XElement(soap + "Body", content));
        return Results.Text(envelope.ToString(SaveOptions.DisableFormatting), ContentType(env));
    }

    /// <summary>A fault with HTTP 500: SOAP 1.1 faultcode/faultstring, or SOAP 1.2 Code (Sender/Receiver with the code as subcode) and Reason.</summary>
    private static IResult Fault(string env, string code, string prefix, string message)
    {
        XNamespace soap = env;
        XElement fault;
        if (env == Soap12)
        {
            var top = prefix == "soap" ? code : "Sender";
            var value = new XElement(soap + "Value", $"soap:{top}");
            var codeElement = new XElement(soap + "Code", value);
            if (prefix != "soap")
            {
                codeElement.Add(new XElement(soap + "Subcode", new XElement(soap + "Value", $"{prefix}:{code}")));
            }

            fault = new XElement(soap + "Fault", codeElement, new XElement(soap + "Reason", new XElement(soap + "Text", new XAttribute(XNamespace.Xml + "lang", "en"), message)));
        }
        else
        {
            fault = new XElement(soap + "Fault", new XElement("faultcode", $"{prefix}:{code}"), new XElement("faultstring", message));
        }

        var envelope = new XElement(
            soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soap", env),
            new XAttribute(XNamespace.Xmlns + "y", Namespace),
            new XAttribute(XNamespace.Xmlns + "wsse", Wsse),
            new XElement(soap + "Body", fault));
        return Results.Text(envelope.ToString(SaveOptions.DisableFormatting), ContentType(env), statusCode: 500);
    }

    private static string ContentType(string env) => env == Soap12 ? "application/soap+xml; charset=utf-8" : "text/xml; charset=utf-8";

    private static string Wsdl(string address) => $$"""
        <?xml version="1.0" encoding="utf-8"?>
        <wsdl:definitions name="YakeenPersonService" targetNamespace="{{Namespace}}"
            xmlns:wsdl="http://schemas.xmlsoap.org/wsdl/" xmlns:soap="http://schemas.xmlsoap.org/wsdl/soap/"
            xmlns:soap12="http://schemas.xmlsoap.org/wsdl/soap12/" xmlns:xs="http://www.w3.org/2001/XMLSchema"
            xmlns:wsp="http://www.w3.org/ns/ws-policy" xmlns:sp="http://docs.oasis-open.org/ws-sx/ws-securitypolicy/200702"
            xmlns:tns="{{Namespace}}">
          <wsdl:documentation>Yakeen (mock): citizen information and national address by national ID. Development only.</wsdl:documentation>
          <wsp:Policy wsu:Id="UsernameTokenPolicy" xmlns:wsu="{{Wsu}}">
            <sp:SupportingTokens><wsp:Policy><sp:UsernameToken sp:IncludeToken="http://docs.oasis-open.org/ws-sx/ws-securitypolicy/200702/IncludeToken/AlwaysToRecipient"/></wsp:Policy></sp:SupportingTokens>
          </wsp:Policy>
          <wsdl:types>
            <xs:schema targetNamespace="{{Namespace}}" elementFormDefault="qualified">
              <xs:complexType name="CitizenRequest">
                <xs:sequence>
                  <xs:element name="nin" type="xs:string"/>
                  <xs:element name="dateOfBirth" type="xs:string"/>
                </xs:sequence>
              </xs:complexType>
              <xs:complexType name="Citizen">
                <xs:sequence>
                  <xs:element name="firstName" type="xs:string"/>
                  <xs:element name="fatherName" type="xs:string" minOccurs="0"/>
                  <xs:element name="familyName" type="xs:string"/>
                  <xs:element name="englishFirstName" type="xs:string"/>
                  <xs:element name="englishLastName" type="xs:string"/>
                  <xs:element name="gender" type="xs:string"/>
                  <xs:element name="dateOfBirthG" type="xs:date"/>
                  <xs:element name="dateOfBirthH" type="xs:string"/>
                  <xs:element name="nationality" type="xs:string"/>
                  <xs:element name="nationalityCode" type="xs:string"/>
                  <xs:element name="idStatus" type="xs:string"/>
                  <xs:element name="idExpiryDate" type="xs:string"/>
                </xs:sequence>
              </xs:complexType>
              <xs:complexType name="Address">
                <xs:sequence>
                  <xs:element name="city" type="xs:string"/>
                  <xs:element name="district" type="xs:string"/>
                  <xs:element name="streetName" type="xs:string"/>
                  <xs:element name="buildingNumber" type="xs:string"/>
                  <xs:element name="postalCode" type="xs:string"/>
                  <xs:element name="isPrimary" type="xs:boolean"/>
                </xs:sequence>
              </xs:complexType>
              <xs:element name="GetCitizenInfo">
                <xs:complexType><xs:sequence>
                  <xs:element name="request" type="tns:CitizenRequest"/>
                  <xs:element name="chargeCode" type="xs:string" minOccurs="0"/>
                </xs:sequence></xs:complexType>
              </xs:element>
              <xs:element name="GetCitizenInfoResponse">
                <xs:complexType><xs:sequence><xs:element name="return" type="tns:Citizen"/></xs:sequence></xs:complexType>
              </xs:element>
              <xs:element name="GetCitizenAddress">
                <xs:complexType><xs:sequence><xs:element name="nin" type="xs:string"/></xs:sequence></xs:complexType>
              </xs:element>
              <xs:element name="GetCitizenAddressResponse">
                <xs:complexType><xs:sequence>
                  <xs:element name="addresses"><xs:complexType><xs:sequence>
                    <xs:element name="address" type="tns:Address" maxOccurs="unbounded"/>
                  </xs:sequence></xs:complexType></xs:element>
                </xs:sequence></xs:complexType>
              </xs:element>
            </xs:schema>
          </wsdl:types>
          <wsdl:message name="GetCitizenInfoRequest"><wsdl:part name="parameters" element="tns:GetCitizenInfo"/></wsdl:message>
          <wsdl:message name="GetCitizenInfoResponse"><wsdl:part name="parameters" element="tns:GetCitizenInfoResponse"/></wsdl:message>
          <wsdl:message name="GetCitizenAddressRequest"><wsdl:part name="parameters" element="tns:GetCitizenAddress"/></wsdl:message>
          <wsdl:message name="GetCitizenAddressResponse"><wsdl:part name="parameters" element="tns:GetCitizenAddressResponse"/></wsdl:message>
          <wsdl:portType name="YakeenPersonPort">
            <wsdl:operation name="GetCitizenInfo">
              <wsdl:documentation>Citizen information by national ID and date of birth.</wsdl:documentation>
              <wsdl:input message="tns:GetCitizenInfoRequest"/><wsdl:output message="tns:GetCitizenInfoResponse"/>
            </wsdl:operation>
            <wsdl:operation name="GetCitizenAddress">
              <wsdl:documentation>National addresses of a citizen.</wsdl:documentation>
              <wsdl:input message="tns:GetCitizenAddressRequest"/><wsdl:output message="tns:GetCitizenAddressResponse"/>
            </wsdl:operation>
          </wsdl:portType>
          <wsdl:binding name="YakeenPersonSoap" type="tns:YakeenPersonPort">
            <soap:binding style="document" transport="http://schemas.xmlsoap.org/soap/http"/>
            <wsdl:operation name="GetCitizenInfo"><soap:operation soapAction="urn:yakeen:GetCitizenInfo"/>
              <wsdl:input><soap:body use="literal"/></wsdl:input><wsdl:output><soap:body use="literal"/></wsdl:output></wsdl:operation>
            <wsdl:operation name="GetCitizenAddress"><soap:operation soapAction="urn:yakeen:GetCitizenAddress"/>
              <wsdl:input><soap:body use="literal"/></wsdl:input><wsdl:output><soap:body use="literal"/></wsdl:output></wsdl:operation>
          </wsdl:binding>
          <wsdl:binding name="YakeenPersonSoap12" type="tns:YakeenPersonPort">
            <soap12:binding style="document" transport="http://schemas.xmlsoap.org/soap/http"/>
            <wsdl:operation name="GetCitizenInfo"><soap12:operation soapAction="urn:yakeen:GetCitizenInfo"/>
              <wsdl:input><soap12:body use="literal"/></wsdl:input><wsdl:output><soap12:body use="literal"/></wsdl:output></wsdl:operation>
            <wsdl:operation name="GetCitizenAddress"><soap12:operation soapAction="urn:yakeen:GetCitizenAddress"/>
              <wsdl:input><soap12:body use="literal"/></wsdl:input><wsdl:output><soap12:body use="literal"/></wsdl:output></wsdl:operation>
          </wsdl:binding>
          <wsdl:service name="YakeenPersonService">
            <wsdl:port name="YakeenPersonSoap" binding="tns:YakeenPersonSoap"><soap:address location="{{address}}"/></wsdl:port>
            <wsdl:port name="YakeenPersonSoap12" binding="tns:YakeenPersonSoap12"><soap12:address location="{{address}}"/></wsdl:port>
          </wsdl:service>
        </wsdl:definitions>
        """;
}
