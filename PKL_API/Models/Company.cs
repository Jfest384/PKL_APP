using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models
{
    public class Company
    {
        [Key]
        public int id { get; set; }
        [StringLength(100)]
        public required string name { get; set; }
        [DataType(DataType.MultilineText)]
        public string? address { get; set; }
        [StringLength(20)]
        public string? phone { get; set; }

        [Column(TypeName = "decimal(10,7)")]
        public decimal? lat { get; set; }

        [Column("long", TypeName = "decimal(10,7)")]
        public decimal? longitude { get; set; }

        public ICollection<Student>? Students { get; set; }
    }
}
