# The MedTech service device and FHIR destination mappings samples overview

> [!NOTE]
> [Fast Healthcare Interoperability Resources (FHIR&#174;)](https://www.hl7.org/fhir/) is an open healthcare specification.

The MedTech service requires two types of JSON mappings that are added to your MedTech service through the Azure portal or Azure Resource Manager API. The device mapping is the first type and controls mapping values in the device data sent to the MedTech service to an internal, normalized data object. The device mapping contains expressions that the MedTech service uses to extract types, device identifiers, measurement date time, and measurement value(s). The FHIR destination mapping is the second type and controls how the normalized data is mapped to FHIR Observations.

## Samples content

The MedTech service samples folder will contain the following resources:

* Device mapping
* FHIR destination mapping
* README
* Test device message

Use the MedTech service [Mapping debugger](https://learn.microsoft.com/azure/healthcare-apis/iot/how-to-use-mapping-debugger) for assistance creating, updating, and troubleshooting the MedTech service device and FHIR destination mappings. The Mapping debugger enables you to easily view and make inline adjustments in real-time, without ever having to leave the Azure portal. The Mapping debugger can also be used for uploading test device messages to see how they'll look after being processed into normalized messages and transformed into FHIR Observations.

## Resources

* To learn about the MedTech service, see [What is MedTech service?](https://learn.microsoft.com/azure/healthcare-apis/iot/overview)

* To learn about the MedTech service device data processing stages, see [Overview of the MedTech service device data processing stages](https://learn.microsoft.com/azure/healthcare-apis/iot/overview-of-device-data-processing-stages).

* To learn about the different deployment methods for the MedTech service, see [Choose a deployment method for the MedTech service](https://learn.microsoft.com/azure/healthcare-apis/iot/deploy-choose-method).

* For an overview of the MedTech service device mapping, see [Overview of the MedTech service device mapping](https://learn.microsoft.com/azure/healthcare-apis/iot/overview-of-device-mapping).

* For an overview of the MedTech service FHIR destination mapping, see [Overview of the MedTech service FHIR destination mapping](https://learn.microsoft.com/azure/healthcare-apis/iot/overview-of-fhir-destination-mapping).

FHIR® is a registered trademark of Health Level Seven International, registered in the U.S. Trademark Office and is used with their permission.
