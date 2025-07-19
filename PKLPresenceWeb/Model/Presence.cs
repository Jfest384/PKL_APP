namespace PKLPresenceWeb.Model
{
    public class PresenceListResponse
    {
        public int page { get; set; }
        public int pageSize { get; set; }
        public int totalCount { get; set; }
        public int totalPages { get; set; }
        public List<PresenceItem> data { get; set; } = new();
    }

    public class PresenceItem
    {
        public string id_presence { get; set; }
        public string nis { get; set; }
        public string name { get; set; }
        public int classId { get; set; }
        public string classroom_name { get; set; }
        public string date { get; set; }
        public string time { get; set; }
        public string presence_type { get; set; }
        public string feedback { get; set; }
        public string isPresence { get; set; }
        public string lat { get; set; }
        public string longitude { get; set; }
    }

    public class PresenceTypeItem
    {
        public int id { get; set; }
        public string name { get; set; }
    }

    public class PresencePhotoResponse
    {
        public string id { get; set; }
        public string type { get; set; }
        public string extension { get; set; }
        public string url { get; set; }
    }

    public class GeolocationPosition
    {
        public GeolocationCoords coords { get; set; }
    }
    public class GeolocationCoords
    {
        public double latitude { get; set; }
        public double longitude { get; set; }
    }
}
