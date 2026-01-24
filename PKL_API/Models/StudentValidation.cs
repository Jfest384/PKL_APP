using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

        [Column("id_period")]
        public int? Periodid { get; set; }
        public DateTime? update_daily { get; set; }
        public DateTime? update_weekly { get; set; }
        public DateOnly? start_pkl { get; set; }

        public Period? Period { get; set; }
        public ICollection<Student>? Students { get; set; }
    }
}
