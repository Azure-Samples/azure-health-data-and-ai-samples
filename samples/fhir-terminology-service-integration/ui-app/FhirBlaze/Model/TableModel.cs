namespace FhirBlaze.Model
{
    public class LookUpTableModel
    {
        public string Code { get; set; }

        public string System { get; set; }

        public string Description { get; set; }

    }

    public class TranslateTableModel
    {
        public string SourceCode { get; set; }

        public string SourceSystem { get; set; }
        
        public string TargetCode { get; set; }

        public string TargetSystem { get; set; }

        public string TargetDescription { get; set; }

    }

}
