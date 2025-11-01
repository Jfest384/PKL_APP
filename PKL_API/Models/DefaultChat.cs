using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models
{
    public class DefaultChat
    {
        [Key]
        public int id { get; set; }
        [Column("id_service")]
        public required int ChatServiceid { get; set; }
        [Column("id_contact")]
        public required int ChatContactid { get; set; }

        public ChatService ChatService { get; set; }
        public ChatContact ChatContact { get; set; }
    }
}
