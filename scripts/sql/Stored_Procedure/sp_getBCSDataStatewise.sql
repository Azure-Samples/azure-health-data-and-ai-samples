USE
[fhirdb]
GO
IF EXISTS(SELECT 1 FROM sys.procedures WHERE Name = 'sp_getBCSDataStatewise')
BEGIN 
	DROP PROCEDURE [dbo].[sp_getBCSDataStatewise]
END
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:      <Author, , Name>
-- Create Date: <Create Date, , >
-- Description: <Description, , >
-- =============================================
CREATE PROCEDURE sp_getBCSDataStatewise
(
    @vMeasurementPeriodStartDate DATE NULL,
	@vMeasurementPeriodEndDate DATE NULL
)
AS
BEGIN
WITH ComposeIncludeValueSets (id, url)
AS
(
    SELECT 
        id,
        [compose.include.valueSet.value]
    FROM [fhir].ValueSet
    CROSS APPLY OPENJSON (ValueSet.[compose.include]) WITH (
        [compose.include.valueSet] VARCHAR(8000) '$.valueSet'
    ) AS IncludeSet
    CROSS APPLY OPENJSON (IncludeSet.[compose.include.valueSet]) WITH (
        [compose.include.valueSet.value] VARCHAR(256) '$'
    )
),
EncounterTypeExpanded (id, code, [system])
AS
(
    SELECT
        id,
        JSON_VALUE(EncounterType.[type.coding],'$[0].code'),
        JSON_VALUE(EncounterType.[type.coding],'$[0].system')
    FROM [fhir].[EncounterType] EncounterType     
),
BreastCancerScreeningEligiblePatients (PatientId, PatientResourceVersionId, PatientResourceLastUpdated, PatientBirthDate, PatientGender, PatientAgeAtEncounter, EncounterDate)
AS
(
    SELECT
        Patient.id as PatientId,
        Patient.[meta.versionId] as PatientResourceVersionId,
        Patient.[meta.lastUpdated] as PatientResourceLastUpdated,
        Patient.birthDate as PatientBirthDate,
        Patient.gender as PatientGender,
        DATEDIFF(year, Patient.birthDate, Encounter.[period.start]) as AgeAtEncounter,
        Encounter.[period.end] as EncounterDate
    FROM
        [fhir].[Encounter] as Encounter
    -- join with EncounterTypeExpanded for encounter codes
    INNER JOIN EncounterTypeExpanded ON
    Encounter.Id = EncounterTypeExpanded.id
    AND EncounterTypeExpanded.code IN ('86013001','185345009','3391000175108', '444971000124105', '439708006', '90526000')
    AND Encounter.[period.end] BETWEEN @vMeasurementPeriodStartDate AND @vMeasurementPeriodEndDate

    -- Join to the patient
    INNER JOIN [fhir].[Patient] as Patient         
        ON Patient.id = SUBSTRING([Encounter].[subject.reference], 9, 1000)

    WHERE
        Patient.gender = 'female'
       AND DATEDIFF(year, Patient.birthDate, @vMeasurementPeriodEndDate) >= 52
       AND DATEDIFF(year, Patient.birthDate, @vMeasurementPeriodEndDate) <= 74       
),
MammogramProcedure(id,PatientID,codeid,codeextension,[coding],codetext,performedperiod,codingsystem,codingcode,codingdisplay)
AS
(
SELECT 
    [pro].[id],
    [subject.reference] AS PatientID,
    [code.id],
    [code.extension],
    [code.coding] AS coding,
    [code.text],
    [performed.period.end],
    proSystem.[system] AS [code.coding.system],
    proSystem.[code] AS [code.coding.code],
    proSystem.[display] AS [code.coding.display]
FROM [fhir].[Procedure] AS [pro]
CROSS APPLY openjson (pro.[code.coding]) WITH (
        [system]            VARCHAR(256)        '$.system',
        [code]            VARCHAR(256)        '$.code',
        [display]            VARCHAR(256)        '$.display'
    ) proSystem
WHERE proSystem.code IN ('241055006','24623002','71651007')   
),
BCSNumerator (PatientID, [State])
AS
(
SELECT DISTINCT
    Patient.id AS PatientID, JSON_VALUE(Patient.[address],'$[0].state') [state]
    FROM [fhir].[Patient] AS Patient
    INNER JOIN MammogramProcedure ON
    Patient.id = SUBSTRING(MammogramProcedure.PatientID, 9, 1000)
    INNER JOIN BreastCancerScreeningEligiblePatients ON
    BreastCancerScreeningEligiblePatients.PatientId = Patient.Id 
    AND
    DATEDIFF(MONTH, CONVERT (DATETIMEOFFSET,MammogramProcedure.performedperiod,111), @vMeasurementPeriodEndDate) < = 48
    --CONVERT (DATETIMEOFFSET,MammogramProcedure.performedperiod,111) >= DATEFROMPARTS(YEAR(@vMeasurementPeriodStartDate)-2,10,1) 
    --AND 
    --CONVERT (DATETIMEOFFSET,MammogramProcedure.performedperiod,111) <= @vMeasurementPeriodEndDate
),

StatewisePercentage ( [State], Percentages)
	AS (SELECT [state], CONVERT(DECIMAL(10,2),COUNT(*) * 100.0 / SUM(COUNT(*)) OVER()) AS [Percentage]
	FROM BCSNumerator
	GROUP BY [state])

Select '0 - 20' as [Percentages], [State]
	FROM StatewisePercentage WHERE Percentages between 0 and 20
	Union All
	Select '21 - 40' as [Percentages], [State]
	FROM StatewisePercentage WHERE Percentages between 21 and 40
	Union All
	Select '41 - 60' as [Percentages], [State]
	FROM StatewisePercentage WHERE Percentages between 41 and 60
	Union All
	Select '61 - 80' as [Percentages], [State]
	FROM StatewisePercentage WHERE Percentages between 61 and 80
	Union All
	Select '81 - 100' as [Percentages], [State]
	FROM StatewisePercentage WHERE Percentages between 81 and 100

END
GO
