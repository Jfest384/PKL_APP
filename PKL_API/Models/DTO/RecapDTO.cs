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
}
