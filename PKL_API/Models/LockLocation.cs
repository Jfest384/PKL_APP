using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models
{
    public class LockLocation
    {
        [Key]
        public int id { get; set; }
        [Column("id_user")]
        public int Userid { get; set; }
        [Column("id_student")]
        public int Studentid { get; set; }
        [Column(TypeName = "decimal(10,7)")]
        public decimal? lat { get; set; }
        [Column("long", TypeName = "decimal(10,7)")]
        public decimal? longitude { get; set; }

        public User User { get; set; }
        public Student Student { get; set; }
    }
}
