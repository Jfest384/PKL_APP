using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models.DTO
{
    public class PrecenseDTO
    {
        [Column("id_precense")]
        public int PresenceTypeid { get; set; }

        [Column(TypeName = "decimal(10,7)")]
        public decimal? Lat { get; set; }

        [Column("long", TypeName = "decimal(10,7)")]
        public decimal? Long { get; set; }

        public IFormFile? FullBodyPhoto { get; set; }
        public IFormFile? Treatment { get; set; }
        public IFormFile? PermitToCompany { get; set; }
        public IFormFile? PermitToMentor { get; set; }
        public IFormFile? PermitToWalas { get; set; }
        public IFormFile? HolidayFromCompany { get; set; }
        public IFormFile? WFHFromCompany { get; set; }
    }
}
