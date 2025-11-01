using System.ComponentModel.DataAnnotations;

namespace PKL_API.Models
{
    public class ChatContact
    {
        [Key]
        public int id { get; set; }
        public required string id_chat { get; set; }
        public required string chat_name { get; set; }

        public ICollection<Classroom> Classrooms { get; set; }
        public ICollection<DefaultChat> DefaultChats { get; set; }
    }
}
