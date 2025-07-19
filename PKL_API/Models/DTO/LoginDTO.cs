using System.ComponentModel.DataAnnotations;

namespace PKL_API.Models.DTO
{
    public class LoginDTO
    {
        [StringLength(100)]
        public string username { get; set; }
        [StringLength(100)]
        public string password { get; set; }
    }
}
