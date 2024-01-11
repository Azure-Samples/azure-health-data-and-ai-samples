# ARM templates for FHIR service integration with Azure Active Directory B2C

You can use the FHIR® service in Azure Health Data Services with Azure Active Directory B2C (Azure AD B2C). This capability gives healthcare organizations a secure and convenient way to grant access to the FHIR service with fine-grained access control for different users or groups, without creating or commingling user accounts in their organization’s Microsoft Entra ID tenant.

## ARM templates resources

- Deploy an Azure AD B2C tenant with the [b2c-arm-template.json](b2c-arm-template.json)
- Configure API permissions with the [oauth2Permissions.json](oauth2Permissions.json)
- Deploy the FHIR service with the [fhir-service-arm-template.json](fhir-service-arm-template.json)

## Documentation

- [Use Azure Active Directory B2C to grant access to the FHIR service](https://learn.microsoft.com/azure/healthcare-apis/fhir/azure-ad-b2c-setup)

- [Configure multiple service identity providers for the FHIR service](https://learn.microsoft.com/azure/healthcare-apis/fhir/configure-identity-providers)

- [Troubleshoot identity provider configuration for the FHIR service](https://learn.microsoft.com/azure/healthcare-apis/fhir/troubleshoot-identity-provider-configuration)

- [SMART on FHIR in Azure Health Data Services](https://learn.microsoft.com/azure/healthcare-apis/fhir/smart-on-fhir)

- [Sample: Azure ONC (g)(10) SMART on FHIR](https://github.com/Azure-Samples/azure-health-data-and-ai-samples/tree/main/samples/patientandpopulationservices-smartonfhir-oncg10)

FHIR® is a registered trademark of HL7 and is used with the permission of HL7.
