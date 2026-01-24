using System.ComponentModel.DataAnnotations;

namespace PKL_API.Models
{
    public class PresenceSuspension
    {
        [Key]
        public int id { get; set; }
        public DateOnly start_date { get; set; }
        public DateOnly end_date { get; set; }
        public required string reason { get; set; }
        public DateTime created_at { get; set; }
    }
}
