using Microsoft.AspNetCore.Components.Forms;

namespace PKLPresenceWeb.Model
{
    public class BlazorInputFileStreamFile : IBrowserFile
    {
        public BlazorInputFileStreamFile(string name, string contentType, byte[] data)
        {
            Name = name;
            LastModified = DateTimeOffset.Now;
            Size = data.Length;
            ContentType = contentType;
            _data = data;
        }

        private readonly byte[] _data;

        public string Name { get; }
        public DateTimeOffset LastModified { get; }
        public long Size { get; }
        public string ContentType { get; }

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
            => new MemoryStream(_data);
    }
}
