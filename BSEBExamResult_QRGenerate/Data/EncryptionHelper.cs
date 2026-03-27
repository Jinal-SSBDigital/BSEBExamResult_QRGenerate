using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Security.Cryptography;
using System.Text;

namespace BSEBExamResult_QRGenerate.Data
{
    public class EncryptionHelper
    {
        // ⚠ Keep this secret & same on QR reader side
        //private static readonly string EncryptionKey = "BSEB2026@SecureKey!";
        private static readonly byte[] Key = SHA256.HashData(
        Encoding.UTF8.GetBytes("BSEB2026@SecureKey!")
    ); // 32 bytes, AES-256 — same key your old code used ✅

        public static byte[] Encrypt(byte[] data)
        {
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV(); // random IV every time = more secure

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();

            // Prepend IV (16 bytes) so decrypt side can extract it
            ms.Write(aes.IV, 0, aes.IV.Length);

            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            {
                cs.Write(data, 0, data.Length);
                cs.FlushFinalBlock();
            }

            return ms.ToArray(); // [16 bytes IV] + [encrypted data]
        }

        // ✅ NEW: AES encrypted byte[] → decrypted byte[] (for decode side)
        public static byte[] Decrypt(byte[] encryptedData)
        {
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            // Extract prepended IV
            var iv = new byte[16];
            Array.Copy(encryptedData, 0, iv, 0, iv.Length);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream(
                encryptedData, iv.Length, encryptedData.Length - iv.Length
            );
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var output = new MemoryStream();
            cs.CopyTo(output);
            return output.ToArray();
        }

        // ✅ KEPT: old string→string Encrypt (if used elsewhere in project)
        public static string EncryptString(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();
            ms.Write(aes.IV, 0, aes.IV.Length);
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(plainText);
            }
            return Convert.ToBase64String(ms.ToArray());
        }

        // ✅ KEPT: old string→string Decrypt (if used elsewhere in project)
        public static string DecryptString(string cipherText)
        {
            var fullCipher = Convert.FromBase64String(cipherText);
            using var aes = Aes.Create();
            aes.Key = Key;
            var iv = new byte[16];
            Array.Copy(fullCipher, 0, iv, 0, iv.Length);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream(fullCipher, iv.Length, fullCipher.Length - iv.Length);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }
    }
    //public static string Encrypt(string plainText)
    //{
    //    using var aes = Aes.Create();
    //    aes.Key = GetKey();
    //    aes.GenerateIV();

    //    using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
    //    using var ms = new MemoryStream();

    //    // prepend IV
    //    ms.Write(aes.IV, 0, aes.IV.Length);

    //    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
    //    using (var sw = new StreamWriter(cs))
    //    {
    //        sw.Write(plainText);
    //    }

    //    return Convert.ToBase64String(ms.ToArray());
    //}

    //public static string Decrypt(string cipherText)
    //{
    //    var fullCipher = Convert.FromBase64String(cipherText);

    //    using var aes = Aes.Create();
    //    aes.Key = GetKey();

    //    var iv = new byte[16];
    //    Array.Copy(fullCipher, 0, iv, 0, iv.Length);
    //    aes.IV = iv;

    //    using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
    //    using var ms = new MemoryStream(fullCipher, iv.Length, fullCipher.Length - iv.Length);
    //    using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
    //    using var sr = new StreamReader(cs);

    //    return sr.ReadToEnd();
    //}

    //private static byte[] GetKey()
    //{
    //    using var sha = SHA256.Create();
    //    return sha.ComputeHash(Encoding.UTF8.GetBytes(EncryptionKey));
    //}
}

