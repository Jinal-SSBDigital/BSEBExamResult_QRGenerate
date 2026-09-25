using BSEBExamResult_QRGenerate.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Cryptography.Xml;

namespace BSEBExamResult_QRGenerate.Data
{
    public class DbHelper
    {
        #region Jinal Nagar
        //private readonly AppDBContext _context;
        private readonly AppDBContext _context;
        private readonly string _dbCerti;
        private readonly string _dbOriginalCertiQRData;

        public DbHelper(AppDBContext context, IConfiguration config)
        {
            _context = context;
            _dbCerti = config.GetConnectionString("dbCerti") ?? throw new InvalidOperationException("Connection string 'dbCerti' not found.");
            _dbOriginalCertiQRData = config.GetConnectionString("dbOriginalCertiQRData") ?? throw new InvalidOperationException("Connection string 'dbOriginalCertiQRData' not found.");
        }

        private static async Task<SqlConnection> OpenConnectionAsync(string connectionString, string connectionName)
        {
            var conn = new SqlConnection(connectionString);
            try
            {
                await conn.OpenAsync();
                return conn;
            }
            catch (SqlException ex)
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                conn.Dispose();
                throw new InvalidOperationException(
                    $"Failed to open connection '{connectionName}'. " +
                    $"Server='{builder.DataSource}', Database='{builder.InitialCatalog}'. " +
                    $"Check that: (1) the server name is spelled correctly, " +
                    $"(2) SQL Server is running and reachable from this machine (try 'sqlcmd -S {builder.DataSource} -d {builder.InitialCatalog} -E'), " +
                    $"(3) TCP/IP is enabled in SQL Server Configuration Manager, " +
                    $"(4) firewall allows the connection. Original error: {ex.Message}", ex);
            }
        }

        //new
        // 🔹 Get ALL rollcodes by rollno

        // ✅ NEW: Get ALL RollCode + RollNo pairs (up to 13 lakh students)
        public async Task<List<(string RollCode, string RollNo)>> GetAllRollCodesAsync()
        {
            var result = new List<(string, string)>();
            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "GetAllRollCodesForQR"; // ← your new SP
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandTimeout = 300; // 5 min timeout for large data

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add((
                    reader["RollCode"].ToString()!,
                    reader["RollNo"].ToString()!
                ));
            }

            return result;
        }
        // ✅ Bulk insert encrypted data using SqlBulkCopy
        // ✅ Bulk insert encrypted data using SqlBulkCopy
        public async Task BulkSaveEncryptedDataAsync(List<QREncryptedData> records)
        {
            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            var sqlConn = (SqlConnection)conn;

            var dt = new DataTable();
            dt.Columns.Add("RollCode", typeof(string));
            dt.Columns.Add("RollNo", typeof(string));
            dt.Columns.Add("EncryptedData", typeof(string));
            dt.Columns.Add("CharLength", typeof(int));

            foreach (var r in records)
                dt.Rows.Add(r.RollCode, r.RollNo, r.EncryptedData,r.Length);

            using var bulk = new SqlBulkCopy(sqlConn)
            {
                DestinationTableName = "[InterExam2026].[dbo].[EXAM_QREncryptedData]",
                BatchSize = 1000,
                BulkCopyTimeout = 600
            };

            bulk.ColumnMappings.Add("RollCode", "RollCode");
            bulk.ColumnMappings.Add("RollNo", "RollNo");
            bulk.ColumnMappings.Add("EncryptedData", "EncryptedData");
            bulk.ColumnMappings.Add("CharLength", "CharLength"); 

            await bulk.WriteToServerAsync(dt);
        }
        public async Task<List<string>> GetRollCodesByRollNoAsync(string rollno)
        {
            try
            {
                var rollCodes = new List<string>();

                var conn = _context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                using var cmd = conn.CreateCommand();

                cmd.CommandText = @" SELECT DISTINCT TOP 25 RollCode FROM [InterExam2026].[dbo].[EXAM_FinalPublishedResult]   WHERE IsActive = 1 AND RollNumber = @rollno ORDER BY RollCode desc";
                //cmd.CommandText = @" SELECT DISTINCT TOP 20 RollCode FROM [InterExam2026].[dbo].[EXAM_FinalPublishedResult]   WHERE IsActive = 1 AND RollNumber = @rollno ORDER BY RollCode ASC";
                //cmd.CommandText = @"SELECT DISTINCT TOP 10 RollCode  FROM [BSEB-RESULT-2025].[dbo].[EXAM_FinalPublishedResult]  WHERE RollNumber = @rollno";

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

        // 🔹 Get student + subject result for multiple qr generate 
        public async Task<CompartStudentResult?> GetCompartStudentResultAsync(string rollcode, string rollno)
        {
            try
            {
                var conn = _context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                //cmd.CommandText = "LoginSp";
                //cmd.CommandText = "MultipleQR"; // db BSEB-RESULT-2025
                cmd.CommandText = "sp_finalresultforqr"; // db InterExam2026
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add(new SqlParameter("@rollcode", rollcode));
                cmd.Parameters.Add(new SqlParameter("@rollno", rollno));

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    return null;

                var student = new CompartStudentResult
                {
                    Status = reader.GetInt32(reader.GetOrdinal("status")),
                    IsCCEMarks = reader.GetInt32(reader.GetOrdinal("IsCCEMarks")),
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
                    Division = reader["DIVISION"].ToString(),
                    ExamType = reader["ExamType"]?.ToString()

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
        public async Task BulkSaveCompartEncryptedDataAsync(List<EXAM_QRComprtEncryptedData> records)
        {
            if (records == null || records.Count == 0)
                return;

            var conn = (SqlConnection)_context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            var dt = new DataTable();
            dt.Columns.Add("RollCode", typeof(string));
            dt.Columns.Add("RollNo", typeof(string));
            dt.Columns.Add("EncryptedData", typeof(string));
            dt.Columns.Add("Length", typeof(string)); // Length column in DB is nvarchar(max)

            foreach (var r in records)
                dt.Rows.Add(r.RollCode, r.RollNo, r.EncryptedData, r.Length.ToString());

            using var bulk = new SqlBulkCopy(conn)
            {
                DestinationTableName = "[InterCompartSpecial2026].[dbo].[EXAM_QRComprtEncryptedData]",
                BatchSize = 1000,
                BulkCopyTimeout = 600
            };

            bulk.ColumnMappings.Add("RollCode", "RollCode");
            bulk.ColumnMappings.Add("RollNo", "RollNo");
            bulk.ColumnMappings.Add("EncryptedData", "EncryptedData");
            bulk.ColumnMappings.Add("Length", "Length");
            // CreatedOn and Id are intentionally NOT mapped:
            // - Id is IDENTITY, SqlBulkCopy skips it automatically
            // - CreatedOn will be NULL unless you also map it (see note below)

            await bulk.WriteToServerAsync(dt);
        }

        public async Task<StudentResult?> GetStudentResultAsync(string rollcode, string rollno)
        {
            try
            {
                var conn = _context.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                //cmd.CommandText = "LoginSp";
                //cmd.CommandText = "MultipleQR"; // db BSEB-RESULT-2025
                cmd.CommandText = "sp_finalresultforqr"; // db InterExam2026
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add(new SqlParameter("@rollcode", rollcode));
                cmd.Parameters.Add(new SqlParameter("@rollno", rollno));

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    return null;

                var student = new StudentResult
                {
                    Status = reader.GetInt32(reader.GetOrdinal("status")),
                    IsCCEMarks = reader.GetInt32(reader.GetOrdinal("IsCCEMarks")),
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

        public async Task SaveQrDataAsync(string shortId, string encryptedValue)
        {
            var conn = _context.Database.GetDbConnection();

            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO QrData (ShortId, EncryptedValue)
                        VALUES (@ShortId, @EncryptedValue)";

            var param1 = cmd.CreateParameter();
            param1.ParameterName = "@ShortId";
            param1.Value = shortId;

            var param2 = cmd.CreateParameter();
            param2.ParameterName = "@EncryptedValue";
            param2.Value = encryptedValue;

            cmd.Parameters.Add(param1);
            cmd.Parameters.Add(param2);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<(string RollCode, string RollNo)>> Provisional_RollCodesForQR()
        {
            var result = new List<(string, string)>();
            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "sp_GetProvisionalRollCodesForQR"; // ← your new SP
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandTimeout = 300; // 5 min timeout for large data

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add((
                    reader["RollCode"].ToString()!,
                    reader["RollNo"].ToString()!
                ));
            }

            return result;
        }

        public async Task BulkSaveProvisionalEXAMQREncData(List<ProvisionalEXAMQREncdData> records)
        {
            if (records == null || records.Count == 0)
                return;

            var conn = (SqlConnection)_context.Database.GetDbConnection();

            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            using var transaction = conn.BeginTransaction();

            try
            {
                var dt = new DataTable();
                dt.Columns.Add("RollCode", typeof(string));
                dt.Columns.Add("RollNo", typeof(string));
                dt.Columns.Add("EncryptedData", typeof(string));
                dt.Columns.Add("QRLength", typeof(int));

                foreach (var r in records)
                {
                    dt.Rows.Add(r.RollCode, r.RollNo, r.EncryptedData, r.QRLength);
                }

                using var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.Default, transaction)
                {
                    DestinationTableName = "[InterExam2026].[dbo].[Provisional_EXAMQREncdData]",
                    BatchSize = 2000,
                    BulkCopyTimeout = 600,
                    EnableStreaming = true
                };

                bulk.ColumnMappings.Add("RollCode", "RollCode");
                bulk.ColumnMappings.Add("RollNo", "RollNo");
                bulk.ColumnMappings.Add("EncryptedData", "EncryptedData");
                bulk.ColumnMappings.Add("QRLength", "QRLength");

                await bulk.WriteToServerAsync(dt);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task BulkSaveCompartProvisionalEXAMQREncData(List<ProvisionalEXAMQREncdData> records)
        {
            if (records == null || records.Count == 0)
                return;

            var conn = (SqlConnection)_context.Database.GetDbConnection();

            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            using var transaction = conn.BeginTransaction();

            try
            {
                var dt = new DataTable();
                dt.Columns.Add("RollCode", typeof(string));
                dt.Columns.Add("RollNo", typeof(string));
                dt.Columns.Add("EncryptedData", typeof(string));
                dt.Columns.Add("QRLength", typeof(int));

                foreach (var r in records)
                {
                    dt.Rows.Add(r.RollCode, r.RollNo, r.EncryptedData, r.QRLength);
                }

                using var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.Default, transaction)
                {
                    DestinationTableName = "[InterCompartSpecial2026].[dbo].[ProvisionalEXAMQREncdData]",
                    BatchSize = 2000,
                    BulkCopyTimeout = 600,
                    EnableStreaming = true
                };

                //bulk.ColumnMappings.Add("Id", "Id");
                bulk.ColumnMappings.Add("RollCode", "RollCode");
                bulk.ColumnMappings.Add("RollNo", "RollNo");
                bulk.ColumnMappings.Add("EncryptedData", "EncryptedData");
                bulk.ColumnMappings.Add("QRLength", "QRLength");

                await bulk.WriteToServerAsync(dt);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<(string RollCode, string RollNo)>> AnnualOriginal_RollCodesForQR()
        {
            try
            {
                var result = new List<(string, string)>();
                using var conn = await OpenConnectionAsync(_dbCerti, nameof(_dbCerti));

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "sp_GetAnnualOriginal_RollCodesForQR"; // ← your new SP
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 300; // 5 min timeout for large data

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add((
                        reader["RollCode"].ToString()!,
                        reader["RollNo"].ToString()!
                    ));
                }

                return result;
            }
            catch (Exception ex)
            {

                throw ex;
            }

        }
        public async Task<List<(string RollCode, string RollNo)>> AnnualOriginalCompartCert_RollCodesForQR()
        {
            try
            {
                var result = new List<(string, string)>();
                using var conn = await OpenConnectionAsync(_dbCerti, nameof(_dbCerti));

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "sp_GetAnnualOriginalCompart_RollCodesForQR"; // ← your new SP
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 300; // 5 min timeout for large data

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add((
                        reader["RollCode"].ToString()!,
                        reader["RollNo"].ToString()!
                    ));
                }

                return result;
            }
            catch (Exception ex)
            {

                throw ex;
            }

        }

        public async Task<CertificateStudentResult?> GetAnnualStudentResultAsync(string rollcode, string rollno)
        {
            try
            {
                using var conn = new SqlConnection(_dbCerti);
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "sp_CertificateResultForQR";
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add(new SqlParameter("@rollcode", rollcode));
                cmd.Parameters.Add(new SqlParameter("@rollno", rollno));

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    return null;

                // ⚠️ FIX: reader.GetInt32(...) requires the column to be exactly
                // SQL `int`. If "status" (or any other numeric column here) is
                // actually tinyint/smallint/bigint/decimal, GetInt32 throws
                // InvalidCastException ("Unable to cast object of type 'Byte' to
                // type 'Int32'"). Convert.ToInt32(...) accepts any numeric
                // source type and converts safely, so it's used everywhere below
                // instead of GetInt32.
                var status = Convert.ToInt32(reader["status"]);
                var msg = reader["msg"]?.ToString();

                // Invalid roll code/no: this result set only has status + msg.
                // Return immediately — reading any other column here would throw.
                if (status != 1)
                {
                    return new CertificateStudentResult { Status = status, Msg = msg };
                }

                var student = new CertificateStudentResult
                {
                    Status = status,
                    Msg = msg,
                    RollCode = reader["RollCode"]?.ToString(),
                    RollNo = reader["rollno"]?.ToString(),
                    RegistrationNo = reader["RegistrationNo"]?.ToString(),
                    BsebUniqueID = reader["BsebUniqueID"]?.ToString(),
                    NameoftheCandidate = reader["NameoftheCandidate"]?.ToString(),
                    FathersName = reader["FathersName"]?.ToString(),
                    MothersName = reader["MothersName"]?.ToString(),
                    Gender = reader["Gender"]?.ToString(),
                    CollegeName = reader["CollegeName"]?.ToString(),
                    DistrictName = reader["DistrictName"]?.ToString(),
                    Faculty = reader["FACULTY"]?.ToString(),
                    Division = reader["DIVISION"]?.ToString(),
                    TotalMarks = reader["TotalMarks"]?.ToString(),
                    Nationality = reader["Nationality"]?.ToString(),
                    ExamType = reader["ExamType"]?.ToString()
                };

                while (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var SubjectPaperGroupId = reader["SubjectPaperGroupId"];

                        student.Subjects.Add(new CertificateSubject
                        {
                            SubjectName = reader["SubjectName"]?.ToString(),
                            SubjectPaperCode = reader["SubjectPaperCode"]?.ToString(),
                            SubjectGroupName = reader["SubjectGroupName"]?.ToString(),
                            SubjectPaperGroupId = (SubjectPaperGroupId == DBNull.Value)? null: Convert.ToInt32(SubjectPaperGroupId)
                        });
                    }
                }

                return student;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<CertificateStudentResult?> GetCompartCertificateResultForQR(string rollcode, string rollno)
        {
            try
            {
                using var conn = new SqlConnection(_dbCerti);
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "sp_CompartCertificateResultForQR";
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add(new SqlParameter("@rollcode", rollcode));
                cmd.Parameters.Add(new SqlParameter("@rollno", rollno));

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                    return null;

                // ⚠️ FIX: reader.GetInt32(...) requires the column to be exactly
                // SQL `int`. If "status" (or any other numeric column here) is
                // actually tinyint/smallint/bigint/decimal, GetInt32 throws
                // InvalidCastException ("Unable to cast object of type 'Byte' to
                // type 'Int32'"). Convert.ToInt32(...) accepts any numeric
                // source type and converts safely, so it's used everywhere below
                // instead of GetInt32.
                var status = Convert.ToInt32(reader["status"]);
                var msg = reader["msg"]?.ToString();

                // Invalid roll code/no: this result set only has status + msg.
                // Return immediately — reading any other column here would throw.
                if (status != 1)
                {
                    return new CertificateStudentResult { Status = status, Msg = msg };
                }

                var student = new CertificateStudentResult
                {
                    Status = status,
                    Msg = msg,
                    RollCode = reader["RollCode"]?.ToString(),
                    RollNo = reader["rollno"]?.ToString(),
                    RegistrationNo = reader["RegistrationNo"]?.ToString(),
                    BsebUniqueID = reader["BsebUniqueID"]?.ToString(),
                    NameoftheCandidate = reader["NameoftheCandidate"]?.ToString(),
                    FathersName = reader["FathersName"]?.ToString(),
                    MothersName = reader["MothersName"]?.ToString(),
                    Gender = reader["Gender"]?.ToString(),
                    CollegeName = reader["CollegeName"]?.ToString(),
                    DistrictName = reader["DistrictName"]?.ToString(),
                    Faculty = reader["FACULTY"]?.ToString(),
                    Division = reader["DIVISION"]?.ToString(),
                    TotalMarks = reader["TotalMarks"]?.ToString(),
                    Nationality = reader["Nationality"]?.ToString(),
                    ExamType = reader["ExamType"]?.ToString()
                };

                while (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var SubjectPaperGroupId = reader["SubjectPaperGroupId"];

                        student.Subjects.Add(new CertificateSubject
                        {
                            SubjectName = reader["SubjectName"]?.ToString(),
                            SubjectPaperCode = reader["SubjectPaperCode"]?.ToString(),
                            SubjectGroupName = reader["SubjectGroupName"]?.ToString(),
                            SubjectPaperGroupId = (SubjectPaperGroupId == DBNull.Value) ? null : Convert.ToInt32(SubjectPaperGroupId)
                        });
                    }
                }

                return student;
            }
            catch (Exception)
            {
                throw;
            }
        }
       



        public async Task BulkSaveAnnualOriginalEXAMQREncData(List<AnnualOriginalEXAMQREncdData> records)
        {
            if (records == null || records.Count == 0)
                return;

            using var conn = await OpenConnectionAsync(_dbOriginalCertiQRData, nameof(_dbOriginalCertiQRData));

            using var transaction = conn.BeginTransaction();
            try
            {
                var dt = new DataTable();
                dt.Columns.Add("RollCode", typeof(string));
                dt.Columns.Add("RollNo", typeof(string));
                dt.Columns.Add("EncryptedData", typeof(string));
                dt.Columns.Add("QRLength", typeof(int));

                foreach (var r in records)
                    dt.Rows.Add(r.RollCode, r.RollNo, r.EncryptedData, r.QRLength);

                using var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.Default, transaction)
                {
                    DestinationTableName = "[OriginalCertiQRData].[dbo].[Annual26OrgQRdata]",
                    BatchSize = 2000,
                    BulkCopyTimeout = 600,
                    EnableStreaming = true
                };

                bulk.ColumnMappings.Add("RollCode", "RollCode");
                bulk.ColumnMappings.Add("RollNo", "RollNo");
                bulk.ColumnMappings.Add("EncryptedData", "EncryptedData");
                bulk.ColumnMappings.Add("QRLength", "QRLength");

                await bulk.WriteToServerAsync(dt);
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ✅ UPDATED: uses OpenConnectionAsync, opens the connection exactly once.
        public async Task BulkSaveCompartAnnualOriginalEXAMQREncData(List<AnnualOriginalEXAMQREncdData> records)
        {
            if (records == null || records.Count == 0)
                return;

            using var conn = await OpenConnectionAsync(_dbOriginalCertiQRData, nameof(_dbOriginalCertiQRData));

            using var transaction = conn.BeginTransaction();

            try
            {
                var dt = new DataTable();
                dt.Columns.Add("RollCode", typeof(string));
                dt.Columns.Add("RollNo", typeof(string));
                dt.Columns.Add("EncryptedData", typeof(string));
                dt.Columns.Add("QRLength", typeof(int));

                foreach (var r in records)
                {
                    dt.Rows.Add(r.RollCode, r.RollNo, r.EncryptedData, r.QRLength);
                }

                using var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.Default, transaction)
                {
                    DestinationTableName = "[OriginalCertiQRData].[dbo].[SpclCompart26OrgQRdata]",
                    BatchSize = 2000,
                    BulkCopyTimeout = 600,
                    EnableStreaming = true
                };

                bulk.ColumnMappings.Add("RollCode", "RollCode");
                bulk.ColumnMappings.Add("RollNo", "RollNo");
                bulk.ColumnMappings.Add("EncryptedData", "EncryptedData");
                bulk.ColumnMappings.Add("QRLength", "QRLength");

                await bulk.WriteToServerAsync(dt);

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
        #endregion
}
