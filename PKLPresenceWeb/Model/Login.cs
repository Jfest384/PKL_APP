using System.ComponentModel.DataAnnotations;

namespace PKLPresenceWeb.Model
{
    public class LoginDTO
    {
        [StringLength(100)]
        public required string username { get; set; }

        [StringLength(100)]
        public required string password { get; set; }
    }
}
