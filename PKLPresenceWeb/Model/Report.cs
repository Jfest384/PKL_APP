  namespace PKLPresenceWeb.Model
{
    public class ReportListResponse
    {
        public int page { get; set; }
        public int pageSize { get; set; }
        public int totalCount { get; set; }
        public int totalPages { get; set; }
        public List<ReportItem> data { get; set; } = new();
    }

    public class ReportItem
    {
        public int id { get; set; }
        public string date { get; set; }
        public string time { get; set; }
        public int studentId { get; set; }
        public string nis { get; set; }
        public string name { get; set; }
        public string classroom_name { get; set; }
        public string company_name { get; set; }
        public string description { get; set; }
        public string feedback { get; set; }
        public string reportFileId { get; set; }
        public string reportPhotoId { get; set; }
        public string isGuidance { get; set; }
    }
}
