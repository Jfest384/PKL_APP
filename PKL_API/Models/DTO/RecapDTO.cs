namespace PKL_API.Models.DTO
{
    public class RecapDTO
    {
        public List<int> StudentIds { get; set; } = new();
        public DateTime Date { get; set; }
    }

    public class GetRecapRequest
    {
        public int StudentId { get; set; }
    }

    public class StudentRekapRow
    {
        public int No { get; set; }
        public int StudentId { get; set; }
        public string Name { get; set; } = "-";
        public List<(string P, string RorT)> Data { get; set; } = new();
        public int TotalP { get; set; }
        public int TotalR { get; set; }
        public int TotalT { get; set; }
    }

}
