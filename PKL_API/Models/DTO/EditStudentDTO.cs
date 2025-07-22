using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models.DTO
{
    public class EditStudentDTO
    {
        public bool? isPKL { get; set; }
        [Column("id_class")]
        public int? idClass { get; set; }
    }
}
