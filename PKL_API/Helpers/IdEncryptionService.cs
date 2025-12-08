using System.Security.Cryptography;
using System.Text;

namespace PKL_API.Helpers
{
    public interface IIdEncryptionService
    {
        string EncryptId(int id);
        int DecryptId(string encrypted);
    }

    public class IdEncryptionService : IIdEncryptionService
    {
        private readonly string _key;
        private readonly string _iv;

        public IdEncryptionService(IConfiguration config)
        {
            // Ambil key dari appsettings.json
            _key = config["Encryption:Key"];
            _iv = config["Encryption:IV"];
        }

        public string EncryptId(int id)
        {
            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(_key);
            aes.IV = Encoding.UTF8.GetBytes(_iv);

            var plainBytes = Encoding.UTF8.GetBytes(id.ToString());
            var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            return Convert.ToBase64String(encryptedBytes)
                .Replace("/", "_")
                .Replace("+", "-");
        }

        public int DecryptId(string encrypted)
        {
            encrypted = encrypted.Replace("_", "/").Replace("-", "+");
            var cipherBytes = Convert.FromBase64String(encrypted);

            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(_key);
            aes.IV = Encoding.UTF8.GetBytes(_iv);

            var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            var decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

            return int.Parse(Encoding.UTF8.GetString(decryptedBytes));
        }
    }
}
