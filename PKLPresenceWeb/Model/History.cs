namespace PKLPresenceWeb.Model
{
    public class PresenceHistoryResponse
    {
        public int page { get; set; }
        public int pageSize { get; set; }
        public int totalPages { get; set; }
        public int totalDays { get; set; }
        public List<PresenceHistoryItem> data { get; set; } = new();
    }

    public class PresenceHistoryItem
    {
        public string id_presence { get; set; }
        public string nis { get; set; }
        public string name { get; set; }
        public string classroom_name { get; set; }
        public string date { get; set; }
        public string time { get; set; }
        public string presence_type { get; set; }
        public string report { get; set; }
        public string lat { get; set; }
        public string longitude { get; set; }
    }

    public class ReportHistoryResponse
    {
        public int page { get; set; }
        public int pageSize { get; set; }
        public int totalCount { get; set; }
        public int totalPages { get; set; }
        public List<ReportHistoryItem> data { get; set; } = new();
    }

    public class ReportHistoryItem
    {
        public string id { get; set; }
        public string nis { get; set; }
        public string name { get; set; }
        public string date { get; set; }
        public string time { get; set; }
        public string description { get; set; }
        public string reportFileId { get; set; }
        public string reportPhotoId { get; set; }
        public FeedbackItem feedback { get; set; }
    }

    public class HistoryState
    {
        public int? StudentId { get; set; }
    }
}
