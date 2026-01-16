using System.ComponentModel.DataAnnotations.Schema;

namespace PKL_API.Models.DTO
{
    public class DefaultChatDTO
    {
        [Column("id_service")]
        public required int ChatServiceid { get; set; }
        [Column("id_contact")]
        public required List<string> ChatContactid { get; set; }
        public required List<string> ContactName { get; set; }
    }

    public class DefaultChatEditDTO
    {
        [Column("id_service")]
        public required int ChatServiceid { get; set; }
        [Column("id_contact")]
        public required List<string> ChatContactid { get; set; }
        public required List<string> ContactName { get; set; }
    }

    public class TestSendRequest
    {
        public string ChatId { get; set; } = string.Empty;
    }

}
