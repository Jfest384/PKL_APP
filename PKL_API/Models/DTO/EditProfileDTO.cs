using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models.DTO
{
    public class EditProfileDTO
    {
        [StringLength(50)]
        public string? nis { get; set; }
        [StringLength(50)]
        public string? nip { get; set; }
        [StringLength(100)]
        public required string fullname { get; set; }
        [Column("id_class")]
        public int Classroomid { get; set; }
        [Column("id_company")]
        public int Companyid { get; set; }
        [StringLength(100)]
        public required string email { get; set; }
        public required bool gender { get; set; }

        //[StringLength(100)]
        //public string? password { get; set; }
    }
}
