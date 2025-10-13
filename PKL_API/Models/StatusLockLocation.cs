using System.ComponentModel.DataAnnotations;

namespace PKL_API.Models
{
    public class StatusLockLocation
    {
        [Key]
        public int id { get; set; }
        public bool status { get; set; }
        public DateTime? updateAt { get; set; }
    }
}
