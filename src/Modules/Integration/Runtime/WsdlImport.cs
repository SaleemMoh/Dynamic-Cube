using System.Xml.Linq;
using DynamicCube.Modules.Integration.Domain;

namespace DynamicCube.Modules.Integration.Runtime;

/// <summary>
/// Reads WSDL 1.1 descriptions with their inline XML schemas (ADR-0044) into the same suggestions as an OpenAPI
/// description: the provider, the connection's address and SOAP operations whose inputs are the request's elements and
/// whose response fields are paths into the response read as JSON.
/// </summary>
/// <remarks>
/// Document/literal (wrapped or not) and RPC/literal bindings are read; when a port type has a SOAP 1.1 and a SOAP 1.2
/// binding, the 1.1 one is used. Schemas outside the description (<c>xs:import</c> with a location) are not fetched.
/// </remarks>
public static class WsdlImport
{
    private const string WsdlNs = "http://schemas.xmlsoap.org/wsdl/";
    private const string Soap11Binding = "http://schemas.xmlsoap.org/wsdl/soap/";
    private const string Soap12Binding = "http://schemas.xmlsoap.org/wsdl/soap12/";
    private const string XsdNs = "http://www.w3.org/2001/XMLSchema";
    private const int MaxBodyDepth = 4;
    private const int MaxOutputDepth = 6;
    private const int MaxFields = 80;

    private static readonly XNamespace Wsdl = WsdlNs;
    private static readonly XNamespace Xsd = XsdNs;

    /// <summary>Whether the text looks like XML (a WSDL) rather than an OpenAPI description.</summary>
    public static bool IsXml(string? text) => text is not null && text.TrimStart().StartsWith('<');

    public static OpenApiReadResult Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > OpenApiImport.MaxLength)
        {
            throw new OpenApiImportException($"The description is larger than {OpenApiImport.MaxLength / 1_000_000} MB.");
        }

        var root = SoapXml.Parse(text)?.Root ?? throw new OpenApiImportException("The text is not well-formed XML.");
        if (root.Name.LocalName == "description")
        {
            throw new OpenApiImportException("WSDL 2.0 is not supported; ask the provider for the WSDL 1.1 description.");
        }

        if (root.Name != Wsdl + "definitions")
        {
            throw new OpenApiImportException("The XML is not a WSDL 1.1 description (no wsdl:definitions).");
        }

        var warnings = new List<string>();
        var targetNamespace = (string?)root.Attribute("targetNamespace") ?? string.Empty;
        var schemas = new SchemaSet(root.Element(Wsdl + "types")?.Elements(Xsd + "schema") ?? [], warnings);
        var messages = root.Elements(Wsdl + "message").ToDictionary(
            m => XName.Get((string?)m.Attribute("name") ?? string.Empty, targetNamespace),
            m => m.Elements(Wsdl + "part").Select(p => new Part((string?)p.Attribute("name") ?? "part", QName(p, (string?)p.Attribute("element")), QName(p, (string?)p.Attribute("type")))).ToList());
        var portTypes = root.Elements(Wsdl + "portType").ToDictionary(
            p => XName.Get((string?)p.Attribute("name") ?? string.Empty, targetNamespace),
            p => p.Elements(Wsdl + "operation").ToList());
        var bindings = root.Elements(Wsdl + "binding").ToDictionary(b => XName.Get((string?)b.Attribute("name") ?? string.Empty, targetNamespace));

        // The SOAP ports, one per port type (SOAP 1.1 preferred).
        var ports = new List<(XElement Binding, string Version, Uri Address, string Service)>();
        foreach (var service in root.Elements(Wsdl + "service"))
        {
            foreach (var port in service.Elements(Wsdl + "port"))
            {
                if (QName(port, (string?)port.Attribute("binding")) is not { } bindingName || !bindings.TryGetValue(bindingName, out var binding))
                {
                    continue;
                }

                var version = binding.Element(XName.Get("binding", Soap11Binding)) is not null ? "1.1" : binding.Element(XName.Get("binding", Soap12Binding)) is not null ? "1.2" : null;
                var location = port.Elements().FirstOrDefault(e => e.Name.LocalName == "address" && e.Name.NamespaceName is Soap11Binding or Soap12Binding)?.Attribute("location")?.Value;
                if (version is null || !Uri.TryCreate(location, UriKind.Absolute, out var address))
                {
                    continue;
                }

                ports.Add((binding, version, address, (string?)service.Attribute("name") ?? "Service"));
            }
        }

        var chosen = ports.GroupBy(p => QName(p.Binding, (string?)p.Binding.Attribute("type")))
            .Select(g => g.OrderBy(p => p.Version, StringComparer.Ordinal).First())
            .ToList();
        if (chosen.Count == 0)
        {
            throw new OpenApiImportException("No SOAP port with an address was found in the description.");
        }

        if (ports.Count > chosen.Count)
        {
            warnings.Add("The SOAP 1.2 binding is skipped where a SOAP 1.1 binding of the same operations exists.");
        }

        var codes = new HashSet<string>(StringComparer.Ordinal);
        var operations = new List<ImportedOperation>();
        foreach (var (binding, version, address, _) in chosen)
        {
            var soapNs = version == "1.1" ? Soap11Binding : Soap12Binding;
            var style = (string?)binding.Element(XName.Get("binding", soapNs))?.Attribute("style") ?? "document";
            var portTypeName = QName(binding, (string?)binding.Attribute("type"));
            var abstractOperations = portTypeName is not null && portTypes.TryGetValue(portTypeName, out var list) ? list : [];
            foreach (var bound in binding.Elements(Wsdl + "operation"))
            {
                var name = (string?)bound.Attribute("name") ?? string.Empty;
                var definition = abstractOperations.FirstOrDefault(o => (string?)o.Attribute("name") == name);
                if (definition is null)
                {
                    warnings.Add($"Operation '{name}' of the binding has no definition in its port type.");
                    continue;
                }

                operations.Add(Operation(name, bound, definition, version, soapNs, style, address, targetNamespace, messages, schemas, codes));
            }
        }

        var title = (string?)root.Attribute("name") ?? chosen[0].Service;
        var servers = chosen.Select(p => p.Address.GetLeftPart(UriPartial.Authority)).Distinct(StringComparer.Ordinal).ToList();
        var authentication = text.Contains("UsernameToken", StringComparison.Ordinal)
            ? new List<OpenApiAuthSuggestion> { new("UsernameToken", AuthType.WsSecurity, null, null, null) }
            : [];
        return new OpenApiReadResult(
            title,
            null,
            OpenApiImport.FirstLine(root.Element(Wsdl + "documentation")?.Value),
            OpenApiImport.Truncate(OpenApiImport.UpperSnake(title), 30),
            servers,
            authentication,
            operations,
            warnings.Distinct(StringComparer.Ordinal).Take(20).ToList());
    }

    private static ImportedOperation Operation(
        string name,
        XElement bound,
        XElement definition,
        string version,
        string soapNs,
        string bindingStyle,
        Uri address,
        string targetNamespace,
        Dictionary<XName, List<Part>> messages,
        SchemaSet schemas,
        HashSet<string> codes)
    {
        var warnings = new List<string>();
        var soapOperation = bound.Element(XName.Get("operation", soapNs));
        var style = (string?)soapOperation?.Attribute("style") ?? bindingStyle;
        var inputBody = bound.Element(Wsdl + "input")?.Element(XName.Get("body", soapNs));
        if ((string?)inputBody?.Attribute("use") == "encoded")
        {
            warnings.Add("The binding uses SOAP encoding; the envelope is sent as literal XML, which the provider may refuse.");
        }

        var input = Message(definition, "input", messages);
        var output = Message(definition, "output", messages);
        var names = new HashSet<string>(StringComparer.Ordinal);
        var parameters = new List<OperationParameter>();
        SoapSettings soap;
        if (style == "rpc")
        {
            soap = new SoapSettings
            {
                Version = version,
                OperationName = name,
                Namespace = (string?)inputBody?.Attribute("namespace") ?? Blank(targetNamespace),
                QualifiedElements = false,
            };
            foreach (var part in input)
            {
                AddInputs(schemas.Content(part.Type, part.Element), part.Name, true, 0, parameters, names, warnings, schemas);
            }
        }
        else
        {
            var wrapper = input.FirstOrDefault(p => p.Element is not null)?.Element;
            if (input.Count > 1)
            {
                warnings.Add("Only the first part of the request message is sent.");
            }

            soap = new SoapSettings
            {
                Version = version,
                OperationName = wrapper?.LocalName ?? name,
                Namespace = Blank(wrapper?.NamespaceName) ?? Blank(targetNamespace),
                QualifiedElements = wrapper is null || schemas.IsQualified(wrapper.Namespace),
            };
            if (wrapper is not null)
            {
                foreach (var child in schemas.Children(schemas.Content(null, wrapper)))
                {
                    AddInputs(child, child.Name, child.MinOccurs > 0, 0, parameters, names, warnings, schemas);
                }
            }
        }

        soap.Action = Blank((string?)soapOperation?.Attribute("soapAction"));

        var fields = new List<ImportedField>();
        var outputNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in output)
        {
            var content = schemas.Content(part.Type, part.Element);
            if (style == "rpc")
            {
                // The response element wraps the parts: paths start with the part's name.
                AddFields(content, part.Name, 0, fields, outputNames, schemas);
            }
            else
            {
                foreach (var child in schemas.Children(content))
                {
                    AddFields(child, child.Name, 0, fields, outputNames, schemas);
                }
            }

            if (style != "rpc")
            {
                break;
            }
        }

        if (fields.Count >= MaxFields)
        {
            warnings.Add($"Only the first {MaxFields} response fields are offered.");
        }

        var path = address.AbsolutePath == "/" ? string.Empty : address.AbsolutePath;
        var documentation = definition.Element(Wsdl + "documentation")?.Value;
        return new ImportedOperation(
            OpenApiImport.Unique(OpenApiImport.Truncate(OpenApiImport.UpperSnake(name), 40), codes),
            "POST",
            path,
            OpenApiImport.Truncate(OpenApiImport.FirstLine(documentation) ?? name, 200),
            documentation?.Trim(),
            null,
            parameters,
            fields,
            warnings,
            OperationProtocol.Soap,
            soap);
    }

    private static void AddInputs(Node node, string path, bool required, int depth, List<OperationParameter> parameters, HashSet<string> names, List<string> warnings, SchemaSet schemas)
    {
        if (node.Repeats)
        {
            warnings.Add($"Request element '{path}' is a list; lists are not filled from inputs.");
            return;
        }

        var children = schemas.Children(node);
        if (children.Count == 0)
        {
            parameters.Add(new OperationParameter
            {
                Name = OpenApiImport.Unique(OpenApiImport.ParameterName(path.Split('.')[^1]), names),
                Location = ParameterLocation.Body,
                Target = path,
                IsRequired = required,
                IsMasked = OpenApiImport.Sensitive(path),
            });
            return;
        }

        if (depth + 1 >= MaxBodyDepth)
        {
            warnings.Add($"Request element '{path}' is nested too deeply; add its elements by hand.");
            return;
        }

        foreach (var child in children)
        {
            AddInputs(child, path + "." + child.Name, required && child.MinOccurs > 0, depth + 1, parameters, names, warnings, schemas);
        }
    }

    private static void AddFields(Node node, string path, int depth, List<ImportedField> fields, HashSet<string> names, SchemaSet schemas)
    {
        if (fields.Count >= MaxFields || depth > MaxOutputDepth)
        {
            return;
        }

        var listPath = node.Repeats ? path + "[*]" : path;
        var children = schemas.Children(node);
        if (children.Count == 0)
        {
            fields.Add(new ImportedField(OpenApiImport.Unique(OpenApiImport.OutputName(listPath), names), listPath, node.Type + (node.Repeats ? "[]" : string.Empty)));
            return;
        }

        foreach (var child in children)
        {
            AddFields(child, listPath + "." + child.Name, depth + 1, fields, names, schemas);
        }
    }

    private static List<Part> Message(XElement operation, string direction, Dictionary<XName, List<Part>> messages) =>
        operation.Element(Wsdl + direction) is { } io && QName(io, (string?)io.Attribute("message")) is { } name && messages.TryGetValue(name, out var parts) ? parts : [];

    /// <summary>A prefixed name (<c>tns:Person</c>) resolved with the namespaces in scope of the element holding it.</summary>
    private static XName? QName(XElement context, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var colon = value.IndexOf(':', StringComparison.Ordinal);
        var prefix = colon < 0 ? string.Empty : value[..colon];
        var local = value[(colon + 1)..];
        var ns = prefix.Length == 0 ? context.GetDefaultNamespace() : context.GetNamespaceOfPrefix(prefix) ?? XNamespace.None;
        return ns + local;
    }

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private sealed record Part(string Name, XName? Element, XName? Type);

    /// <summary>An element of a schema: its name, its type's name (simple types), its declaration and how often it occurs.</summary>
    private sealed record Node(string Name, string Type, XElement? Declaration, XElement? ComplexType, XElement SchemaElement, int MinOccurs, bool Repeats);

    /// <summary>The inline schemas: their elements and types by qualified name, and whether child elements are qualified.</summary>
    private sealed class SchemaSet
    {
        private readonly Dictionary<XName, (XElement Declaration, XElement Schema)> _elements = [];
        private readonly Dictionary<XName, (XElement Declaration, XElement Schema)> _complexTypes = [];
        private readonly Dictionary<XName, XElement> _simpleTypes = [];
        private readonly Dictionary<XNamespace, bool> _qualified = [];

        public SchemaSet(IEnumerable<XElement> schemas, List<string> warnings)
        {
            foreach (var schema in schemas)
            {
                XNamespace ns = (string?)schema.Attribute("targetNamespace") ?? string.Empty;
                _qualified[ns] = (string?)schema.Attribute("elementFormDefault") == "qualified";
                foreach (var element in schema.Elements(Xsd + "element"))
                {
                    _elements[ns + ((string?)element.Attribute("name") ?? string.Empty)] = (element, schema);
                }

                foreach (var type in schema.Elements(Xsd + "complexType"))
                {
                    _complexTypes[ns + ((string?)type.Attribute("name") ?? string.Empty)] = (type, schema);
                }

                foreach (var type in schema.Elements(Xsd + "simpleType"))
                {
                    _simpleTypes[ns + ((string?)type.Attribute("name") ?? string.Empty)] = type;
                }

                foreach (var external in schema.Elements().Where(e => e.Name == Xsd + "import" || e.Name == Xsd + "include"))
                {
                    if ((string?)external.Attribute("schemaLocation") is { } location)
                    {
                        warnings.Add($"The schema '{location}' is outside the description and is not read; add its elements by hand.");
                    }
                }
            }
        }

        public bool IsQualified(XNamespace ns) => _qualified.TryGetValue(ns, out var qualified) && qualified;

        /// <summary>The node of a message part: a global element, or a part typed directly.</summary>
        public Node Content(XName? type, XName? element)
        {
            if (element is not null && _elements.TryGetValue(element, out var global))
            {
                return Declared(global.Declaration, global.Schema);
            }

            if (type is not null)
            {
                return _complexTypes.TryGetValue(type, out var complex)
                    ? new Node(element?.LocalName ?? type.LocalName, "value", null, complex.Declaration, complex.Schema, 1, false)
                    : new Node(element?.LocalName ?? type.LocalName, SimpleType(type), null, null, new XElement(Xsd + "schema"), 1, false);
            }

            return new Node(element?.LocalName ?? "value", "string", null, null, new XElement(Xsd + "schema"), 1, false);
        }

        /// <summary>The child elements of a node's complex type (sequence, all and choice; extensions add to their base).</summary>
        public List<Node> Children(Node node) => node.ComplexType is null ? [] : Members(node.ComplexType, node.SchemaElement, 0);

        private List<Node> Members(XElement container, XElement schema, int depth)
        {
            var result = new List<Node>();
            if (depth > 10)
            {
                return result;
            }

            foreach (var item in container.Elements())
            {
                if (item.Name == Xsd + "element")
                {
                    if (item.Attribute("ref") is { } reference && QName(item, reference.Value) is { } refName && _elements.TryGetValue(refName, out var target))
                    {
                        result.Add(Declared(target.Declaration, target.Schema) with { MinOccurs = Min(item), Repeats = Repeats(item) });
                    }
                    else if (item.Attribute("name") is not null)
                    {
                        result.Add(Declared(item, schema));
                    }
                }
                else if (item.Name == Xsd + "sequence" || item.Name == Xsd + "all" || item.Name == Xsd + "choice")
                {
                    result.AddRange(Members(item, schema, depth + 1));
                }
                else if (item.Name == Xsd + "complexContent" || item.Name == Xsd + "simpleContent")
                {
                    foreach (var derivation in item.Elements().Where(e => e.Name == Xsd + "extension" || e.Name == Xsd + "restriction"))
                    {
                        if (derivation.Name == Xsd + "extension" && QName(derivation, (string?)derivation.Attribute("base")) is { } baseName && _complexTypes.TryGetValue(baseName, out var baseType))
                        {
                            result.AddRange(Members(baseType.Declaration, baseType.Schema, depth + 1));
                        }

                        result.AddRange(Members(derivation, schema, depth + 1));
                    }
                }
                else if (item.Name == Xsd + "attribute" && item.Attribute("name") is { } attribute)
                {
                    // Attributes read as properties of the element (see SoapXml).
                    result.Add(new Node(attribute.Value, SimpleType(QName(item, (string?)item.Attribute("type"))), item, null, schema, (string?)item.Attribute("use") == "required" ? 1 : 0, false));
                }
            }

            return result;
        }

        private Node Declared(XElement declaration, XElement schema)
        {
            var name = (string?)declaration.Attribute("name") ?? "value";
            var inline = declaration.Element(Xsd + "complexType");
            var typeName = QName(declaration, (string?)declaration.Attribute("type"));
            if (inline is not null)
            {
                return new Node(name, "value", declaration, inline, schema, Min(declaration), Repeats(declaration));
            }

            if (typeName is not null && _complexTypes.TryGetValue(typeName, out var complex))
            {
                return new Node(name, "value", declaration, complex.Declaration, complex.Schema, Min(declaration), Repeats(declaration));
            }

            return new Node(name, SimpleType(typeName), declaration, null, schema, Min(declaration), Repeats(declaration));
        }

        /// <summary>The kind of a simple type, as the OpenAPI import names them.</summary>
        private string SimpleType(XName? type)
        {
            for (var i = 0; i < 5 && type is not null && type.Namespace != Xsd && _simpleTypes.TryGetValue(type, out var simple); i++)
            {
                var restriction = simple.Element(Xsd + "restriction");
                type = restriction is null ? null : QName(restriction, (string?)restriction.Attribute("base"));
            }

            return type?.Namespace == Xsd
                ? type.LocalName switch
                {
                    "int" or "integer" or "long" or "short" or "decimal" or "double" or "float" or "byte" or "nonNegativeInteger" or "positiveInteger" or "unsignedInt" or "unsignedLong" => "number",
                    "boolean" => "boolean",
                    "date" => "string:date",
                    "dateTime" => "string:date-time",
                    _ => "string",
                }
                : "string";
        }

        private static int Min(XElement declaration) => (string?)declaration.Attribute("minOccurs") == "0" ? 0 : 1;

        private static bool Repeats(XElement declaration) =>
            (string?)declaration.Attribute("maxOccurs") is { } max && (max == "unbounded" || (int.TryParse(max, out var count) && count > 1));
    }
}
