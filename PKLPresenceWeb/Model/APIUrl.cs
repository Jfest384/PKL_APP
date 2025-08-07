namespace PKLPresenceWeb.Model
{
    public static class APIUrl
    {
        // Ubah base URL sesuai kebutuhan (bisa dari konfigurasi jika perlu)
        public static string Base => "http://localhost:5027/api/";

        // Fungsi untuk membangun URL endpoint
        public static string Endpoint(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return Base;
            // Pastikan tidak ada double slash
            return Base.TrimEnd('/') + "/" + path.TrimStart('/');
        }
    }
}