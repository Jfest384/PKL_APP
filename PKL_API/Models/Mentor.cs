using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models
{
    public class Mentor
    {
        [Key]
        public int id { get; set; }
        [Column("id_user")]
        public int Userid { get; set; }
        [Column("id_teacher")]
        public int Teacherid { get; set; }

        public required User User { get; set; }
        public required Teacher Teacher { get; set; }

        public required ICollection<Student> Students { get; set; }
        public required ICollection<Presence> Presences { get; set; }
        public required ICollection<Report> Reports { get; set; }
    }
}
