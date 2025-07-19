using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models
{
    public class UserRole
    {
        [Key]
        public int id { get; set; }
        [Column("id_user")]
        public int Userid { get; set; }
        [Column("id_role")]
        public int RoleId { get; set; }

        public required User User { get; set; }
        public required Role Role { get; set; }
    }
}
