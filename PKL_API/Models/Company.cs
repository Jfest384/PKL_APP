using System.ComponentModel.DataAnnotations;

namespace PKL_API.Models
{
    public class Company
    {
        [Key]
        public int id { get; set; }
        [StringLength(100)]
        public required string name { get; set; }
        [DataType(DataType.MultilineText)]
        public string? address { get; set; }
        [StringLength(20)]
        public string? phone { get; set; }
        public required ICollection<Student> Students { get; set; }
    }
}
