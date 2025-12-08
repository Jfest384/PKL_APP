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
        [Column(TypeName = "decimal(15,12)")]
        public decimal? Lat { get; set; }

        [Column("long", TypeName = "decimal(15,12)")]
        public decimal? Long { get; set; }
    }

    public class CompanyEditDTO
    {
        [StringLength(100)]
        public required string name { get; set; }
        [MaxLength]
        public required string address { get; set; }
        [Column(TypeName = "decimal(15,12)")]
        public decimal? Lat { get; set; }
        [Column("long", TypeName = "decimal(15,12)")]
        public decimal? Long { get; set; }
    }
}
