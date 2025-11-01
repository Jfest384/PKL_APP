using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models
{
    public class ChatService
    {
        [Key]
        public int id { get; set; }
        public required string service_name { get; set; }
        [Column("id_template")]
        public required int MessageTemplateId { get; set; }

        public MessageTemplate MessageTemplate { get; set; }
        public required ICollection<DefaultChat> DefaultChats { get; set; }
    }
}
