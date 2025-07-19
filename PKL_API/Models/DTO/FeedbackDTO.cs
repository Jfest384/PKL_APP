using System.ComponentModel.DataAnnotations;

namespace PKL_API.Models.DTO
{
    public class FeedbackDTO
    {
        [StringLength(1000)]
        public required string feedback { get; set; }
    }
}
