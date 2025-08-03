namespace PKL_API.Models.DTO
{
    public class Presence_ReportDTO
    {
        public IFormFile? DailyReport { get; set; }
        public IFormFile? MedicalCertificate { get; set; }
        public IFormFile? SickToCompany { get; set; }
        public IFormFile? SickToMentor { get; set; }
        public IFormFile? SickToWalas { get; set; }
        public IFormFile? Activity { get; set; }
    }
}
