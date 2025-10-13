using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models.DTO
{
    public class StudentDTO
    {
        [StringLength(50)]
        public required string nis { get; set; }
        public required string name { get; set; }
        [Column("id_class")]
        public required int Classid { get; set; }
    }
}
