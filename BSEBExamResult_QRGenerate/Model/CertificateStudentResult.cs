namespace BSEBExamResult_QRGenerate.Model
{
    public class CertificateStudentResult
    {
        public int Status { get; set; }
        public string? Msg { get; set; }
        public string? RollCode { get; set; }
        public string? RollNo { get; set; }
        public string? RegistrationNo { get; set; }
        public string? BsebUniqueID { get; set; }
        public string? NameoftheCandidate { get; set; }
        public string? FathersName { get; set; }
        public string? MothersName { get; set; }
        public string? Gender { get; set; }
        public string? CollegeName { get; set; }
        public string? DistrictName { get; set; }
        public string? Faculty { get; set; }
        public string? Division { get; set; }
        public string? TotalMarks { get; set; }
        public string? Nationality { get; set; }
        public string? ExamType { get; set; }
        public List<CertificateSubject> Subjects { get; set; } = new();
    }
    public class CertificateSubject
    {
        public string? SubjectName { get; set; }
        public string? SubjectPaperCode { get; set; }
        public string? SubjectGroupName { get; set; }
        public int? SubjectPaperGroupId { get; set; }
    }
}
