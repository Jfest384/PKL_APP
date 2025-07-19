using System.ComponentModel.DataAnnotations;

namespace PKL_API.Models.DTO
{
    public class ReportDTO
    {
        [MaxLength]
        public required string content { get; set; }
    }
}
