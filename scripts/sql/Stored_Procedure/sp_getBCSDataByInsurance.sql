USE
[fhirdb]
GO
IF EXISTS(SELECT 1 FROM sys.procedures WHERE Name = 'sp_getBCSDataByInsurance')
BEGIN 
	DROP PROCEDURE [dbo].[sp_getBCSDataByInsurance]
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
CREATE PROCEDURE sp_getBCSDataByInsurance
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
ClaimData (claimId, patientId, insurance)
AS 
(	
	SELECT 
		id, 
		RIGHT([patient.reference], LEN([patient.reference]) - 8) AS PatientId,
		JSON_VALUE(claim.[insurance],'$[0].coverage.display') AS insurance
	FROM fhir.Claim claim
),
BCSNumerator (PatientID, Insurance)
AS
(
	SELECT DISTINCT
		Patient.id AS PatientId,
		C.insurance
	FROM [fhir].[Patient] AS Patient
	INNER JOIN MammogramProcedure ON
	Patient.id = SUBSTRING(MammogramProcedure.PatientID, 9, 1000)
    INNER JOIN BreastCancerScreeningEligiblePatients ON
    BreastCancerScreeningEligiblePatients.PatientId = Patient.Id 
    AND
    DATEDIFF(MONTH, CONVERT (DATETIMEOFFSET,MammogramProcedure.performedperiod,111), @vMeasurementPeriodEndDate) < = 48
	INNER JOIN ClaimData C ON C.patientId = Patient.id
)

	SELECT 
		Insurance, 
		CONVERT(DECIMAL(10,2),COUNT(*) * 100.0 / SUM(COUNT(*)) OVER()) AS InsurancePercentage
	FROM BCSNumerator
	GROUP BY Insurance
END
GO
