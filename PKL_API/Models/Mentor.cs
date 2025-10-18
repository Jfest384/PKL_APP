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

        public User User { get; set; }
        public Teacher Teacher { get; set; }

        public ICollection<Student> Students { get; set; }
        public ICollection<Presence> Presences { get; set; }
        public ICollection<Report> Reports { get; set; }
    }
}
