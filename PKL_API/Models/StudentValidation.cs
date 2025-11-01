using System.ComponentModel.DataAnnotations;

namespace PKL_API.Models
{
    public class StudentValidation
    {
        [Key]
        public int id { get; set; }

        [StringLength(50)]
        public string? nis { get; set; }

        public bool isPKL { get; set; }
        public bool isLock { get; set; }
        public bool isPresence { get; set; }
        public bool isDailyReport { get; set; }
        public bool isReport { get; set; }

        public DateTime? update_daily { get; set; }
        public DateTime? update_weekly { get; set; }

        public ICollection<Student>? Students { get; set; }
    }
}
