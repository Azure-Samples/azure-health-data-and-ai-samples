USE [fhirdb]
GO
IF EXISTS(SELECT 1 FROM sys.procedures WHERE Name = 'sp_getBCSComplianceDetails')
BEGIN 
	DROP PROCEDURE [dbo].[sp_getBCSComplianceDetails]
END
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:      <>
-- Create Date: <Create Date, , >
-- Description: <Description, , >
-- =============================================
CREATE PROCEDURE sp_getBCSComplianceDetails
(
    @vMeasurementPeriodStartDate DATE,
	@vMeasurementPeriodEndDate DATE
)
AS
BEGIN

WITH
EncounterMeasure (Id, PatientId)
AS
(
    SELECT
        id,
		SUBSTRING([Encounter].[subject.reference], 9, 1000)
    FROM
        [fhir].[Encounter] AS Encounter
    WHERE JSON_VALUE([type],'$[0].coding[0].code') IN ('86013001','185345009','3391000175108', '444971000124105', '439708006', '90526000') 
		AND CAST(Encounter.[period.end] AS DATE) BETWEEN @vMeasurementPeriodStartDate AND @vMeasurementPeriodEndDate
),
InitialPopulation(PatientId, Age, PatientCity, PatientState, RaceCategory, EthnicityCategory)
AS
(
	SELECT P.id,
	DATEDIFF(year, CAST(P.birthDate AS DATE), @vMeasurementPeriodEndDate),
	JSON_VALUE(P.[address],'$[0].city'),
	JSON_VALUE(P.[address],'$[0].state'),
	JSON_VALUE(P.[extension],'$[0].extension[0].valueCoding.display'),
	JSON_VALUE(P.[extension],'$[1].extension[0].valueCoding.display')
	FROM [fhir].[Patient] P
	INNER JOIN EncounterMeasure EM ON P.id = EM.PatientId
	WHERE
        P.gender = 'female'
        AND DATEDIFF(year, P.birthDate, @vMeasurementPeriodEndDate) >= 50
        AND DATEDIFF(year, P.birthDate, @vMeasurementPeriodEndDate) <= 70 
),
MammogramProcedure(Id, PatientID)
AS
(
	SELECT 
		[pro].[Id],
		SUBSTRING([subject.reference], 9, 1000) AS PatientID
	FROM [fhir].[Procedure] AS [pro]
		CROSS APPLY openjson (pro.[code.coding]) WITH (
        [system]          VARCHAR(256)        '$.system',
        [code]            VARCHAR(256)        '$.code',
        [display]         VARCHAR(256)        '$.display'
		) proSystem
	WHERE proSystem.code IN ('241055006','24623002','71651007')
	AND DATEDIFF(MONTH, CAST([performed.period.end] AS DATE), @vMeasurementPeriodEndDate) <= 48
),
Numerators (PatientId, IsNumerator)
AS
(
	SELECT DISTINCT P.PatientId, 1
	FROM InitialPopulation P 
	INNER JOIN MammogramProcedure  MP ON P.PatientId = MP.PatientID
),
Claims (ClaimId, PatientId, Payor, EncounterId)
AS
(
	SELECT
		claim.id, 
		RIGHT([patient.reference], LEN([patient.reference]) - 8) AS PatientId,
		JSON_VALUE(claim.[insurance],'$[0].coverage.display') as Insurance,
		SUBSTRING(JSON_VALUE(claim.item,'$[0].encounter[0].reference'), 11, 1000) AS EncounterId	
	FROM fhir.Claim claim
	INNER JOIN EncounterMeasure E ON SUBSTRING(JSON_VALUE(claim.item,'$[0].encounter[0].reference'), 11, 1000) = E.Id
),
Patients (PatientId, Age, PatientCity, PatientState, RaceCategory, EthnicityCategory, Numerator , Payor)
AS
(
	SELECT DISTINCT P.PatientId, Age, PatientCity, PatientState, RaceCategory, EthnicityCategory,
	CASE 
        WHEN N.IsNumerator = 1 THEN 1
        ELSE 0
    END AS Numerator,
	C.Payor
	FROM InitialPopulation P  
	LEFT JOIN Numerators N ON P.PatientId = N.PatientId
	LEFT JOIN Claims C ON P.PatientId = C.PatientId
)

	SELECT DISTINCT TotalPatients.* from Patients P
	CROSS APPLY (SELECT TOP 1 * FROM Patients WHERE Patients.PatientId = P.PatientId) TotalPatients

END
GO