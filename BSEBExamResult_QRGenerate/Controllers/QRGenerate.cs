using BSEBExamResult_QRGenerate.Data;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using QRCoder;

namespace BSEBExamResult_QRGenerate.Controllers
{
    public class QRGenerate : Controller
    {
        private readonly DbHelper _dbHelper;

        public QRGenerate(AppDBContext context)
        {
            _dbHelper = new DbHelper(context);
        }

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
    }
}
