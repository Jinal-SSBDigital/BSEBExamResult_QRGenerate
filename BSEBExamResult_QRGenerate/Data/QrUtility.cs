using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace BSEBExamResult_QRGenerate.Data
{
    public static class QrUtility
    {
        private static readonly byte[] Key = Encoding.UTF8.GetBytes("A9F3K7L2X8VQ1M5N4B6C7D8E2R5T9Y1U");
        private static readonly byte[] IV = Encoding.UTF8.GetBytes("A9F3K7L2X8VQ1M5N4B6C7D8E2R5T9Y1U");

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



        public static string GenerateEncryptedPayloadCompact(Model.StudentResult student)
        {
            // 🔹 Step 1: Create compact string (pipe-separated)
            // Exclude dob, status, msg
            var sb = new StringBuilder();

            sb.Append(student.RollCode).Append("|");
            sb.Append(student.RollNo).Append("|");
            sb.Append(student.BsebUniqueID).Append("|");
            sb.Append(student.NameoftheCandidate?.Replace("|", "")).Append("|");
            sb.Append(student.FathersName?.Replace("|", "")).Append("|");
            sb.Append(student.CollegeName?.Replace("|", "")).Append("|");
            sb.Append(student.RegistrationNo).Append("|");
            sb.Append(student.Faculty?.Replace("|", "")).Append("|");
            sb.Append(student.TotalAggregateMarkinNumber.Replace("|", "")).Append("|");
            //sb.Append(student.TotalAggregateMarkinWords?.Replace("|", "")).Append("|");
            sb.Append(student.Division?.Replace("|", ""));

            // 🔹 Define a mapping for subject groups
            var groupMap = new Dictionary<string, string>
    {
        {"1. अनिवार्य Compulsory", "1"},
        {"2. ऐच्छिक Elective", "2"},
        {"3. अतिरिक्त Additional", "3"},
        {"4. Additional subject group Vocational (100 marks)", "4"}
    };

            // 🔹 Add subjects compactly (all 4 groups)
            // 🔹 Add subjects compactly (all groups)
            foreach (var sub in student.SubjectResults)
            {
                var groupId = groupMap.ContainsKey(sub.SubjectGroupName) ? groupMap[sub.SubjectGroupName] : "0";

                sb.Append("|")
                  .Append(groupId).Append(",")                        // Subject group ID
                  .Append(sub.Sub?.Replace("|", "")).Append(",")      // Subject name
                  .Append(sub.Theory).Append(",")                     // Theory marks
                  .Append(sub.OB_PR).Append(",")                      // Practical marks
                  .Append(sub.GRC_THO).Append(",")                    // Grace theory
                  .Append(sub.GRC_PR).Append(",")                     // Grace practical
                  .Append(sub.TotSub?.Replace("|", "")).Append(",")   // Total marks
                  .Append(sub.CCEMarks?.Replace("|", ""));           // CCE marks
            }

            // 🔹 Convert to bytes
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());

            // 🔹 Compress
            var compressed = Compress(bytes);

            // 🔹 Encrypt
            var encrypted = Encrypt(compressed);

            // 🔹 Base45 encode for QR
            return Base45Encode(encrypted);
        }

        // 🔹 Compress using GZip
 



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

        //jinal code
        //// 🔹 Step 3: Encrypt
        //public static byte[] Encrypt(byte[] data)
        //{
        //    using var aes = Aes.Create();
        //    aes.Key = Key;
        //    aes.IV = IV;

        //    using var encryptor = aes.CreateEncryptor();
        //    return encryptor.TransformFinalBlock(data, 0, data.Length);
        //}


        //Anuja Code
        public static byte[] Encrypt(byte[] data)
        {
            using var aes = Aes.Create();

            aes.KeySize = 256; // ✅ Force AES-256
            aes.BlockSize = 128;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            aes.Key = Key;

            // ✅ Generate random IV (16 bytes)
            aes.GenerateIV();
            byte[] iv = aes.IV;

            using var encryptor = aes.CreateEncryptor(aes.Key, iv);
            using var ms = new MemoryStream();

            // ✅ Prepend IV to output
            ms.Write(iv, 0, iv.Length);

            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            {
                cs.Write(data, 0, data.Length);
                cs.FlushFinalBlock();
            }

            return ms.ToArray();
        }




        // 🔹 Step 4: Base45 Encode
        private const string Charset = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";


        private static string Base45Encode(byte[] data)
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


        //jinal code

        //public static string Base45Encode(byte[] data)
        //{
        //    var sb = new StringBuilder();

        //    for (int i = 0; i < data.Length; i += 2)
        //    {
        //        if (i + 1 < data.Length)
        //        {
        //            int x = (data[i] << 8) + data[i + 1];
        //            sb.Append(Charset[x % 45]);
        //            sb.Append(Charset[(x / 45) % 45]);
        //            sb.Append(Charset[x / (45 * 45)]);
        //        }
        //        else
        //        {
        //            int x = data[i];
        //            sb.Append(Charset[x % 45]);
        //            sb.Append(Charset[x / 45]);
        //        }
        //    }

        //    return sb.ToString();
        //}


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
