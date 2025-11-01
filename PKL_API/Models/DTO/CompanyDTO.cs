using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models.DTO
{
    public class CompanyDTO
    {
        [StringLength(100)]
        public required string name { get; set; }
        [MaxLength]
        public required string address { get; set; }
        [Column(TypeName = "decimal(10,7)")]
        public decimal? Lat { get; set; }

        [Column("long", TypeName = "decimal(10,7)")]
        public decimal? Long { get; set; }
    }

    public class CompanyEditDTO
    {
        [StringLength(100)]
        public required string name { get; set; }
        [MaxLength]
        public required string address { get; set; }
        [Column(TypeName = "decimal(10,7)")]
        public decimal? Lat { get; set; }
        [Column("long", TypeName = "decimal(10,7)")]
        public decimal? Long { get; set; }
    }
}
