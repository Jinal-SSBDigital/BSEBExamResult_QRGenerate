using System.IO.Compression;
using System.Text;

namespace BSEBExamResult_QRGenerate.Data
{
    public static class CompressionHelper
    {
        public static string Compress(string text)
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

        public static string Decompress(string compressedText)
        {
            var bytes = Convert.FromBase64String(compressedText);
            using var msi = new MemoryStream(bytes);
            using var mso = new MemoryStream();
            using var gs = new GZipStream(msi, CompressionMode.Decompress);
            gs.CopyTo(mso);
            return Encoding.UTF8.GetString(mso.ToArray());
        }
    }
}
