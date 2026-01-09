using BSEBExamResult_QRGenerate.Data;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using QRCoder;
using System.IO.Compression;

namespace BSEBExamResult_QRGenerate.Controllers
{
    public class QRGenerate : Controller
    {
        private readonly DbHelper _dbHelper;

        public QRGenerate(AppDBContext context)
        {
            _dbHelper = new DbHelper(context);
        }
        //[HttpGet]
        //public IActionResult GenerateQRCode()
        //{
        //    // 🔹 Static student data
        //    var student = new
        //    {
        //        Status = 1,
        //        RollCode = "16031",
        //        RollNo = "25010009",
        //        NameoftheCandidate = "KHUSHEE KUMARI",
        //        FathersName = "VINOD KUMAR",
        //        CollegeName = "PT. DEVNATH PANDEY +2 HIGH SCHOOL BARE, KAIMUR",
        //        Faculty = "SCIENCE",
        //        TotalAggregateMarkinNumber = 335,
        //        Division = "FIRST",
        //        SubjectResults = new List<object>
        //{
        //    new
        //    {
        //        Sub = "English",
        //        MaxMark = 100,
        //        PassMark = 30,
        //        Theory = 73,
        //        OB_PR = 0,
        //        GRC_THO = 0,
        //        GRC_PR = 0,
        //        TotSub = "73"
        //    },
        //    new
        //    {
        //        Sub = "Hindi",
        //        MaxMark = 100,
        //        PassMark = 30,
        //        Theory = 80,
        //        OB_PR = 0,
        //        GRC_THO = 0,
        //        GRC_PR = 0,
        //        TotSub = "80 D"
        //    },
        //    new
        //    {
        //        Sub = "Physics",
        //        MaxMark = 100,
        //        PassMark = 33,
        //        Theory = 39,
        //        OB_PR = 22,
        //        GRC_THO = 0,
        //        GRC_PR = 0,
        //        TotSub = "61"
        //    },
        //    new
        //    {
        //        Sub = "Chemistry",
        //        MaxMark = 100,
        //        PassMark = 33,
        //        Theory = 37,
        //        OB_PR = 16,
        //        GRC_THO = 0,
        //        GRC_PR = 0,
        //        TotSub = "53"
        //    },
        //    new
        //    {
        //        Sub = "Biology",
        //        MaxMark = 100,
        //        PassMark = 33,
        //        Theory = 43,
        //        OB_PR = 25,
        //        GRC_THO = 0,
        //        GRC_PR = 0,
        //        TotSub = "68"
        //    },
        //    new
        //    {
        //        Sub = "Mathematics",
        //        MaxMark = 100,
        //        PassMark = 30,
        //        Theory = 17,
        //        OB_PR = 0,
        //        GRC_THO = 0,
        //        GRC_PR = 0,
        //        TotSub = "17 F"
        //    }
        //}
        //    };

        //    if (student.Status != 1)
        //        return Content("Invalid Student");

        //    // 🔹 QR DTO
        //    var qrDto = new
        //    {
        //        student.RollCode,
        //        student.RollNo,
        //        student.NameoftheCandidate,
        //        student.FathersName,
        //        student.CollegeName,
        //        student.Faculty,
        //        student.TotalAggregateMarkinNumber,
        //        student.Division,
        //        Subjects = student.SubjectResults
        //    };

        //    var qrJson = JsonConvert.SerializeObject(qrDto);

        //    // 🔐 Encrypt QR payload
        //    var encryptedPayload = EncryptionHelper.Encrypt(qrJson);

        //    using var generator = new QRCodeGenerator();
        //    using var qrData = generator.CreateQrCode(encryptedPayload, QRCodeGenerator.ECCLevel.Q);
        //    using var qrCode = new QRCode(qrData);
        //    using var bitmap = qrCode.GetGraphic(20);
        //    using var ms = new MemoryStream();

        //    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        //    return File(ms.ToArray(), "image/png");
        //}

        [HttpGet]
        public async Task<IActionResult> GenerateQRCode(string rollcode, string rollno)
        {
            if (string.IsNullOrEmpty(rollcode) || string.IsNullOrEmpty(rollno))
                return BadRequest("RollCode and RollNo required");

            var student = await _dbHelper.GetStudentResultAsync(rollcode, rollno);

            if (student == null || student.Status != 1)
                return Content("Invalid RollCode or RollNo");

            var qrDto = new
            {
                student.RollCode,
                student.RollNo,
                student.NameoftheCandidate,
                student.FathersName,
                student.CollegeName,
                student.Faculty,
                student.TotalAggregateMarkinNumber,
                student.Division,
                Subjects = student.SubjectResults.Select(s => new
                {
                    s.Sub,
                    s.MaxMark,
                    s.PassMark,
                    s.Theory,
                    s.OB_PR,
                    s.GRC_THO,
                    s.GRC_PR,
                    s.TotSub
                })
            };

            var qrJson = JsonConvert.SerializeObject(qrDto);

            // 🔐 Encrypt before QR
            var encryptedPayload = EncryptionHelper.Encrypt(qrJson);
            using var generator = new QRCodeGenerator();
            using var qrData = generator.CreateQrCode(encryptedPayload, QRCodeGenerator.ECCLevel.Q);

            //using var qrData = generator.CreateQrCode(qrJson, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCode(qrData);
            using var bitmap = qrCode.GetGraphic(20);
            using var ms = new MemoryStream();

            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return File(ms.ToArray(), "image/png");
        }

        [HttpGet]
        public async Task<IActionResult> GenerateBulkQRCode()
        {
            var students = await _dbHelper.GetStudentsForQRAsync();

            if (!students.Any())
                return Content("No students found");

            using var zipStream = new MemoryStream();

            using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                foreach (var student in students)
                {
                    if (student.Status != 1)
                        continue;

                    var qrDto = new
                    {
                        student.RollCode,
                        student.RollNo,
                        student.NameoftheCandidate,
                        student.FathersName,
                        student.CollegeName,
                        student.Faculty,
                        student.TotalAggregateMarkinNumber,
                        student.Division
                    };

                    var qrJson = JsonConvert.SerializeObject(qrDto);
                    var encryptedPayload = EncryptionHelper.Encrypt(qrJson);

                    using var generator = new QRCodeGenerator();
                    using var qrData = generator.CreateQrCode(encryptedPayload, QRCodeGenerator.ECCLevel.Q);
                    using var qrCode = new QRCode(qrData);
                    using var bitmap = qrCode.GetGraphic(20);

                    using var ms = new MemoryStream();
                    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);

                    var entry = zip.CreateEntry($"{student.RollCode}_{student.RollNo}.png");
                    using var entryStream = entry.Open();
                    ms.Position = 0;
                    ms.CopyTo(entryStream);
                }
            }

            zipStream.Position = 0;
            return File(zipStream.ToArray(), "application/zip", "Student_QR_Codes.zip");
        }
    }
}
