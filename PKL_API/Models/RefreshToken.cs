using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models
{
    public class RefreshToken
    {
        [Key]
        public int id { get; set; }
        [Column("id_user")]
        public int UserId { get; set; }
        public string token { get; set; } = string.Empty;

        public DateTime expires { get; set; }
        public bool isRevoked { get; set; }
    }
}
