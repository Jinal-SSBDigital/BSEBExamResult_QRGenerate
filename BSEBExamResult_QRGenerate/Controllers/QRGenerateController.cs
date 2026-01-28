using BSEBExamResult_QRGenerate.Data;
using BSEBExamResult_QRGenerate.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;

namespace BSEBExamResult_QRGenerate.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QRGenerateController : ControllerBase
    {
        private readonly DbHelper _dbHelper;

        public QRGenerateController(AppDBContext context)
        {
            _dbHelper = new DbHelper(context);
        }

        [HttpGet("GenerateSingleQRCode")]// old single QR 
        public async Task<IActionResult> GenerateSingleQRCode(string rollcode, string rollno)
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
                student.BsebUniqueID,
                student.dob,
                student.NameoftheCandidate,
                student.FathersName,
                student.CollegeName,
                student.RegistrationNo,
                student.Faculty,
                student.TotalAggregateMarkinNumber,
                student.TotalAggregateMarkinWords,
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
                    s.CCEMarks,
                    s.TotSub,
                    s.SubjectGroupName
                })
            };

            // 🔹 Serialize
            var qrJson = JsonConvert.SerializeObject(qrDto);

            // 🔹 COMPRESS FIRST
            var compressed = CompressionHelper.Compress(qrJson);

            // 🔹 THEN ENCRYPT
            var encryptedPayload = EncryptionHelper.Encrypt(compressed);

            using var generator = new QRCodeGenerator();

            using var qrData = generator.CreateQrCode(
                encryptedPayload,
                QRCodeGenerator.ECCLevel.L
            );

            using var qrCode = new QRCode(qrData);

            using var bitmap = qrCode.GetGraphic(25);

            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);

            return File(ms.ToArray(), "image/png");
        }


        [HttpGet("GenerateQRCodeWithCSV")] // save in CSV file
        public async Task<IActionResult> GenerateQRCodeWithCSV(string rollno)
        {
            if (string.IsNullOrEmpty(rollno))
                return BadRequest("RollNo required");

            var rollCodes = await _dbHelper.GetRollCodesByRollNoAsync(rollno);
            if (!rollCodes.Any())
                return Content("No data found");

            var csvRows = new List<string>
    {
        "RollNo,RollCode,EncryptedValue"
    };

            string basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string qrFolder = Path.Combine(basePath, "qr");
            string csvFolder = Path.Combine(basePath, "csv");
            string zipFolder = Path.Combine(basePath, "zip");

            Directory.CreateDirectory(qrFolder);
            Directory.CreateDirectory(csvFolder);
            Directory.CreateDirectory(zipFolder);

            foreach (var rc in rollCodes)
            {
                var student = await _dbHelper.GetStudentResultAsync(rc, rollno);
                if (student == null || student.Status != 1)
                    continue;

                var json = JsonConvert.SerializeObject(student);
                var compressed = CompressionHelper.Compress(json);
                var encrypted = EncryptionHelper.Encrypt(compressed);

                // Generate QR
                using var generator = new QRCodeGenerator();
                using var qrData = generator.CreateQrCode(encrypted, QRCodeGenerator.ECCLevel.L);
                using var qrCode = new QRCode(qrData);
                using var bitmap = qrCode.GetGraphic(25);

                string qrFile = $"{rollno}_{rc}.png";
                string qrPath = Path.Combine(qrFolder, qrFile);
                bitmap.Save(qrPath, System.Drawing.Imaging.ImageFormat.Png);

                csvRows.Add($"{rollno},{rc},\"{encrypted}\"");
            }

            if (csvRows.Count == 1)
                return Content("No valid result");

            // Save CSV
            string csvFile = $"qr_{rollno}.csv";
            string csvPath = Path.Combine(csvFolder, csvFile);
            System.IO.File.WriteAllLines(csvPath, csvRows);

            // Create ZIP
            string zipFileName = $"QR_{rollno}_{DateTime.Now:yyyyMMddHHmmss}.zip";
            string zipPath = Path.Combine(zipFolder, zipFileName);

            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (var file in Directory.GetFiles(qrFolder, $"{rollno}_*.png"))
                    zip.CreateEntryFromFile(file, Path.GetFileName(file));

                zip.CreateEntryFromFile(csvPath, csvFile);
            }

            byte[] zipBytes = await System.IO.File.ReadAllBytesAsync(zipPath);

            return File(zipBytes, "application/zip", zipFileName);
        }
        [HttpPost("VerifyQRCode")] // only check encypt QR Data 
        public IActionResult VerifyQRCode([FromBody] QrRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.EncryptedValue))
                return BadRequest("QR data is required");

            try
            {
                // 🔐 STEP 1: DECRYPT FIRST
                var decrypted = EncryptionHelper.Decrypt(request.EncryptedValue);

                // 📦 STEP 2: DECOMPRESS
                var json = CompressionHelper.Decompress(decrypted);

                // 🔁 STEP 3: DESERIALIZE
                var student = JsonConvert.DeserializeObject<dynamic>(json);

                return Ok(student); // or return View(student)
            }
            catch (Exception ex)
            {
                return BadRequest("Invalid or corrupted QR Code");
            }
        }
        [HttpPost("generate")] //Encrypt Text and Generate QR
        public IActionResult GenerateQrEncrypt([FromBody] QrRequest request)
        {
            if (string.IsNullOrEmpty(request.EncryptedValue))
                return BadRequest("Encrypted value is required");


            // Create QR
            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(
            request.EncryptedValue,
            QRCodeGenerator.ECCLevel.Q
            );


            using var qrCode = new QRCode(qrData);
            using Bitmap qrImage = qrCode.GetGraphic(20);


            // Convert image to byte[]
            using var ms = new MemoryStream();
            qrImage.Save(ms, ImageFormat.Png);
            byte[] imageBytes = ms.ToArray();


            return File(imageBytes, "image/png");
        }
    }
}
