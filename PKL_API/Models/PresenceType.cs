using System.ComponentModel.DataAnnotations;

namespace PKL_API.Models
{
    public class PresenceType
    {
        [Key]
        public int id { get; set; }
        [StringLength(50)]
        public required string name { get; set; }

        public required ICollection<Presence> PresenceList { get; set; }
    }
}
