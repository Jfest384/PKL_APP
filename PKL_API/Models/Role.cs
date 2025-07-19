using System.ComponentModel.DataAnnotations;

namespace PKL_API.Models
{
    public class Role
    {
        [Key]
        public int id { get; set; }
        [StringLength(50)]
        public required string name { get; set; }

        public required ICollection<UserRole> UserRoles { get; set; }
    }
}
