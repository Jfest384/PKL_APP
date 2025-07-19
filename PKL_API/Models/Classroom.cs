using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models
{
    public class Classroom
    {
        [Key]
        public int id { get; set; }
        [StringLength(50)]
        public required string name { get; set; }
        [Column("id_walas")]
        public required int WaliKelasid { get; set; }
        public int? total_students { get; set; }
        [StringLength(255)]
        public string? description { get; set; }
        public int? year { get; set; }

        public ICollection<Student> Students { get; set; }
        public ICollection<Presence> Presences { get; set; }
        public ICollection<Report> Reports { get; set; }
        public WaliKelas WaliKelas { get; set; }
    }
}
