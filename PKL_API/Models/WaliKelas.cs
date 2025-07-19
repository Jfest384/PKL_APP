using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models
{
    public class WaliKelas
    {
        [Key]
        public int id { get; set; }
        [Column("id_user")]
        public int Userid { get; set; }
        [Column("id_teacher")]
        public int Teacherid { get; set; }

        public required User User { get; set; }
        [ForeignKey(nameof(Teacherid))]
        public required Teacher Teacher { get; set; }
    }
}
