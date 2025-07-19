using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models
{
    public class Department
    {
        [Key]
        public int id { get; set; }
        [StringLength(50)]
        public required string name { get; set; }
        [Column("id_teacher")]
        public int Teacherid { get; set; }
        [StringLength(100)]
        public required string teacher_name { get; set; }

        public required Teacher Teacher { get; set; }
    }
}
