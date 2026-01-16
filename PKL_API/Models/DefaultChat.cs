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
        public required string ChatContactid { get; set; }
        public required string contact_name { get; set; }

        public ChatService ChatService { get; set; }
    }
}
