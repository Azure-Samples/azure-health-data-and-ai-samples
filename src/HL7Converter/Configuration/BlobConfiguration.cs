﻿namespace HL7Converter.Configuration
{
    public class BlobConfiguration
    {
        public string BlobConnectionString { get; set; }
        public string ValidatedContainer { get; set; }
        public string ConvertedContainer { get; set; }

        public string ConversionfailContainer { get; set; }
    }
}