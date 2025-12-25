using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models
{
    public class Company
    {
        [Key]
        public int id { get; set; }
        [StringLength(100)]
        public required string name { get; set; }
        [DataType(DataType.MultilineText)]


        public ICollection<Student>? Students { get; set; }
        public ICollection<CompanyLocation>? CompanyLocations { get; set; }
    }
}
