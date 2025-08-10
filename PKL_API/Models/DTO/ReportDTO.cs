using System.ComponentModel.DataAnnotations;

namespace PKL_API.Models.DTO
{
    public class ReportDTO
    {
        [MaxLength]
        public required string description { get; set; }

        public IFormFile? GuidancePhoto { get; set; }
        public IFormFile? ReportFile { get; set; }
    }
}
