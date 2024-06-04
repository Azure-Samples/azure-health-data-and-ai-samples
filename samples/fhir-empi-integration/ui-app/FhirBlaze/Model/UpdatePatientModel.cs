using System;
using System.ComponentModel.DataAnnotations;

namespace FhirBlaze.Model
{
	public class UpdatePatientModel
	{
        [Required(ErrorMessage = "Lastname is a required field")]
        public string LastName { get; set; }
		[Required(ErrorMessage = "PhoneNumber is a required field")]
		public string PhoneNumber { get; set; }
		[Required(ErrorMessage = "BirthDate is a required field")]
		public DateTime BirthDate { get; set; } = DateTime.Now;
    }
}
