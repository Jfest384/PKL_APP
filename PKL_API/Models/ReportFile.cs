namespace PKL_API.Models
{
    public class ReportFile
    {
        public Guid id { get; set; }
        public required Byte[] files { get; set; }
        public required string extension { get; set; }

        //public ICollection<Report> Reports { get; set; }
    }
}
