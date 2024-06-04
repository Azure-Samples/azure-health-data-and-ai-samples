using Blazored.Modal.Services;
using FhirBlaze.Model;
using FhirBlaze.Shared;
using FhirBlaze.SharedComponents;
using FhirBlaze.SharedComponents.Services;
using Hl7.Fhir.ElementModel.Types;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DateTime = System.DateTime;
using Task = System.Threading.Tasks.Task;

namespace FhirBlaze.Pages
{
	public partial class MatchPatient
	{
		[Inject]
		IEMPIConnectorService eMPIConnectorService { get; set; }
		[Inject]
		IFhirService fhirService { get; set; }

        [Inject]
        private IConfiguration _config { get; set; }

        [CascadingParameter] public IModalService Modal { get; set; } = default!;

        private string jsonResource = string.Empty;
		private string matchPatientResult = string.Empty;
		private string matchPaitentJson = string.Empty;
		private string patientWithHighScore = string.Empty;
		private bool matchPatientResultFound = false;
		private string msgNewPatientAdded = string.Empty;
		private string msgPatientUpdated = string.Empty;
		private string msgPatientDeleted = string.Empty;
		private string selectedPatientResource = string.Empty;
		private bool isLoading = false;
        

        [CascadingParameter]
		private Task<AuthenticationState> authenticationStateTask { get; set; }

		private LoadPatientModel loadPatientModel = new LoadPatientModel();

		private UpdatePatientModel updatePatientModel = new UpdatePatientModel();

		private IQueryable<LoadPatientModel> patients;

        private async Task SearchAndLoadPatient()
		{
			msgPatientDeleted = string.Empty;
			var internalAuth = Convert.ToBoolean(_config["InternalAuth"]);
			StateHasChanged();
			isLoading = true;
			bool isFound = false;
			try
			{
				if (!await CheckAuthentication() && !internalAuth)
				{
					ShowLoginModal();
				}
				else
				{
					string[] fileNames = new string[] { "SamplePatient1.json", "SamplePatient2.json", "SamplePatient3.json", "SamplePatient4.json", "SamplePatient5.json" };

					foreach (string fileName in fileNames)
					{
						string filePath = $"sample-patient-data/{fileName}";

						var cacheBuster = DateTime.Now.Ticks.ToString();
						var urlWithCacheBuster = $"{filePath}?v={cacheBuster}";
						var jsonData = await Http.GetStringAsync(urlWithCacheBuster);
						var jObject = JObject.Parse(jsonData);
						JArray identifierArray = (JArray)jObject["identifier"];
						string storedIdentifier = string.Empty;
						bool isIdentifierPresent = true;
						bool isLastNamePresent = true;

						if (!identifierArray.IsNullOrEmpty())
						{
							foreach (var identifier in identifierArray)
							{
								storedIdentifier = (string)identifier["value"];
								if (!storedIdentifier.Equals(loadPatientModel.Identifier)) isIdentifierPresent = false;
							}
						}
						if (!isIdentifierPresent) continue;

						JArray nameArray = (JArray)jObject["name"];
						string storedLastName = string.Empty;
						string storedFirstName = string.Empty;
						bool firstNameFound = false;

						if (!nameArray.IsNullOrEmpty())
						{
							foreach (var name in nameArray)
							{
								storedLastName = (string)name["family"];
								if (!storedLastName.ToLower().Equals(loadPatientModel.LastName.ToLower())) isLastNamePresent = false;
								if (!isLastNamePresent) continue;
								if (name["given"].ToList().Count > 0)
								{
									storedFirstName = (string)name["given"].FirstOrDefault(name => loadPatientModel.FirstName.ToLower() == ((string)name).ToLower());
									if (storedFirstName != null)
									{
										firstNameFound = true;
										break;
									}
								}
							}
							if (firstNameFound == false)
							{
								break;
							}
						}
						if (!isLastNamePresent) continue;

						string storedGender = (string)jObject["gender"];
						string storedBirthDate = (string)jObject["birthDate"];
						string formattedBirthDate = loadPatientModel.BirthDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
						if (storedGender.Equals(loadPatientModel.Gender)
							&& storedBirthDate.Equals(formattedBirthDate))
						{
							jsonResource = jsonData.ToString();
							isFound = true;
							break;
						}
						StateHasChanged();
					}
					if (isFound == false)
					{
						jsonResource = "No Patient Found";
					}
				}
			}
			catch
			{
				isLoading = false;
				StateHasChanged();
				throw;
			}
			isLoading = false;
			StateHasChanged();

		}

		private void ClearAllFields()
		{
			jsonResource = string.Empty;
			matchPatientResult = string.Empty;
			matchPaitentJson = string.Empty;
			patientWithHighScore = string.Empty;
			matchPatientResultFound = false;
			msgNewPatientAdded = string.Empty;
			msgPatientUpdated = string.Empty;
			loadPatientModel = new LoadPatientModel();
			updatePatientModel = new UpdatePatientModel();
			patients = Enumerable.Empty<LoadPatientModel>().AsQueryable();
		}

		public async void GetAllPatientMatch()
		{
			matchPaitentJson = string.Empty;
			patientWithHighScore = string.Empty;
			msgNewPatientAdded = string.Empty;
			msgPatientUpdated = string.Empty;
			msgPatientDeleted = string.Empty;
			isLoading = true;
            var internalAuth = Convert.ToBoolean(_config["InternalAuth"]);

            try
			{
				if (!await CheckAuthentication() && !internalAuth)
				{
					ShowLoginModal();
				}
				else
				if (!string.IsNullOrEmpty(jsonResource))
				{
					string requestBody = PrepareMatchOperationRequestBody(jsonResource);

					var matchedPatients = await eMPIConnectorService.GetPatientMatchAsync(requestBody);
					if (matchedPatients.IsSuccessStatusCode)
					{
						var patientJsonResponse = matchedPatients.Content.ReadAsStringAsync().Result;

						if (!string.IsNullOrEmpty(patientJsonResponse))
						{
							object parsedJson = JsonConvert.DeserializeObject(patientJsonResponse);
							matchPatientResult = JsonConvert.SerializeObject(parsedJson, Formatting.Indented);

							JObject json = JObject.Parse(matchPatientResult);

							JArray entryArray = (JArray)json["entry"];

							if (entryArray.Count > 0)
							{
								matchPaitentJson = CleanMatchPatientResult(matchPatientResult);
								// patientWithHighScore = GetHighestMatchedPatient(matchPatientResult);
								// PrePopulateUpdatePatientForm(patientWithHighScore);
								PopulateMatchedPatients(entryArray);
								matchPatientResultFound = true;
							}
							else
							{
								matchPaitentJson = "No match found for the patient. You can add this patient or select another patient for update.";
								matchPatientResultFound = false;
							}
						}
					}
					else
					{
						matchPaitentJson = "Error occured while performing operation. Please try again after some time.";
						matchPatientResultFound = false;
					}
				}
				else
				{
					jsonResource = "Select valid patient resource.";
				}
				StateHasChanged();
			}
			catch (Exception)
			{
				isLoading = false;
				StateHasChanged();
				throw;
			}
			isLoading = false;
			StateHasChanged();
		}

		private void PrePopulateUpdatePatientForm(string matchPaitentJson)
		{
			try
			{
				JObject json = JObject.Parse(matchPaitentJson);
				JArray nameArray = (JArray)json["name"];
				if (!nameArray.IsNullOrEmpty())
				{
					updatePatientModel.LastName = (string)nameArray[0]["family"];
				}
				var BirthDate = (string)json["birthDate"];
				if (BirthDate != null)
				{
					updatePatientModel.BirthDate = DateTime.Parse(BirthDate);
				}
				JArray telecomArray = (JArray)json["telecom"];
				if (!telecomArray.IsNullOrEmpty())
				{
					foreach (var telecom in telecomArray)
					{
						var system = (string)telecom["system"];
						if (system != null && system == "phone")
						{
							var value = (string)telecom["value"];
							if (value != null)
							{
								updatePatientModel.PhoneNumber = value;
							}
						}
					}
				}

			}
			catch
			{
				throw;
			}
		}

		private LoadPatientModel PopulatePatientModel(JObject entry)
		{
			try
			{
				var patientModel = new LoadPatientModel();
				var resource = entry["resource"];
				JArray nameArray = (JArray)resource["name"];
				if (!nameArray.IsNullOrEmpty())
				{
					foreach (var name in nameArray)
					{
						var lastName = (string)name["family"];
						var firstNames = name["given"];
						var firstName = (string)firstNames.FirstOrDefault();
						patientModel.FirstName = firstName ?? string.Empty;
						patientModel.LastName = lastName ?? string.Empty;
					}
				}
				var birthDate = (string)resource["birthDate"];
				if (birthDate != null)
				{
					patientModel.BirthDate = DateTime.Parse(birthDate);
				}
				var gender = (string)resource["gender"];
				patientModel.Gender = gender;
				var identifierArray = resource["identifier"];
				var identifier = identifierArray.FirstOrDefault();
				patientModel.Identifier = (string)identifier["value"];
				patientModel.FhirId = (string)resource["id"];

				return patientModel;
			}
			catch
			{
				throw;
			}
		}

		private void PopulateSelectedPatientModel(LoadPatientModel patient)
		{
			try
			{
				selectedPatientResource = GetSelectedPatient(patient.FhirId);
				if (selectedPatientResource != null)
				{
					PrePopulateUpdatePatientForm(selectedPatientResource);
				}
				matchPatientResultFound = true;
				StateHasChanged();
			}
			catch
			{
				throw;
			}
		}

		private void PopulateMatchedPatients(JArray entryArray)
		{
			try
			{
				IQueryable<LoadPatientModel> patientsList = Enumerable.Empty<LoadPatientModel>().AsQueryable();
				foreach (JObject entry in entryArray)
				{
					var patient = PopulatePatientModel(entry);
					patientsList = patientsList.Append(patient);
				}
				patients = patientsList.AsQueryable();
			}
			catch
			{
				throw;
			}
		}

		public async void CreatePatient()
		{
			StateHasChanged();
			isLoading = true;
            var internalAuth = Convert.ToBoolean(_config["InternalAuth"]);

            try
			{
				if (!await CheckAuthentication() && !internalAuth)
                {
					ShowLoginModal();
				}
                else if (!string.IsNullOrEmpty(jsonResource))
				{
                    var fhirResponse = await fhirService.CreatePatientsAsync(jsonResource);
					if (fhirResponse.IsSuccessStatusCode)
					{
						msgNewPatientAdded = "New patient added to FHIR and EMPI successfully!!!";
					}
					else
					{
						msgNewPatientAdded = "Error occured while adding patient to FHIR. Please try after some time.";
					}
				}
				else
				{
					jsonResource = "Please select valid patient";
					msgNewPatientAdded = "Please select valid patient.";
				}

				StateHasChanged();
			}
			catch (Exception)
			{
				isLoading = false;
				StateHasChanged();
				throw;
			}
			isLoading = false;
			StateHasChanged();
		}
		public async void UpdatePatient()
		{
			isLoading = true;
			StateHasChanged();
			try
			{
				if (!await CheckAuthentication())
				{
					ShowLoginModal();
				}
				else
				if (!string.IsNullOrEmpty(selectedPatientResource))
				{
					UpdatePatientJson();
					JObject jsonObject = JObject.Parse(selectedPatientResource);
					string patientId = (string)jsonObject["id"];

					var fhirResponse = await fhirService.UpdatePatientAsync(patientId, selectedPatientResource);
					if (fhirResponse.IsSuccessStatusCode)
					{
						msgPatientUpdated = "Patient updated in FHIR and EMPI successfully!!!";
					}
					else
					{
						msgPatientUpdated = "Error occured while updating patient in FHIR. Please try after some time.";
					}
				}
				else
				{
					selectedPatientResource = "Please select valid patient.";
				}

				StateHasChanged();
			}
			catch (Exception)
			{
				isLoading = false;
				StateHasChanged();
				throw;
			}
			isLoading = false;
			StateHasChanged();
		}
		public async void DeletePatient()
		{
			StateHasChanged();
			isLoading = true;
			try
			{
				if (!await CheckAuthentication())
				{
					ShowLoginModal();
				}
				else if (!string.IsNullOrEmpty(selectedPatientResource))
				{
					JObject jsonObject = JObject.Parse(selectedPatientResource);
					string patientId = (string)jsonObject["id"];
					if (await DeleteConfirmationModal())
					{
						var fhirResponse = await fhirService.DeletePatientAsync(patientId);

						if (fhirResponse.IsSuccessStatusCode)
						{
							msgPatientDeleted = "Patient deleted in FHIR and EMPI successfully!!!";
							ClearAllFields();
							msgPatientUpdated = string.Empty;
						}
						else
						{
							msgPatientDeleted = "Error occured while deleting patient in FHIR. Please try after some time.";
						}
					}
				}
				else
				{
					msgPatientDeleted = "Please select valid patient.";
				}
				StateHasChanged();
			}
			catch (Exception)
			{
				isLoading = false;
				StateHasChanged();
				throw;
			}
			isLoading = false;
			StateHasChanged();
		}
		private string GetHighestMatchedPatient(string patientBundle)
		{
			JObject json = JObject.Parse(patientBundle);
			string resourceJson = string.Empty;

			JArray entryArray = (JArray)json["entry"];

			if (entryArray.Count > 0)
			{
				JObject firstEntry = (JObject)entryArray[0];
				JObject resource = (JObject)firstEntry["resource"];

				resourceJson = resource.ToString();
			}

			return resourceJson;
		}

		private string GetSelectedPatient(string id)
		{
			JObject json = JObject.Parse(matchPatientResult);
			string resourceJson = string.Empty;

			JArray entryArray = (JArray)json["entry"];

			if (entryArray.Count > 0)
			{
				foreach (JObject entry in entryArray)
				{
					JObject resource = (JObject)entry["resource"];
					if ((string)resource["id"] == id)
					{
						resourceJson = resource.ToString();
						break;
					}
				}
			}
			return resourceJson;
		}

		private string PrepareMatchOperationRequestBody(string jsonResource)
		{
			// Parse the input JSON
			JObject objResource = JObject.Parse(jsonResource);

			// Extract values
			string gender = (string)objResource["gender"];
			string birthDate = (string)objResource["birthDate"];

			JArray identifiers = (JArray)objResource["identifier"];
			JArray identifierList = new JArray();
			if (identifiers != null)
			{
				foreach (JToken identifier in identifiers)
				{
					identifierList.Add(new JObject(
					new JProperty("system", (string)identifier["system"]),
					new JProperty("value", (string)identifier["value"])
				));
				}
			}

			JArray nameArray = (JArray)objResource["name"];
			JArray nameList = new JArray();
			if (nameArray != null)
			{
				foreach (JToken name in nameArray)
				{
					string family = (string)name["family"];
					JArray givenArray = (JArray)name["given"];
					JArray givenList = new JArray();
					if (givenArray != null)
					{
						foreach (string givenName in givenArray)
						{
							givenList.Add(givenName);
						}
					}
					nameList.Add(new JObject(
						new JProperty("family", family),
						new JProperty("given", givenList)
					));
				}
			}

			// Create the new JSON structure
			JObject requestObject = new JObject(
				new JProperty("resourceType", "Parameters"),
				new JProperty("id", Guid.NewGuid().ToString()),
				new JProperty("parameter", new JArray(
					new JObject(
						new JProperty("name", "resource"),
						new JProperty("resource", new JObject(
							new JProperty("resourceType", "Patient"),
							new JProperty("identifier", identifierList),
							new JProperty("name", nameList),
							new JProperty("gender", gender),
							new JProperty("birthDate", birthDate)
						))
					),
					new JObject(
						new JProperty("name", "count"),
						new JProperty("valueInteger", "10")
					),
					new JObject(
						new JProperty("name", "onlyCertainMatches"),
						new JProperty("valueBoolean", "true")
					)
				))
			);

			return requestObject.ToString();
		}
		private string CleanMatchPatientResult(string matchPatientJsonString)
		{
			var json = JObject.Parse(matchPatientJsonString);
			JArray entryArray = (JArray)json["entry"];
			if (!entryArray.IsNullOrEmpty())
			{
				foreach (var entry in entryArray)
				{
					var fullUrl = (string)entry["fullUrl"];
					if (fullUrl != null) entry["fullUrl"].Parent.Remove();
					JObject resource = (JObject)entry["resource"];
					if (resource.ContainsKey("meta")) resource["meta"].Parent.Remove();
					if (resource.ContainsKey("extension")) resource["extension"].Parent.Remove();
					if (resource.ContainsKey("text")) resource["text"].Parent.Remove();
				}
			}

			return json.ToString();
		}
		private async Task<bool> DeleteConfirmationModal()
		{
			try
			{
				var messageForm = Modal.Show<Confirm>("Delete Patient");
				var result = await messageForm.Result;
				Console.WriteLine(result.Data.ToString());
				if (result.Data.ToString() == "Ok")
				{
					return true;
				};
			}
			catch (Exception ex)
			{
				Console.WriteLine("Exception");
				Console.WriteLine(ex.Message);
			}
			return false;
		}

		private void ShowLoginModal()
		{
			try
			{
				Modal.Show<LoginModal>("To proceed with this action, please log in to your account.");
			}
			catch (Exception)
			{
				throw;
			}
		}
		private async Task<bool> CheckAuthentication()
		{
			var authenticationState = await authenticationStateTask;

			return authenticationState.User.Identity.IsAuthenticated;
		}
		private void UpdatePatientJson()
		{
			try
			{
				JObject patientJson = JObject.Parse(selectedPatientResource);

				if (patientJson.ContainsKey("birthDate"))
					patientJson["birthDate"] = updatePatientModel.BirthDate.ToString("yyyy-MM-dd");

				JArray telecomArray = (JArray)patientJson["telecom"];
				if (!telecomArray.IsNullOrEmpty())
				{
					foreach (var telecom in telecomArray)
					{
						var system = (string)telecom["system"];
						if (system != null && system == "phone")
						{
							var value = (string)telecom["value"];
							if (value != null)
							{
								telecom["value"] = updatePatientModel.PhoneNumber;
							}
						}
					}
				}
				JArray nameArray = (JArray)patientJson["name"];
				if (!nameArray.IsNullOrEmpty())
				{
					foreach (var name in nameArray)
					{
						var familyName = (string)name["family"];
						if (familyName != null)
							name["family"] = updatePatientModel.LastName;
						var fullName = (string)name["text"];
						if (fullName != null)
						{
							var updatedFullName = fullName.Replace(familyName, updatePatientModel.LastName);
							name["text"] = updatedFullName;
						}
					}
				}

				selectedPatientResource = patientJson.ToString();
			}
			catch
			{
				throw;
			}

		}

	}

}

