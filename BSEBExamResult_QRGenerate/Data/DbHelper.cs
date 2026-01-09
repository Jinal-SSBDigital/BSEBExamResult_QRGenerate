using BSEBExamResult_QRGenerate.Data;
using BSEBExamResult_QRGenerate.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;


namespace BSEBExamResult_QRGenerate.Data
{

    public class DbHelper
    {
        private readonly AppDBContext _context;

        public DbHelper(AppDBContext context)
        {
            _context = context;
        }

        public async Task<StudentResult> GetStudentResultAsync(string rollcode, string rollno)
        {
            using var conn = _context.Database.GetDbConnection();
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "LoginSp";
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add(new SqlParameter("@rollcode", rollcode));
            cmd.Parameters.Add(new SqlParameter("@rollno", rollno));

            using var reader = await cmd.ExecuteReaderAsync();

            // 🔹 Result Set 1: Student Header
            if (!await reader.ReadAsync())
                return null;

            var student = new StudentResult
            {
                Status = reader.GetInt32(reader.GetOrdinal("status")),
                RollCode = reader["rollcode"].ToString(),
                RollNo = reader["rollno"].ToString(),
                NameoftheCandidate = reader["NameoftheCandidate"].ToString(),
                FathersName = reader["FathersName"].ToString(),
                CollegeName = reader["CollegeName"].ToString(),
                Faculty = reader["FACULTY"].ToString(),
                TotalAggregateMarkinNumber = reader["TotalAggregateMarkinNumber"].ToString(),
                Division = reader["DIVISION"].ToString()
            };

            // 🔹 Read all subject result sets
            while (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync())
                {
                    student.SubjectResults.Add(new SubjectResult
                    {
                        Sub = reader["Sub"].ToString(),
                        MaxMark = Convert.ToInt32(reader["maxMark"]),
                        PassMark = Convert.ToInt32(reader["passMark"]),
                        Theory = reader["theory"].ToString(),
                        OB_PR = reader["OB_PR"].ToString(),
                        GRC_THO = reader["GRC_THO"].ToString(),
                        GRC_PR = reader["GRC_PR"].ToString(),
                        TotSub = reader["TOT_SUB"].ToString()
                    });
                }
            }

            return student;
        }
    }
}
