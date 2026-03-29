namespace BSEBExamResult_QRGenerate.Model
{
    public class QrData
    {
        public int Id { get; set; }
        public string ShortId { get; set; }
        public string EncryptedValue { get; set; }
        public DateTime CreatedOn { get; set; }
    }
}
