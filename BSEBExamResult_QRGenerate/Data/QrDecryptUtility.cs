using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BSEBExamResult_QRGenerate.Model;

namespace BSEBExamResult_QRGenerate.Data
{
    public static class QrDecryptUtility
    {
        private static readonly byte[] Key = Encoding.UTF8.GetBytes("1234567890123456");
        private static readonly byte[] IV = Encoding.UTF8.GetBytes("1234567890123456");

        // 🔓 AES Decrypt
        public static byte[] Decrypt(byte[] encryptedData)
        {
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = IV;

            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
        }

        // 🔓 GZIP Decompress
        public static byte[] Decompress(byte[] compressedData)
        {
            using var input = new MemoryStream(compressedData);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();

            gzip.CopyTo(output);
            return output.ToArray();
        }

        // 🔓 FULL PIPELINE: returns JSON string
        public static string DecodeToJson(string enc)
        {
            // Step 1: URL Decode
            string base64 = Uri.UnescapeDataString(enc);

            // Step 2: Base64 Decode
            byte[] encryptedBytes = Convert.FromBase64String(base64);

            // Step 3: AES Decrypt
            byte[] decryptedBytes = Decrypt(encryptedBytes);

            // Step 4: GZIP Decompress
            byte[] originalBytes = Decompress(decryptedBytes);

            // Step 5: Convert to string (JSON)
            return Encoding.UTF8.GetString(originalBytes);
        }

        // 🔓 FULL PIPELINE: returns StudentResult object
        public static StudentResult DecodeToStudent(string enc)
        {
            string json = DecodeToJson(enc);

            // Deserialize JSON to StudentResult
            var student = JsonSerializer.Deserialize<StudentResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            return student;
        }
    }
}