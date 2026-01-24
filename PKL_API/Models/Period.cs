using System.ComponentModel.DataAnnotations;

namespace PKL_API.Models
{
    public class Period
    {
        [Key]
        public int id { get; set; }

        [StringLength(100)]
        public required string period { get; set; }

        public ICollection<StudentValidation>? StudentValidations { get; set; }
    }
}
