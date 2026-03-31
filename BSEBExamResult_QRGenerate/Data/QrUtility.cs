using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace BSEBExamResult_QRGenerate.Data
{
    public static class QrUtility
    {
        private static readonly byte[] Key = Encoding.UTF8.GetBytes("A9F3K7L2X8VQ1M5N4B6C7D8E2R5T9Y1U");
        private static readonly byte[] IV = Encoding.UTF8.GetBytes("A9F3K7L2X8VQ1M5N4B6C7D8E2R5T9Y1U");

        // ─── Base85 charset (RFC 1924 — alphanumeric + safe symbols, QR-friendly) ───
        // 85 printable ASCII chars, no characters that upset QR encoders
        private const string B85 =
            "0123456789" +
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
            "abcdefghijklmnopqrstuvwxyz" + "!#$%&()*+-;<=>?@^_`{|}~";  // 10+26+26+23 = 85

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



        //working code anuja 
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
            //sb.Append(student.CollegeName?.Replace("|", "")).Append("|");
            sb.Append(student.RegistrationNo).Append("|");

            // 🔹 Map Faculty full name to single character
            var facultyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    { "ARTS", "A" },
    { "SCIENCE", "S" },
    { "COMMERCE", "C" },
    { "VOCATIONAL", "V" }
};

            // 🔹 Get the short code
            string facultyCode = student.Faculty != null && facultyMap.ContainsKey(student.Faculty)
                ? facultyMap[student.Faculty]
                : ""; // fallback if faculty is null or unknown

            // 🔹 Append to your compact string
            sb.Append(facultyCode).Append("|");
            // sb.Append(student.Faculty?.Replace("|", "")).Append("|");
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
                                                                      //.Append(sub.Theory).Append(",")                     // Theory marks
                                                                      //.Append(sub.OB_PR).Append(",")                      // Practical marks
                                                                      //.Append(sub.GRC_THO).Append(",")                    // Grace theory
                                                                      //.Append(sub.GRC_PR).Append(",")                     // Grace practical
                                                                      //.Append(sub.TotSub?.Replace("|", "")).Append(",")   // Total marks
                                                                      //.Append(sub.CCEMarks?.Replace("|", ""));           // CCE marks



                  .Append((sub.PassMark.HasValue && sub.PassMark.Value != 0) ? sub.PassMark.Value.ToString() : "").Append(",")   // Pass marks
                    .Append(string.IsNullOrEmpty(sub.Theory) || sub.Theory == "0" ? "" : sub.Theory).Append(",")           // Theory marks
      .Append(string.IsNullOrEmpty(sub.OB_PR) || sub.OB_PR == "0" ? "" : sub.OB_PR).Append(",")             // Practical marks
      .Append(string.IsNullOrEmpty(sub.GRC_THO) || sub.GRC_THO == "0" ? "" : sub.GRC_THO).Append(",")       // Grace theory
      .Append(string.IsNullOrEmpty(sub.GRC_PR) || sub.GRC_PR == "0" ? "" : sub.GRC_PR).Append(",")         // Grace practical
     // .Append(sub.TotSub?.Replace("|", "") == "0" ? "" : sub.TotSub?.Replace("|", "")).Append(",")          // Total marks
      .Append(sub.CCEMarks?.Replace("|", "") == "0" ? "" : sub.CCEMarks?.Replace("|", ""));
            }

            var payloadString = sb.ToString();

            // 🔹 Remove any leading/trailing braces
            payloadString = payloadString.Trim('{', '}');

            // 🔹 Convert to bytes
            var bytes = Encoding.UTF8.GetBytes(payloadString);

            

            // 🔹 Compress
            var compressed = Compress(bytes);

            // 🔹 Encrypt
            var encrypted = Encrypt(compressed);

           // var encryptedwithoutkey = EncryptWithoutKey(encrypted);

            // 🔹 Base45 encode for QR
           // return Base45Encode(encrypted);
            return Base85Encode(encrypted);
        }

        // 🔹 Compress using GZip






        // ── Base85 encode (RFC 1924 variant) ─────────────────────────────────────
        // 4 bytes → 5 chars  (1.25× expansion vs Base45's 1.5×)
        public static string Base85Encode(byte[] data)
        {
            var sb = new StringBuilder(data.Length * 5 / 4 + 5);
            int rem = data.Length % 4;
            int full = data.Length - rem;

            for (int i = 0; i < full; i += 4)
            {
                uint n = ((uint)data[i] << 24)
                       | ((uint)data[i + 1] << 16)
                       | ((uint)data[i + 2] << 8)
                       | data[i + 3];

                sb.Append(B85[(int)(n / 52200625u)]);   // 85^4
                n %= 52200625u;
                sb.Append(B85[(int)(n / 614125u)]);     // 85^3
                n %= 614125u;
                sb.Append(B85[(int)(n / 7225u)]);       // 85^2
                n %= 7225u;
                sb.Append(B85[(int)(n / 85u)]);         // 85^1
                sb.Append(B85[(int)(n % 85u)]);         // 85^0
            }

            // Partial tail (0–3 bytes): encode as rem+1 chars
            if (rem > 0)
            {
                uint n = 0;
                for (int i = 0; i < rem; i++)
                    n |= (uint)data[full + i] << (24 - 8 * i);

                int chars = rem + 1;   // 1→2, 2→3, 3→4
                uint div = 1;
                for (int i = 1; i < chars; i++) div *= 85;

                for (int i = 0; i < chars; i++)
                {
                    sb.Append(B85[(int)(n / div)]);
                    n %= div;
                    div /= 85;
                }
            }

            return sb.ToString();
        }






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

           // aes.KeySize = 256; // ✅ Force AES-256
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


        public static byte[] EncryptWithoutKey(byte[] data)
        {
            byte key = 0x5A; // fixed internal byte

            byte[] output = new byte[data.Length];

            for (int i = 0; i < data.Length; i++)
            {
                output[i] = (byte)(data[i] ^ key);
            }

            return output;
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
