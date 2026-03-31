namespace BSEBExamResult_QRGenerate.Model
{
    public class QREncryptedData
    {
        public long Id { get; set; }
        public string RollCode { get; set; }
        public string RollNo { get; set; }
        public string EncryptedData { get; set; }
        public int Length { get; set; }

        //public string QrPath { get; set; }
        public DateTime CreatedOn { get; set; }
    }
}
