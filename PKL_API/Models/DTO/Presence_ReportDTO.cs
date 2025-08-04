using System.ComponentModel.DataAnnotations;

namespace PKL_API.Models.DTO
{
    public class Presence_ReportDTO
    {
        [MaxLength]
        public string? daily_report { get; set; }
        public IFormFile? MedicalCertificate { get; set; }
        public IFormFile? SickToCompany { get; set; }
        public IFormFile? SickToMentor { get; set; }
        public IFormFile? SickToWalas { get; set; }
        public IFormFile? Activity { get; set; }
    }
}
