using System.ComponentModel.DataAnnotations;

namespace PKL_API.Models
{
    public class PresenceFeedback
    {
        [Key]
        public int id { get; set; }

        [DataType(DataType.MultilineText)]
        public string? kajur { get; set; }

        [DataType(DataType.MultilineText)]
        public string? walas { get; set; }

        [DataType(DataType.MultilineText)]
        public string? mentor { get; set; }

        public ICollection<Presence>? Presences { get; set; }
    }
}
