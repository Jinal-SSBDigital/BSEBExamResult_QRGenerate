using Newtonsoft.Json;

namespace BSEBExamResult_QRGenerate.Model
{
    public class StudentMiniDto
    {
        [JsonProperty("rc")] public string? RollCode { get; set; }
        [JsonProperty("rn")] public string? RollNo { get; set; }
        [JsonProperty("uid")] public string? BsebUid { get; set; }
        [JsonProperty("nm")] public string? Name { get; set; }
        [JsonProperty("fn")] public string? FatherName { get; set; }
        [JsonProperty("col")] public string? College { get; set; }
        [JsonProperty("reg")] public string? RegNo { get; set; }
        [JsonProperty("fac")] public string? Faculty { get; set; }
        [JsonProperty("tot")] public string? TotalMarks { get; set; }
        [JsonProperty("div")] public string? Division { get; set; }
        [JsonProperty("dob")] public string? DOB { get; set; }
        [JsonProperty("cce")] public int IsCCE { get; set; }

        // Subjects: each subject also uses short keys
        [JsonProperty("sub")] public List<SubMini>? Subjects { get; set; }

        public static StudentMiniDto FromStudentResult(StudentResult s)
        {
            return new StudentMiniDto
            {
                RollCode = s.RollCode,
                RollNo = s.RollNo,
                BsebUid = s.BsebUniqueID,
                Name = s.NameoftheCandidate,
                FatherName = s.FathersName,
                College = s.CollegeName,
                RegNo = s.RegistrationNo,
                Faculty = s.Faculty,
                TotalMarks = s.TotalAggregateMarkinNumber,
                Division = s.Division,
                DOB = s.dob?.ToString("dd/MM/yyyy"),
                IsCCE = (int)s.IsCCEMarks,
                Subjects = s.SubjectResults?.Select(r => new SubMini
                {
                    Sub = r.Sub,
                    Max = r.MaxMark,
                    Pass = r.PassMark,
                    Th = r.Theory,
                    Pr = r.OB_PR,
                    GT = r.GRC_THO,
                    GP = r.GRC_PR,
                    CCE = r.CCEMarks,
                    Tot = r.TotSub,
                    Grp = r.SubjectGroupName
                }).ToList()
            };
        }
    }

    public class SubMini
    {
        [JsonProperty("s")] public string? Sub { get; set; }
        [JsonProperty("mx")] public int? Max { get; set; }
        [JsonProperty("ps")] public int? Pass { get; set; }
        [JsonProperty("th")] public string? Th { get; set; }
        [JsonProperty("pr")] public string? Pr { get; set; }
        [JsonProperty("gt")] public string? GT { get; set; }
        [JsonProperty("gp")] public string? GP { get; set; }
        [JsonProperty("cc")] public string? CCE { get; set; }
        [JsonProperty("tt")] public string? Tot { get; set; }
        [JsonProperty("gn")] public string? Grp { get; set; }
    }
}
