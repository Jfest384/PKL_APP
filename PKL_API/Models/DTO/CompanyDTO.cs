using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models.DTO
{
    public class CompanyDTO
    {
        [StringLength(100)]
        public required string name { get; set; }
        [MaxLength]
        public string? address { get; set; }
        [Column(TypeName = "decimal(15,12)")]
        public decimal? Lat { get; set; }

        [Column("long", TypeName = "decimal(15,12)")]
        public decimal? Long { get; set; }
    }

    public class CompanyLocationDTO
    {
        public required int Companyid { get; set; }
        [StringLength(100)]
        public required string name { get; set; }
        [MaxLength]
        public string? address { get; set; }
        [Column(TypeName = "decimal(15,12)")]
        public decimal? Lat { get; set; }

        [Column("long", TypeName = "decimal(15,12)")]
        public decimal? Long { get; set; }
    }

    public class CompanyEditDTO
    {
        [StringLength(100)]
        public required string name { get; set; }
    }

    public class CompanyLocationEditDTO
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

    public class CompanyLocationStatusDTO
    {
        public int companyLocationId { get; set; }
        public int value { get; set; }
    }
}
