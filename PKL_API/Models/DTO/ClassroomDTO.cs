using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models.DTO
{
    public class ClassroomDTO
    {
        [StringLength(50)]
        public required string name { get; set; }
        public required int total_students { get; set; }
        [Column("id_walas")]
        public required int WaliKelasid { get; set; }
        public required int year { get; set; }
        [StringLength(200)]
        public required string description { get; set; }
    }
}
