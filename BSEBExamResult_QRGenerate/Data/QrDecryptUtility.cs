using BSEBExamResult_QRGenerate.Model;
using System.Buffers.Text;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BSEBExamResult_QRGenerate.Data
{
    public static class QrDecryptUtility
    {
        private static readonly byte[] Key = Encoding.UTF8.GetBytes("A9F3K7L2X8VQ1M5N4B6C7D8E2R5T9Y1U");
        private static readonly byte[] IV = Encoding.UTF8.GetBytes("A9F3K7L2X8VQ1M5N4B6C7D8E2R5T9Y1U");


        private const string Charset = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";

        // 🔓 Base45 Decode (matching your QrUtility)
        public static byte[] Base45Decode(string input)
        {
            List<byte> result = new();
            int i = 0;

            while (i < input.Length)
            {
                if (i + 2 < input.Length)
                {
                    int x = Charset.IndexOf(input[i])
                          + 45 * Charset.IndexOf(input[i + 1])
                          + 45 * 45 * Charset.IndexOf(input[i + 2]);
                    result.Add((byte)(x >> 8));
                    result.Add((byte)(x & 0xFF));
                    i += 3;
                }
                else
                {
                    int x = Charset.IndexOf(input[i]) + 45 * Charset.IndexOf(input[i + 1]);
                    result.Add((byte)x);
                    i += 2;
                }
            }

            return result.ToArray();
        }

        // 🔓 AES Decrypt (with IV prepended)
        public static byte[] Decrypt(byte[] encryptedData)
        {
            byte[] iv = new byte[16]; // first 16 bytes = IV
            Array.Copy(encryptedData, 0, iv, 0, 16);

            byte[] ciphertext = new byte[encryptedData.Length - 16];
            Array.Copy(encryptedData, 16, ciphertext, 0, ciphertext.Length);

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.BlockSize = 128;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = Key;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
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

        // 🔓 Full pipeline: decode QR string to compact pipe-separated string
        public static string DecodeToCompactString(string enc)
        {
            byte[] encryptedBytes = Base45Decode(enc);
            byte[] decryptedBytes = Decrypt(encryptedBytes);
            byte[] plainBytes = Decompress(decryptedBytes);
            return Encoding.UTF8.GetString(plainBytes);
        }

        // 🔓 Full pipeline: decode QR string to StudentResult object
        public static StudentResult DecodeToStudent(string enc)
        {
            string compact = DecodeToCompactString(enc);
            return ParseCompactStringToStudent(compact);
        }

        // 🔓 Parse pipe-separated compact string to StudentResult
        private static StudentResult ParseCompactStringToStudent(string compact)
        {
            var parts = compact.Split('|');

            var student = new StudentResult
            {
                RollCode = parts[0],
                RollNo = parts[1],
                BsebUniqueID = parts[2],
                NameoftheCandidate = parts[3],
                FathersName = parts[4],
                CollegeName = parts[5],
                RegistrationNo = parts[6],
                Faculty = parts[7],
                TotalAggregateMarkinNumber = parts[8],
                Division = parts[9],
                SubjectResults = new List<SubjectResult>()
            };

            // Subjects start from index 10
            for (int i = 10; i < parts.Length; i++)
            {
                var subParts = parts[i].Split(',');
                if (subParts.Length >= 8)
                {
                    student.SubjectResults.Add(new SubjectResult
                    {
                        SubjectGroupName = subParts[0],
                        Sub = subParts[1],
                        Theory = subParts[2],
                        OB_PR = subParts[3],
                        GRC_THO = subParts[4],
                        GRC_PR = subParts[5],
                        TotSub = subParts[6],
                        CCEMarks = subParts[7]
                    });
                }
            }

            return student;
        }



        //// 🔓 AES Decrypt
        //public static byte[] Decrypt(byte[] encryptedData)
        //{
        //    using var aes = Aes.Create();
        //    aes.Key = Key;
        //    aes.IV = IV;

        //    using var decryptor = aes.CreateDecryptor();
        //    return decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
        //}

        //// 🔓 GZIP Decompress
        //public static byte[] Decompress(byte[] compressedData)
        //{
        //    using var input = new MemoryStream(compressedData);
        //    using var gzip = new GZipStream(input, CompressionMode.Decompress);
        //    using var output = new MemoryStream();

        //    gzip.CopyTo(output);
        //    return output.ToArray();
        //}

        //// 🔓 FULL PIPELINE: returns JSON string
        //public static string DecodeToJson(string enc)
        //{
        //    // Step 1: URL Decode
        //    string base64 = Uri.UnescapeDataString(enc);

        //    // Step 2: Base64 Decode
        //    byte[] encryptedBytes = Convert.FromBase64String(base64);

        //    // Step 3: AES Decrypt
        //    byte[] decryptedBytes = Decrypt(encryptedBytes);

        //    // Step 4: GZIP Decompress
        //    byte[] originalBytes = Decompress(decryptedBytes);

        //    // Step 5: Convert to string (JSON)
        //    return Encoding.UTF8.GetString(originalBytes);
        //}

        //// 🔓 FULL PIPELINE: returns StudentResult object
        //public static StudentResult DecodeToStudent(string enc)
        //{
        //    string json = DecodeToJson(enc);

        //    // Deserialize JSON to StudentResult
        //    var student = JsonSerializer.Deserialize<StudentResult>(json, new JsonSerializerOptions
        //    {
        //        PropertyNameCaseInsensitive = true,
        //        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        //    });

        //    return student;
        //}
    }
}