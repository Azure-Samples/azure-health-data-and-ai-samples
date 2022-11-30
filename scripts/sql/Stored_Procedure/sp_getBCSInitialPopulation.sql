/****** Object:  StoredProcedure [dbo].[sp_getFemalePatients]    Script Date: 11/8/2022 11:48:46 AM ******/
IF EXISTS(SELECT 1 FROM sys.procedures WHERE Name = 'sp_getBCSInitialPopulation')
BEGIN 
	DROP PROCEDURE [dbo].[sp_getBCSInitialPopulation]
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
CREATE PROCEDURE sp_getBCSInitialPopulation
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
)

    SELECT
        COUNT(DISTINCT Patient.id) AS DenominatorCount
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


END
GO
