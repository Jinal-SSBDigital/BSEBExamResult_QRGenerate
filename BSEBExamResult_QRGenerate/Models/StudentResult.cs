namespace BSEBExamResult_QRGenerate.Models
{
    public class StudentResult
    {
        public int Status { get; set; }
        public string RollCode { get; set; }
        public string RollNo { get; set; }
        public string NameoftheCandidate { get; set; }
        public string FathersName { get; set; }
        public string CollegeName { get; set; }
        public string Faculty { get; set; }
        public string TotalAggregateMarkinNumber { get; set; }
        public string Division { get; set; }

        public List<SubjectResult> SubjectResults { get; set; } = new();
    }
}
