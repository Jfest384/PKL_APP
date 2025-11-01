namespace PKL_API.Models
{
    public class MessageTemplate
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Content { get; set; } = "";

        public ICollection<ChatService> ChatServices { get; set; }
    }
}
