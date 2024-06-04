
using Microsoft.Graph;
using System;
using System.ComponentModel.DataAnnotations;

namespace FhirBlaze.Model
{
	public class LoadPatientModel
	{
		public LoadPatientModel() { }
		public LoadPatientModel(string firstName, string lastName, string gender, DateTime birthDate, string identifier, string fhirId)
		{
			FirstName = firstName;
			LastName = lastName;
			Gender = gender;
			BirthDate = birthDate;
			Identifier = identifier;
			FhirId = fhirId;
		}
	
		[Required(ErrorMessage = "FirstName is required Field")]
        public string FirstName { get; set; }

		[Required(ErrorMessage = "LastName is required Field")]
		public string LastName { get; set; }

		[Required(ErrorMessage = "Gender is required Field")]
		public string Gender { get; set; }

		[Required(ErrorMessage = "BirthDate is required Field")]
		public DateTime BirthDate { get; set; } = DateTime.Now;

		[Required(ErrorMessage = "Identifier is required Field")]
		public string Identifier { get; set; }

		public string FhirId { get; set; }

	}
}
