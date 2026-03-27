using System.IO.Compression;
using System.Text;

namespace BSEBExamResult_QRGenerate.Data
{
    public static class CompressionHelper
    {
        // ✅ NEW: JSON string → compressed byte[] (for QR pipeline)
        public static byte[] Compress(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            using var mso = new MemoryStream();
            using (var gs = new GZipStream(mso, CompressionLevel.SmallestSize))
            {
                gs.Write(bytes, 0, bytes.Length);
            }
            return mso.ToArray(); // raw compressed bytes (NOT Base64)
        }

        // ✅ NEW: compressed byte[] → original JSON string (for decode side)
        public static string Decompress(byte[] compressedBytes)
        {
            using var msi = new MemoryStream(compressedBytes);
            using var mso = new MemoryStream();
            using var gs = new GZipStream(msi, CompressionMode.Decompress);
            gs.CopyTo(mso);
            return Encoding.UTF8.GetString(mso.ToArray());
        }

        // ✅ KEPT: old string→string versions (if used elsewhere in project)
        public static string CompressToBase64(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            using var msi = new MemoryStream(bytes);
            using var mso = new MemoryStream();
            using (var gs = new GZipStream(mso, CompressionMode.Compress))
            {
                msi.CopyTo(gs);
            }
            return Convert.ToBase64String(mso.ToArray());
        }

        public static string DecompressFromBase64(string compressedText)
        {
            var bytes = Convert.FromBase64String(compressedText);
            using var msi = new MemoryStream(bytes);
            using var mso = new MemoryStream();
            using var gs = new GZipStream(msi, CompressionMode.Decompress);
            gs.CopyTo(mso);
            return Encoding.UTF8.GetString(mso.ToArray());
        }
        //public static string Compress(string text)
        //{
        //    var bytes = Encoding.UTF8.GetBytes(text);
        //    using var msi = new MemoryStream(bytes);
        //    using var mso = new MemoryStream();
        //    using (var gs = new GZipStream(mso, CompressionMode.Compress))
        //    {
        //        msi.CopyTo(gs);
        //    }
        //    return Convert.ToBase64String(mso.ToArray());
        //}

        //public static string Decompress(string compressedText)
        //{
        //    var bytes = Convert.FromBase64String(compressedText);
        //    using var msi = new MemoryStream(bytes);
        //    using var mso = new MemoryStream();
        //    using var gs = new GZipStream(msi, CompressionMode.Decompress);
        //    gs.CopyTo(mso);
        //    return Encoding.UTF8.GetString(mso.ToArray());
        //}
    }
}
