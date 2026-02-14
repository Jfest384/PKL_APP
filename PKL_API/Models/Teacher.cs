using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models
{
    public class Teacher
    {
        [Key]
        public int id { get; set; }
        [Column("id_user")]
        public int Userid { get; set; }
        [StringLength(50)]
        public required string nip { get; set; }

        public required User User { get; set; }
        public required ICollection<Mentor> Mentors { get; set; }
        public required ICollection<WaliKelas> WaliKelas { get; set; }
    }
}
