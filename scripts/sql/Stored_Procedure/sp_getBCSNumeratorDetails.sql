USE
[fhirdb]
GO
IF EXISTS(SELECT 1 FROM sys.procedures WHERE Name = 'sp_getBCSNumeratorDetails')
BEGIN 
	DROP PROCEDURE [dbo].[sp_getBCSNumeratorDetails]
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
CREATE PROCEDURE sp_getBCSNumeratorDetails
(
    @vMeasurementPeriodStartDate DATE,
	@vMeasurementPeriodEndDate DATE
)
AS
BEGIN

WITH 
EncounterTypeExpanded (id, code)
AS
(
    SELECT
        id,
        JSON_VALUE(EncounterType.[type.coding],'$[0].code')
    FROM [fhir].[EncounterType] EncounterType     
),
BreastCancerScreeningEligiblePatients (PatientId, EncounterDate)
AS
(
    SELECT
        Patient.id AS PatientId,
        Encounter.[period.end] AS EncounterDate
    FROM
        [fhir].[Encounter] AS Encounter
    -- join with EncounterTypeExpanded for encounter codes
    INNER JOIN EncounterTypeExpanded
		ON Encounter.Id = EncounterTypeExpanded.id
    AND EncounterTypeExpanded.code IN ('86013001','185345009','3391000175108', '444971000124105', '439708006', '90526000')
    AND Encounter.[period.end] BETWEEN @vMeasurementPeriodStartDate AND @vMeasurementPeriodEndDate
    -- Join to the patient
    INNER JOIN [fhir].[Patient] as Patient         
        ON Patient.id = SUBSTRING([Encounter].[subject.reference], 9, 1000)
    WHERE
        Patient.gender = 'female'
        AND DATEDIFF(year, Patient.birthDate, @vMeasurementPeriodEndDate) >= 50
        AND DATEDIFF(year, Patient.birthDate, @vMeasurementPeriodEndDate) <= 70       
),
MammogramProcedure(id,PatientID,performedperiod)
AS
(
	SELECT 
		[pro].[id],
		[subject.reference] AS PatientID,
		[performed.period.end]
	FROM [fhir].[Procedure] AS [pro]
		CROSS APPLY openjson (pro.[code.coding]) WITH (
        [system]          VARCHAR(256)        '$.system',
        [code]            VARCHAR(256)        '$.code',
        [display]         VARCHAR(256)        '$.display'
		) proSystem
	WHERE proSystem.code IN ('241055006','24623002','71651007')   
),
BCSNumerator (Patient, [Family Name], [First Name], PatientBirthDate, PatientGender, PatientState, PatientAge, RaceCategory, EthnicityCategory)
AS
(
	SELECT DISTINCT
		Patient.id AS Patient,
		JSON_VALUE([name],'$[0].family') AS [Family Name],
		JSON_VALUE([name],'$[0].given[0]') AS [First Name],
		CAST(Patient.birthDate AS DATE) AS PatientBirthDate,
		Patient.gender AS PatientGender,
		JSON_VALUE(Patient.[address],'$[0].state') AS PatientState,
		DATEDIFF(year, Patient.birthDate, @vMeasurementPeriodEndDate) AS PatientAge,
		JSON_VALUE(Patient.[extension],'$[0].extension[0].valueCoding.display') RaceCategory,
		JSON_VALUE(Patient.[extension],'$[1].extension[0].valueCoding.display') EthnicityCategory
    FROM [fhir].[Patient] AS Patient
    INNER JOIN MammogramProcedure MP 
		ON Patient.id = SUBSTRING(MP.PatientID, 9, 1000)
    INNER JOIN BreastCancerScreeningEligiblePatients BEP 
		ON BEP.PatientId = Patient.Id 
	AND
	DATEDIFF(MONTH, CONVERT (DATETIMEOFFSET,MP.performedperiod,111), @vMeasurementPeriodEndDate) < = 48
)

	SELECT DISTINCT
		N.Patient,
		[Family Name],
		[First Name],
		PatientBirthDate,
		PatientGender,
		PatientState,
		PatientAge,
		CASE
			WHEN PatientAge BETWEEN 50 and 54 THEN '50 - 54'
			WHEN PatientAge BETWEEN 55 and 59 THEN '55 - 59'
			WHEN PatientAge BETWEEN 60 and 64 THEN '60 - 64'
			WHEN PatientAge BETWEEN 65 and 69 THEN '65 - 69'
			WHEN PatientAge BETWEEN 70 and 74 THEN '70 - 74'
		END
		AS [Age Range],
		RaceCategory,
		EthnicityCategory
	FROM BCSNumerator N 
END
GO
