using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models
{
    public class CompanyLocation
    {
        [Key]
        public int id { get; set; }

        [Column("id_company")]
        public int Companyid { get; set; }

        [Column("location_name"), StringLength(100)]
        public required string LocationName { get; set; }
        
        [DataType(DataType.MultilineText)]
        public string? address { get; set; }
        
        [Column(TypeName = "decimal(15,12)")]
        public decimal? lat { get; set; }

        [Column("long", TypeName = "decimal(15,12)")]
        public decimal? longitude { get; set; }

        public int radius_meter { get; set; }
        public bool is_active { get; set; }

        public Company? Company { get; set; }
    }
}
