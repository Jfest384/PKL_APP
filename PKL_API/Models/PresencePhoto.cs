using System.ComponentModel.DataAnnotations;

namespace PKL_API.Models
{
    public class PresencePhoto
    {
        [Key]
        public Guid id { get; set; }
        public required Byte[] photo { get; set; }
        public required string extension { get; set; }

    }
}
