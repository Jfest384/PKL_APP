using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models
{
    public class WeeklyGuidance
    {
        [Key]
        public int Id { get; set; }
        [Column("Id_Student")]
        public int Studentid { get; set; }
        [Column("Id_Mentor")]
        public int? Mentorid { get; set; }
        public DateTime WeekStartDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Student Student { get; set; }
        public Mentor Mentor { get; set; }
    }
}
