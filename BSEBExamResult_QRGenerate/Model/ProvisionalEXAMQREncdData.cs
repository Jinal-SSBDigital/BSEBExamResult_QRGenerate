using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BSEBExamResult_QRGenerate.Model
{
    [Table("Provisional_EXAMQREncdData")]
    public class ProvisionalEXAMQREncdData
    {
       // [Key]
       // public int Id { get; set; } // Add if table has PK (recommended)

        public string RollCode { get; set; }

        public string RollNo { get; set; }

        public string EncryptedData { get; set; }

        public int QRLength { get; set; }
    }
}
