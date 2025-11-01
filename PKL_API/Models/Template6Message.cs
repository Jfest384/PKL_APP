namespace PKL_API.Models
{
    public class Template6Message
    {
        public string ClassName { get; set; } = string.Empty;
        public List<string> IdChat { get; set; } = new();
        public string Content { get; set; } = string.Empty;
    }

    public class DefaultChatResponse
    {
        public int Id { get; set; }
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public object Template { get; set; } = new object();
        public List<int> ContactId { get; set; } = new();
        public List<string> IdChat { get; set; } = new();
        public List<string> ChatName { get; set; } = new();
    }

}
