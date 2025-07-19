namespace PKL_API.Models
{
    public class Photo
    {
        public Guid id { get; set; }
        public required Byte[] photo { get; set; }
        public required string extension { get; set; }

        public required ICollection<User> Users { get; set; }
    }
}
