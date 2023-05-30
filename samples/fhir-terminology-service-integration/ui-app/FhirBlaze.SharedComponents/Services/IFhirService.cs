﻿using Hl7.Fhir.Model;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FhirBlaze.SharedComponents.Services
{
    public interface IFhirService
    {
        Task<TResource> GetResourceByIdAsync<TResource>(string resourceId) where TResource : Resource, new();
        Task<List<TResource>> ExecuteFhirQueryAsync<TResource>(string queryStr) where TResource : Resource, new();

        #region Patient
        Task<Patient> CreatePatientsAsync(Patient patient);
        Task<IList<Patient>> GetPatientsAsync();
        Task<Patient> UpdatePatientAsync(string patientId, Patient patient);
        Task<int> GetPatientCountAsync();
        Task<IList<Patient>> SearchPatient(Patient patient);

        Task<IList<Observation>> GetPatientObservations(string patientId);

        Task<IList<Observation>> GetPatientObservations(string firstName, string lastName);

        Task<string> Translate(string code);
        #endregion

        #region Questionnaire
        Task<QuestionnaireResponse> SaveQuestionnaireResponseAsync(QuestionnaireResponse qResponse);
        Task<IList<Questionnaire>> GetQuestionnairesAsync();
        Task<Questionnaire> GetQuestionnaireByIdAsync(string id);
        Task<Questionnaire> CreateQuestionnaireAsync(Questionnaire questionnaire);
        Task<QuestionnaireResponse> GetQuestionnaireResponseByIdAsync(string id);
        Task<IList<QuestionnaireResponse>> GetQuestionnaireResponsesByQuestionnaireIdAsync(string questionnaireId);
        Task<Questionnaire> UpdateQuestionnaireAsync(Questionnaire questionnaire);

        Task<IList<Questionnaire>> SearchQuestionnaire(string title);
        #endregion

        #region Practitioners
        Task<IList<Practitioner>> GetPractitionersAsync();

        Task<int> GetPractitionerCountAsync();

        Task<IList<Practitioner>> SearchPractitioner(IDictionary<string, string> searchParameters);

        Task<Practitioner> CreatePractitionersAsync(Practitioner practitioner);

        Task<Practitioner> UpdatePractitionerAsync(string practitionerId, Practitioner practitioner);
        #endregion

        #region Observation

        #endregion
    }
}