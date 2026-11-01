using DynamicCube.Modules.Integration.Domain;
using DynamicCube.Modules.Integration.Runtime;
using Shouldly;

namespace DynamicCube.Modules.Integration.Tests;

/// <summary>WSDL 1.1 descriptions read into a connection and SOAP operations to review (ADR-0044).</summary>
public sealed class WsdlImportTests
{
    private const string Person = """
        <?xml version="1.0" encoding="utf-8"?>
        <wsdl:definitions name="PersonService" targetNamespace="http://yakeen.test/person"
            xmlns:wsdl="http://schemas.xmlsoap.org/wsdl/" xmlns:soap="http://schemas.xmlsoap.org/wsdl/soap/"
            xmlns:soap12="http://schemas.xmlsoap.org/wsdl/soap12/" xmlns:xs="http://www.w3.org/2001/XMLSchema"
            xmlns:tns="http://yakeen.test/person">
          <wsdl:documentation>Citizen and resident information.</wsdl:documentation>
          <wsdl:types>
            <xs:schema targetNamespace="http://yakeen.test/person" elementFormDefault="qualified">
              <xs:simpleType name="Nin"><xs:restriction base="xs:string"><xs:pattern value="[12][0-9]{9}"/></xs:restriction></xs:simpleType>
              <xs:complexType name="Base"><xs:sequence><xs:element name="requestId" type="xs:string" minOccurs="0"/></xs:sequence></xs:complexType>
              <xs:complexType name="PersonRequest">
                <xs:complexContent><xs:extension base="tns:Base"><xs:sequence>
                  <xs:element name="nin" type="tns:Nin"/>
                  <xs:element name="dateOfBirth" type="xs:string"/>
                  <xs:element name="tags" type="xs:string" maxOccurs="unbounded" minOccurs="0"/>
                </xs:sequence></xs:extension></xs:complexContent>
              </xs:complexType>
              <xs:complexType name="Address"><xs:sequence>
                <xs:element name="city" type="xs:string"/><xs:element name="postalCode" type="xs:int"/>
              </xs:sequence></xs:complexType>
              <xs:element name="GetPersonInfo">
                <xs:complexType><xs:sequence>
                  <xs:element name="request" type="tns:PersonRequest"/>
                  <xs:element name="chargeCode" type="xs:string" minOccurs="0"/>
                </xs:sequence></xs:complexType>
              </xs:element>
              <xs:element name="GetPersonInfoResponse">
                <xs:complexType><xs:sequence>
                  <xs:element name="return"><xs:complexType><xs:sequence>
                    <xs:element name="firstName" type="xs:string"/>
                    <xs:element name="birthDate" type="xs:date"/>
                    <xs:element name="addresses" type="tns:Address" maxOccurs="unbounded"/>
                  </xs:sequence><xs:attribute name="status" type="xs:string"/></xs:complexType></xs:element>
                </xs:sequence></xs:complexType>
              </xs:element>
            </xs:schema>
          </wsdl:types>
          <wsdl:message name="GetPersonInfoIn"><wsdl:part name="parameters" element="tns:GetPersonInfo"/></wsdl:message>
          <wsdl:message name="GetPersonInfoOut"><wsdl:part name="parameters" element="tns:GetPersonInfoResponse"/></wsdl:message>
          <wsdl:portType name="PersonPort">
            <wsdl:operation name="GetPersonInfo">
              <wsdl:documentation>Information of a citizen by national ID.</wsdl:documentation>
              <wsdl:input message="tns:GetPersonInfoIn"/><wsdl:output message="tns:GetPersonInfoOut"/>
            </wsdl:operation>
          </wsdl:portType>
          <wsdl:binding name="PersonSoap" type="tns:PersonPort">
            <soap:binding style="document" transport="http://schemas.xmlsoap.org/soap/http"/>
            <wsdl:operation name="GetPersonInfo">
              <soap:operation soapAction="urn:GetPersonInfo"/>
              <wsdl:input><soap:body use="literal"/></wsdl:input><wsdl:output><soap:body use="literal"/></wsdl:output>
            </wsdl:operation>
          </wsdl:binding>
          <wsdl:binding name="PersonSoap12" type="tns:PersonPort">
            <soap12:binding style="document" transport="http://schemas.xmlsoap.org/soap/http"/>
            <wsdl:operation name="GetPersonInfo">
              <soap12:operation soapAction="urn:GetPersonInfo"/>
              <wsdl:input><soap12:body use="literal"/></wsdl:input><wsdl:output><soap12:body use="literal"/></wsdl:output>
            </wsdl:operation>
          </wsdl:binding>
          <wsdl:service name="PersonService">
            <wsdl:port name="PersonSoap12Port" binding="tns:PersonSoap12"><soap12:address location="https://yakeen.test/ws/person"/></wsdl:port>
            <wsdl:port name="PersonSoapPort" binding="tns:PersonSoap"><soap:address location="https://yakeen.test/ws/person"/></wsdl:port>
          </wsdl:service>
        </wsdl:definitions>
        """;

    [Fact]
    public void Reads_document_literal_operations_with_their_elements_and_response_fields()
    {
        var read = WsdlImport.Read(Person);

        read.Title.ShouldBe("PersonService");
        read.SuggestedProviderCode.ShouldBe("PERSON_SERVICE");
        read.Description.ShouldBe("Citizen and resident information.");
        read.Servers.ShouldBe(["https://yakeen.test"]);
        read.Warnings.ShouldContain(w => w.Contains("SOAP 1.2 binding is skipped", StringComparison.Ordinal));

        var operation = read.Operations.ShouldHaveSingleItem();
        operation.Code.ShouldBe("GET_PERSON_INFO");
        operation.Name.ShouldBe("Information of a citizen by national ID.");
        operation.Method.ShouldBe("POST");
        operation.PathTemplate.ShouldBe("/ws/person");
        operation.Protocol.ShouldBe(OperationProtocol.Soap);
        operation.Soap!.Version.ShouldBe("1.1");
        operation.Soap.Action.ShouldBe("urn:GetPersonInfo");
        operation.Soap.OperationName.ShouldBe("GetPersonInfo");
        operation.Soap.Namespace.ShouldBe("http://yakeen.test/person");
        operation.Soap.QualifiedElements.ShouldBeTrue();

        operation.Parameters.Select(p => (p.Name, p.Target, p.IsRequired, p.Location)).ShouldBe(
        [
            ("requestId", "request.requestId", false, ParameterLocation.Body),
            ("nin", "request.nin", true, ParameterLocation.Body),
            ("dateOfBirth", "request.dateOfBirth", true, ParameterLocation.Body),
            ("chargeCode", "chargeCode", false, ParameterLocation.Body),
        ]);
        operation.Parameters.Single(p => p.Name == "dateOfBirth").IsMasked.ShouldBeTrue("a date of birth is personal data");
        operation.Warnings.ShouldContain(w => w.Contains("request.tags", StringComparison.Ordinal));

        operation.Fields.Select(f => (f.Path, f.Type)).ShouldBe(
        [
            ("return.firstName", "string"),
            ("return.birthDate", "string:date"),
            ("return.addresses[*].city", "string"),
            ("return.addresses[*].postalCode", "number"),
            ("return.status", "string"),
        ]);
        operation.Fields.Select(f => f.Name).ShouldBe(["returnFirstName", "returnBirthDate", "addressesCity", "addressesPostalCode", "returnStatus"]);
    }

    [Fact]
    public void Reads_rpc_literal_bindings_and_suggests_ws_security_when_the_policy_names_a_username_token()
    {
        var read = WsdlImport.Read("""
            <definitions name="Licences" targetNamespace="urn:licences" xmlns="http://schemas.xmlsoap.org/wsdl/"
                xmlns:soap="http://schemas.xmlsoap.org/wsdl/soap/" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:tns="urn:licences">
              <!-- policy: sp:UsernameToken -->
              <message name="CheckIn"><part name="licenceNo" type="xsd:string"/><part name="year" type="xsd:int"/></message>
              <message name="CheckOut"><part name="valid" type="xsd:boolean"/></message>
              <portType name="LicencePort"><operation name="CheckLicence"><input message="tns:CheckIn"/><output message="tns:CheckOut"/></operation></portType>
              <binding name="LicenceBinding" type="tns:LicencePort">
                <soap:binding style="rpc" transport="http://schemas.xmlsoap.org/soap/http"/>
                <operation name="CheckLicence"><soap:operation soapAction=""/>
                  <input><soap:body use="literal" namespace="urn:licences:rpc"/></input><output><soap:body use="literal"/></output>
                </operation>
              </binding>
              <service name="Licences"><port name="p" binding="tns:LicenceBinding"><soap:address location="http://licences.test/"/></port></service>
            </definitions>
            """);

        var operation = read.Operations.ShouldHaveSingleItem();
        operation.PathTemplate.ShouldBe(string.Empty, "the address itself");
        operation.Soap!.OperationName.ShouldBe("CheckLicence");
        operation.Soap.Namespace.ShouldBe("urn:licences:rpc");
        operation.Soap.QualifiedElements.ShouldBeFalse();
        operation.Soap.Action.ShouldBeNull();
        operation.Parameters.Select(p => p.Target).ShouldBe(["licenceNo", "year"]);
        operation.Fields.Select(f => (f.Path, f.Type)).ShouldBe([("valid", "boolean")]);
        read.Authentication.ShouldHaveSingleItem().AuthType.ShouldBe(AuthType.WsSecurity);
    }

    [Fact]
    public void Refuses_what_it_cannot_read_and_warns_about_external_schemas()
    {
        Should.Throw<OpenApiImportException>(() => WsdlImport.Read("<description xmlns=\"http://www.w3.org/ns/wsdl\"/>")).Message.ShouldContain("WSDL 2.0");
        Should.Throw<OpenApiImportException>(() => WsdlImport.Read("<html><body>Not found</body></html>")).Message.ShouldContain("not a WSDL");
        Should.Throw<OpenApiImportException>(() => WsdlImport.Read("<definitions")).Message.ShouldContain("XML");
        Should.Throw<OpenApiImportException>(() => WsdlImport.Read(Person.Replace("<soap:address", "<other:address xmlns:other=\"urn:x\"", StringComparison.Ordinal)
            .Replace("<soap12:address", "<other:address xmlns:other=\"urn:x\"", StringComparison.Ordinal))).Message.ShouldContain("No SOAP port");

        var external = WsdlImport.Read(Person.Replace(
            "<xs:simpleType name=\"Nin\">",
            "<xs:import namespace=\"urn:common\" schemaLocation=\"https://yakeen.test/common.xsd\"/><xs:simpleType name=\"Nin\">",
            StringComparison.Ordinal));
        external.Warnings.ShouldContain(w => w.Contains("https://yakeen.test/common.xsd", StringComparison.Ordinal));
        WsdlImport.IsXml("  <?xml version=\"1.0\"?><a/>").ShouldBeTrue();
        WsdlImport.IsXml("openapi: 3.0.0").ShouldBeFalse();
    }
}
