using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace BSEBExamResult_QRGenerate.Data
{
    public static class QrUtility
    {
        private static readonly byte[] Key = Encoding.UTF8.GetBytes("1234567890123456");
        private static readonly byte[] IV = Encoding.UTF8.GetBytes("1234567890123456");

        // 🔹 Step 1: Create Compact Data String
        //public static string CreateCompactData(Model.StudentResult student)
        //{
        //    return string.Join("|",
        //        student.RollNo,
        //        student.RollCode,
        //        student.TotalAggregateMarkinNumber,
        //        student.Division,
        //        student.NameoftheCandidate?.Replace("|", ""),
        //        student.FathersName?.Replace("|", "")
        //    );
        //}
        public static string GenerateEncryptedPayloadFull(Model.StudentResult student)
        {
            // 🔹 Convert FULL object to JSON
            var json = System.Text.Json.JsonSerializer.Serialize(student, new System.Text.Json.JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            // 🔹 Convert to bytes
            var bytes = Encoding.UTF8.GetBytes(json);

            // 🔹 Compress
            var compressed = Compress(bytes);

            // 🔹 Encrypt
            var encrypted = Encrypt(compressed);

            // 🔹 Convert to Base64
            string base64 = Convert.ToBase64String(encrypted);

            // 🔹 URL safe
            return Uri.EscapeDataString(base64);
        }
        // 🔹 Step 2: Compress
        public static byte[] Compress(byte[] data)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionMode.Compress, true))
            {
                gzip.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }
        //public static string GenerateEncryptedPayload(Model.StudentResult student)
        //{
        //    var compact = CreateCompactData(student);
        //    var bytes = Encoding.UTF8.GetBytes(compact);
        //    var compressed = Compress(bytes);
        //    var encrypted = Encrypt(compressed);

        //    // Use Base64 URL SAFE
        //    string base64 = Convert.ToBase64String(encrypted);

        //    // Make URL safe
        //    return Uri.EscapeDataString(base64);
        //}
        // 🔹 Step 3: Encrypt
        public static byte[] Encrypt(byte[] data)
        {
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = IV;

            using var encryptor = aes.CreateEncryptor();
            return encryptor.TransformFinalBlock(data, 0, data.Length);
        }

        // 🔹 Step 4: Base45 Encode
        private const string Charset = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";

        public static string Base45Encode(byte[] data)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < data.Length; i += 2)
            {
                if (i + 1 < data.Length)
                {
                    int x = (data[i] << 8) + data[i + 1];
                    sb.Append(Charset[x % 45]);
                    sb.Append(Charset[(x / 45) % 45]);
                    sb.Append(Charset[x / (45 * 45)]);
                }
                else
                {
                    int x = data[i];
                    sb.Append(Charset[x % 45]);
                    sb.Append(Charset[x / 45]);
                }
            }

            return sb.ToString();
        }

        // 🔹 FULL PIPELINE
        //public static string GenerateFinalQrData(Model.StudentResult student)
        //{
        //    var compact = CreateCompactData(student);
        //    var bytes = Encoding.UTF8.GetBytes(compact);
        //    var compressed = Compress(bytes);
        //    var encrypted = Encrypt(compressed);
        //    var encoded = Base45Encode(encrypted);

        //    return encoded;
        //}
    }
}
