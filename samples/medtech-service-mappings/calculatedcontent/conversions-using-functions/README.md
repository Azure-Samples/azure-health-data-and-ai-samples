# README

## Scenario

This example shows how the MedTech service can extract and normalize incoming device data using functions defined inside of the CalculatedContent mapping templates.

In this scenario, we set up two devices (Device A and Device B) to emit exercise data to the MedTech service. Both devices collect the same set of telemetry:

- Height
- Distance traveled

We need to account for the following:

- The schemas are different for each type of device message.
- Device messages are submitted at different frequencies per device type.
- Units of measurement are different per device type. It's desired to have the final data stored in the these units:
  - Height (meters)
  - Distance traveled (meters)

Rather than create an upstream process as a workaround, we can customize the MedTech normalization process to achieve these conversions.

## Overview of the Device A device message

During an exercise session, device data is submitted at one minute intervals to the MedTech service.

A sample device message looks like this:

```json
{
  "deviceType": "deviceTypeA",
  "deviceId": "device123",
  "timestamp": "2023-05-13T23:45:44Z",
  "userId": "user123",
  "heightInInches": 72,
  "distanceInMiles": 0.6214
}
```

Telemetry is emitted in the following units:

- Height (inches)
- Distance traveled (miles)

We can apply the following conversion logic to achieve the correct units:

- Multiply `heightInInches` by `0.0254` to get meters
- Multiply `distanceInMiles` by `1609.344` to get meters

## Creating the template for Device A

We start with a `typeMatchExpression` which allows us to match this device message to a template that we define.

```json
"typeMatchExpression": "$..[?(@.deviceType == 'deviceTypeA')]"
```

The JsonPath matches on device messages produced by Device A.

We do the following:

1. Match on a JSON Object that contains a field `deviceType` that has a value of `deviceTypeA`. **Note:** This matches on the entire device message object.
2. Return the entire device message.

Data is then extracted/normalized from this device data based on the remaining expressions within the template. This produces the following normalized data:

```json
[
  {
    "type": "DeviceADataElements",
    "occurrenceTimeUtc": "2023-05-13T23:45:44Z",
    "deviceId": "device123",
    "patientId": "user123",
    "properties": [
      {
        "name": "heightInMeters",
        "value": "1.8288"
      },
      {
        "name": "distanceInMeters",
        "value": "1000.0463616"
      }
    ]
  }
]
```

For more information on defining templates within CalculatedContent mappings, see [How to use CalculatedContent mappings with the MedTech service device mapping](https://learn.microsoft.com/azure/healthcare-apis/iot/how-to-use-calculatedcontent-mappings).

## Overview of the Device B device message

During an exercise session, data is collected and stored on the device. Data is collected for each minute of the workout. At the end of the session, the data is aggregated into a single device message and submitted to the MedTech service.

A sample device message looks like this:

```json
{
  "deviceId": "deviceTypeB_456",
  "workoutStartTime": "2023-05-23T23:01:00Z",
  "workoutEndTime": "2023-05-23T23:03:00Z",
  "userDetails": {
    "id": "user123",
    "heightInMeters": 1.8288
  },
  "data": [
    {
      "startTime": "2023-05-23T23:01:00Z",
      "distanceInYards": 30.6
    },
    {
      "startTime": "2023-05-23T23:02:00Z",
      "distanceInYards": 32.1
    },
    {
      "startTime": "2023-05-23T23:03:00Z",
      "distanceInYards": 29.8
    }
  ]
}
```

We can apply the following conversion logic:

- Do nothing to `heightInMeters` as it is already in the desired unit
- Divide `distanceInYards` by `1.0936` to get meters

## Creating the template for Device B

We start with a `typeMatchExpression`, which allows us to match this device message to a template that we define.

```json
"typeMatchExpression": {
      "value": "to_array(@) | [? starts_with(deviceId, 'deviceTypeB')].data[] ",
      "language": "JmesPath"
  },
```

The JmesPath matches on device messages produced by Device B and processes each element within the `data` array.

We do the following:

1. Convert the incoming object to an array. This is a prerequisite to using [filtering](https://jmespath.org/specification.html#filter-expressions) in JmesPath.
2. Filter the array to only include objects which have a field `deviceId` with a value that starts with the string `deviceTypeB`. **Note:** This matches on the entire device message object.
3. Return a collection of all elements contained within the `data` array.

Each element in the `data` array will be normalized according to the expressions within the template. Each element is accessible using a special token called `matchedToken`. The original device message is accessible as well. In this way, values for each `data` element as well as in the outer message can be extracted. This produces the following normalized data:

```json
[
  {
    "type": "DeviceBDataElements",
    "occurrenceTimeUtc": "2023-05-23T23:01:00Z",
    "deviceId": "deviceTypeB_456",
    "patientId": "user123",
    "properties": [
      {
        "name": "heightInMeters",
        "value": "1.8288"
      },
      {
        "name": "distanceInMeters",
        "value": "27.9809802487198"
      }
    ]
  },
  {
    "type": "DeviceBDataElements",
    "occurrenceTimeUtc": "2023-05-23T23:02:00Z",
    "deviceId": "deviceTypeB_456",
    "patientId": "user123",
    "properties": [
      {
        "name": "heightInMeters",
        "value": "1.8288"
      },
      {
        "name": "distanceInMeters",
        "value": "29.3525969275786"
      }
    ]
  },
  {
    "type": "DeviceBDataElements",
    "occurrenceTimeUtc": "2023-05-23T23:03:00Z",
    "deviceId": "deviceTypeB_456",
    "patientId": "user123",
    "properties": [
      {
        "name": "heightInMeters",
        "value": "1.8288"
      },
      {
        "name": "distanceInMeters",
        "value": "27.2494513533285"
      }
    ]
  }
]
```

For more information on defining templates within CalculatedContent mappings, see [How to use CalculatedContent mappings with the MedTech service device mapping](https://learn.microsoft.com/azure/healthcare-apis/iot/how-to-use-calculatedcontent-mappings).
