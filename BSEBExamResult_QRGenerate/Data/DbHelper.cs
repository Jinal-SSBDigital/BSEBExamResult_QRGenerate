using BSEBExamResult_QRGenerate.Model;
using Microsoft.AspNetCore.Mvc;
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


        //new
        // 🔹 Get ALL rollcodes by rollno
        public async Task<List<string>> GetRollCodesByRollNoAsync(string rollno)
        {
            try
            {
                var rollCodes = new List<string>();

                var conn = _context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"SELECT DISTINCT TOP 10 RollCode  FROM [BSEB-RESULT-2025].[dbo].[EXAM_FinalPublishedResult]  WHERE RollNumber = @rollno";

                cmd.Parameters.Add(new SqlParameter("@rollno", rollno));

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    rollCodes.Add(reader["RollCode"].ToString());
                }

                return rollCodes;
            }
            catch (Exception ex)
            {

                throw;
            }

        }

        // 🔹 Get student + subject result
        public async Task<StudentResult?> GetStudentResultAsync(string rollcode, string rollno)
        {
            try
            {
                var conn = _context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                //cmd.CommandText = "LoginSp";
                cmd.CommandText = "MultipleQR";
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add(new SqlParameter("@rollcode", rollcode));
                cmd.Parameters.Add(new SqlParameter("@rollno", rollno));

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    return null;

                var student = new StudentResult
                {
                    Status = reader.GetInt32(reader.GetOrdinal("status")),
                    RollCode = reader["rollcode"].ToString(),
                    RollNo = reader["rollno"].ToString(),
                    BsebUniqueID = reader["BsebUniqueID"].ToString(),
                    msg = reader["msg"].ToString(),
                    dob = DateTime.TryParse(reader["dob"]?.ToString(), out var d) ? d : null,
                    NameoftheCandidate = reader["NameoftheCandidate"].ToString(),
                    FathersName = reader["FathersName"].ToString(),
                    CollegeName = reader["CollegeName"].ToString(),
                    RegistrationNo = reader["RegistrationNo"].ToString(),
                    Faculty = reader["FACULTY"].ToString(),
                    TotalAggregateMarkinNumber = reader["TotalAggregateMarkinNumber"].ToString(),
                    TotalAggregateMarkinWords = reader["TotalAggregateMarkinWords"].ToString(),
                    Division = reader["DIVISION"].ToString()
                };

                while (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        student.SubjectResults.Add(new SubjectResult
                        {
                            Sub = reader["Sub"]?.ToString(),
                            MaxMark = reader.IsDBNull("maxMark") ? null : reader.GetInt32("maxMark"),
                            PassMark = reader.IsDBNull("passMark") ? null : reader.GetInt32("passMark"),
                            Theory = reader["theory"]?.ToString(),
                            OB_PR = reader["OB_PR"]?.ToString(),
                            GRC_THO = reader["GRC_THO"]?.ToString(),
                            GRC_PR = reader["GRC_PR"]?.ToString(),
                            CCEMarks = reader["CCEMarks"]?.ToString(),
                            //CCEMarks = reader.IsDBNull("CCEMarks") ? null : reader.GetInt32("CCEMarks"),
                            TotSub = reader["TOT_SUB"]?.ToString(),
                            SubjectGroupName = reader["SubjectGroupName"]?.ToString()
                        });
                    }
                }

                return student;
            }
            catch (Exception ex)
            {

                throw;
            }

        }
        public async Task<List<StudentResult>> GetStudentsForQRAsync()
        {
            try
            {
                var students = new List<StudentResult>();

                using var conn = _context.Database.GetDbConnection();
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "LoginSp"; // same SP
                //cmd.CommandText = "LoginSp"; // same SP
                cmd.CommandType = CommandType.StoredProcedure;

                // ⚠️ IMPORTANT:
                // Modify your SP so that when @rollcode and @rollno are NULL,
                // it returns ALL students

                cmd.Parameters.Add(new SqlParameter("@rollcode", DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@rollno", DBNull.Value));

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
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

                    students.Add(student);
                }

                return students;
            }
            catch (Exception ex)
            {

                throw;
            }

        }

    }
}
