using BSEBExamResult_QRGenerate.Data;
using BSEBExamResult_QRGenerate.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
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
        private readonly ILogger<QRGenerateController> _logger;

        public QRGenerateController(AppDBContext context, ILogger<QRGenerateController> logger)
        {
            _dbHelper = new DbHelper(context);
            _logger = logger;
        }


        // ✅ Bulk encrypt ALL students and save to DB
        [HttpPost("GenerateAndSaveAllEncrypted")]
        public async Task<IActionResult> GenerateAndSaveAllEncrypted()
        {
            _logger.LogInformation("Bulk QR Encryption started at {Time}", DateTime.Now);

            // Step 1: Get all RollCode + RollNo pairs directly from table
            var allRolls = await _dbHelper.GetAllRollCodesAsync();
            _logger.LogInformation("Total students fetched: {Count}", allRolls.Count);

            int successCount = 0;
            int failCount = 0;
            int skippedCount = 0;

            const int BATCH_SIZE = 5000;
            var batch = new List<QREncryptedData>(BATCH_SIZE);

            foreach (var (rollCode, rollNo) in allRolls)
            {
                try
                {
                    // Step 2: Get full student data via LoginSp
                    var student = await _dbHelper.GetStudentResultAsync(rollCode, rollNo);

                    if (student == null || student.Status != 1)
                    {
                        skippedCount++;
                        continue;
                    }

                    // Step 3: Encrypt full student object
                  //  string encrypted = QrUtility.GenerateEncryptedPayloadFull(student);
                    string encrypted = QrUtility.GenerateEncryptedPayloadCompact(student);
                    // var qrPath = GenerateQrImage(encrypted, rollNo, rollCode);
                    int encryptedLength = encrypted.Length;
                    //if (qrPath == null)
                    //{
                    //    skippedCount++;
                    //    continue;
                    //}

                    // ✅ Only valid records go to DB
                    batch.Add(new QREncryptedData
                    {
                        RollCode = rollCode,
                        RollNo = rollNo,
                        EncryptedData = encrypted,
                        Length = encryptedLength,
                        // QrPath = "",
                        CreatedOn = DateTime.Now
                    });

                    successCount++;

                    // Step 5: Flush batch to DB every 5000 records
                    if (batch.Count >= BATCH_SIZE)
                    {
                        await _dbHelper.BulkSaveEncryptedDataAsync(batch);
                        _logger.LogInformation("Flushed batch. Total saved so far: {Count}", successCount);
                        batch.Clear();
                    }
                }
                catch (Exception ex)
                {
                    failCount++;
                    _logger.LogError(ex, "Failed for RollCode: {RC}, RollNo: {RN}", rollCode, rollNo);
                }
            }

            // Step 6: Save any remaining records
            if (batch.Count > 0)
            {
                await _dbHelper.BulkSaveEncryptedDataAsync(batch);
                batch.Clear();
            }

            _logger.LogInformation(
                "Done. Success: {S}, Failed: {F}, Skipped: {SK}",
                successCount, failCount, skippedCount);

            return Ok(new
            {
                message = "Bulk encryption complete",
                total = allRolls.Count,
                success = successCount,
                failed = failCount,
                skipped = skippedCount
            });
        }


        private string GenerateQrImage(string encrypted, string rollNo, string rollCode)
        {
            string basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string qrFolder = Path.Combine(basePath, "qr");

            Directory.CreateDirectory(qrFolder);

            string qrPayload = encrypted;

            if (qrPayload.Length > 2000)
                return null;

            using var qrGenerator = new QRCodeGenerator();
            var qrData = qrGenerator.CreateQrCode(qrPayload, QRCodeGenerator.ECCLevel.L);

            // Set dimension to 100 px
            int targetPixels = 100;

            int moduleCount = qrData.ModuleMatrix.Count;
            int pixelsPerModule = Math.Max(1, targetPixels / moduleCount);

            using var qrCode = new QRCode(qrData);
            using Bitmap qrImage = qrCode.GetGraphic(pixelsPerModule);

            // Force exact 100x100 size
            using Bitmap finalImage = new Bitmap(targetPixels, targetPixels);
            finalImage.SetResolution(300, 300);

            using (Graphics g = Graphics.FromImage(finalImage))
            {
                g.Clear(Color.White);
                g.DrawImage(qrImage, 0, 0, targetPixels, targetPixels);
            }

            string fileName = $"{rollNo}_{rollCode}.png";
            string filePath = Path.Combine(qrFolder, fileName);

            finalImage.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);

            return filePath;
        }


        //working code
        //private string GenerateQrImage(string encrypted, string rollNo, string rollCode)
        //{
        //    string basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        //    string qrFolder = Path.Combine(basePath, "qr");

        //    Directory.CreateDirectory(qrFolder);

        //    //string qrPayload = $"http://115.243.18.52/t1/interResult.aspx?enc={encrypted}";
        //    string qrPayload = encrypted;

        //    if (qrPayload.Length > 2000)
        //        return null;

        //    using var qrGenerator = new QRCodeGenerator();
        //    //var qrData = qrGenerator.CreateQrCode(qrPayload, QRCodeGenerator.ECCLevel.M);
        //    var qrData = qrGenerator.CreateQrCode(qrPayload, QRCodeGenerator.ECCLevel.L);


        //    using var qrCode = new QRCode(qrData);
        //    using Bitmap qrImage = qrCode.GetGraphic(2);
        //    //using Bitmap qrImage = qrCode.GetGraphic(2);

        //    string fileName = $"{rollNo}_{rollCode}.png";
        //    string filePath = Path.Combine(qrFolder, fileName);

        //    qrImage.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);

        //    return filePath;
        //}




        [HttpGet("GenerateQRCodeOptimized")]
        public async Task<IActionResult> GenerateQRCodeOptimized(string rollno)
        {
            if (string.IsNullOrEmpty(rollno))
                return BadRequest("RollNo required");

            var rollCodes = await _dbHelper.GetRollCodesByRollNoAsync(rollno);
            if (!rollCodes.Any())
                return Content("No data found");

            string basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string qrFolder = Path.Combine(basePath, "qr");

            Directory.CreateDirectory(qrFolder);

            var filePaths = new List<string>();

            foreach (var rc in rollCodes)
            {
                var student = await _dbHelper.GetStudentResultAsync(rc, rollno);

                if (student == null || student.Status != 1)
                    continue;

                // 🔐 Encrypted payload
                //string encrypted = QrUtility.GenerateEncryptedPayload(student);
                string encrypted = QrUtility.GenerateEncryptedPayloadFull(student);

                // 🔗 URL inside QR
                string qrPayload = $"http://115.243.18.52/t1/interResult.aspx?enc={encrypted}";

                // ⚠️ Length check
                if (qrPayload.Length > 2000)
                    continue;

                using var qrGenerator = new QRCodeGenerator();
                var qrData = qrGenerator.CreateQrCode(qrPayload, QRCodeGenerator.ECCLevel.M);

                using var qrCode = new QRCode(qrData);
                using Bitmap qrImage = qrCode.GetGraphic(2);

                string fileName = $"{student.RollNo}_{student.RollCode}.png";
                string filePath = Path.Combine(qrFolder, fileName);

                qrImage.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);

                filePaths.Add(filePath);
            }

            return Ok(new
            {
                message = "QR Generated Successfully",
                count = filePaths.Count
            });
        }

        // below code is old
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
                //student.dob,
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


        [HttpGet("GenerateQRCodeWithCSV")] // save in CSV file multiple QR with CSV and ZIP Download this is my old code 
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
                    //if (student == null || student.Status != 1)
                    continue;

                //var modified = new
                //{
                //    RollCode = student.RollCode,
                //    RollNo = student.RollNo,
                //    BsebUniqueID = student.BsebUniqueID,

                //    RegistrationNo = student.RegistrationNo,
                //    NameoftheCandidate = student.NameoftheCandidate,
                //    FathersName = student.FathersName,
                //    CollegeName = student.CollegeName,
                //    Faculty = student.Faculty,
                //    TotalAggregateMarkinNumber = student.TotalAggregateMarkinNumber,
                //    TotalAggregateMarkinWords = student.TotalAggregateMarkinWords,
                //    Division = student.Division,

                //    SubjectResults = student.SubjectResults.Select(sub => new
                //    {
                //        Sub = sub.Sub,
                //        MaxMark = sub.MaxMark,
                //        PassMark = sub.PassMark,
                //        Theory = sub.Theory,
                //        OB_PR = sub.OB_PR,
                //        GRC_THO = sub.GRC_THO,
                //        GRC_PR = sub.GRC_PR,
                //        TotSub = sub.TotSub,
                //        CCEMarks = sub.CCEMarks,

                //        // 🔹 Add SubjectGroupCode based on SubjectGroupName
                //        SubjectGroupCode =
                //            sub.SubjectGroupName != null && sub.SubjectGroupName.Contains("अनिवार्य") ? 1 :
                //            sub.SubjectGroupName != null && sub.SubjectGroupName.Contains("Elective") ? 2 : 3
                //    })
                //};

                //var json = JsonConvert.SerializeObject(modified);
                var json = JsonConvert.SerializeObject(student);
                var compressed = CompressionHelper.Compress(json);
                var encrypted = EncryptionHelper.Encrypt(compressed);
                // 🔹 Build full URL with encrypted data
                string qrPayload = $"http://115.243.18.52/t1/interResult.aspx?enc={encrypted}"; // test live url
                                                                                                //string qrPayload = $"https://interresult-25.biharboardexam.com/interResult.aspx?enc={encrypted}"; // live url

                // Generate QR
                // Generate QR
                using var generator = new QRCodeGenerator();
                using var qrData = generator.CreateQrCode(qrPayload, QRCodeGenerator.ECCLevel.Q);
                using var qrCode = new QRCode(qrData);
                using var bitmap = qrCode.GetGraphic(1); // ✅ Changed from 5 → 10 for Version 2 size

                string qrFile = $"{rollno}_{rc}.png";
                string qrPath = Path.Combine(qrFolder, qrFile);
                bitmap.Save(qrPath, System.Drawing.Imaging.ImageFormat.Png);

                csvRows.Add($"{rollno},{rc},\"{qrPayload}\"");
                //csvRows.Add($"{rollno},{rc},\"{encrypted}\"");
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

        //[HttpGet("GenerateQRCodeOptimized")]
        //public async Task<IActionResult> GenerateQRCodeOptimized(string rollno)
        //{
        //    if (string.IsNullOrEmpty(rollno))
        //        return BadRequest("RollNo required");

        //    var rollCodes = await _dbHelper.GetRollCodesByRollNoAsync(rollno);
        //    if (!rollCodes.Any())
        //        return Content("No data found");

        //    string basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        //    string qrFolder = Path.Combine(basePath, "qr");

        //    Directory.CreateDirectory(qrFolder);

        //    var filePaths = new List<string>();

        //    foreach (var rc in rollCodes)
        //    {
        //        var student = await _dbHelper.GetStudentResultAsync(rc, rollno);

        //        if (student == null || student.Status != 1)
        //            continue;

        //        // 🔹 Generate optimized QR data
        //        string finalData = QrUtility.GenerateFinalQrData(student);

        //        // 🔥 Safety check (VERY IMPORTANT)
        //        if (finalData.Length > 1200)
        //            continue; // skip large data (prevents scan failure)

        //        // 🔹 Generate QR
        //        using var qrGenerator = new QRCodeGenerator();
        //        var qrData = qrGenerator.CreateQrCode(finalData, QRCodeGenerator.ECCLevel.L);

        //        using var qrCode = new QRCode(qrData);
        //        using Bitmap qrImage = qrCode.GetGraphic(8);

        //        string fileName = $"{student.RollNo}_{student.RollCode}.png";
        //        string filePath = Path.Combine(qrFolder, fileName);

        //        qrImage.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);

        //        filePaths.Add(filePath);
        //    }

        //    return Ok(new
        //    {
        //        message = "QR Generated Successfully",
        //        count = filePaths.Count
        //    });
        //}

        //[HttpGet("GenerateQRCodeWithCSV")] // save in CSV file multiple QR with CSV and ZIP Download this is my old code  jinal 3:15
        //public async Task<IActionResult> GenerateQRCodeWithCSV(string rollno)
        //{
        //    if (string.IsNullOrEmpty(rollno))
        //        return BadRequest("RollNo required");

        //    var rollCodes = await _dbHelper.GetRollCodesByRollNoAsync(rollno);
        //    if (!rollCodes.Any())
        //        return Content("No data found");

        //    var csvRows = new List<string>
        //        {
        //            "RollNo,RollCode,EncryptedValue"
        //        };

        //    string basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        //    string qrFolder = Path.Combine(basePath, "qr");
        //    string csvFolder = Path.Combine(basePath, "csv");
        //    string zipFolder = Path.Combine(basePath, "zip");

        //    Directory.CreateDirectory(qrFolder);
        //    Directory.CreateDirectory(csvFolder);
        //    Directory.CreateDirectory(zipFolder);

        //    foreach (var rc in rollCodes)
        //    {
        //        var student = await _dbHelper.GetStudentResultAsync(rc, rollno);
        //        if (student == null || student.Status != 1)
        //            continue;

        //        var json = JsonConvert.SerializeObject(student);
        //        var jsondata = json;

        //        var compressed = CompressionHelper.Compress(json);
        //        var encrypted = EncryptionHelper.Encrypt(compressed);
        //        // 🔹 Build full URL with encrypted data
        //        string qrPayload = $"http://115.243.18.52/t1/interResult.aspx?enc={encrypted}"; // test live url
        //     //string qrPayload = $"https://interresult-25.biharboardexam.com/interResult.aspx?enc={encrypted}"; // live url

        //        // Generate QR
        //        // Generate QR
        //        using var generator = new QRCodeGenerator();
        //        using var qrData = generator.CreateQrCode(qrPayload, QRCodeGenerator.ECCLevel.Q);
        //        using var qrCode = new QRCode(qrData);
        //        using var bitmap = qrCode.GetGraphic(1); // ✅ Changed from 5 → 10 for Version 2 size

        //        string qrFile = $"{rollno}_{rc}.png";
        //        string qrPath = Path.Combine(qrFolder, qrFile);
        //        bitmap.Save(qrPath, System.Drawing.Imaging.ImageFormat.Png);

        //        csvRows.Add($"{rollno},{rc},\"{qrPayload}\"");
        //        //csvRows.Add($"{rollno},{rc},\"{encrypted}\"");
        //    }

        //    if (csvRows.Count == 1)
        //        return Content("No valid result");

        //    // Save CSV
        //    string csvFile = $"qr_{rollno}.csv";
        //    string csvPath = Path.Combine(csvFolder, csvFile);
        //    System.IO.File.WriteAllLines(csvPath, csvRows);

        //    // Create ZIP
        //    string zipFileName = $"QR_{rollno}_{DateTime.Now:yyyyMMddHHmmss}.zip";
        //    string zipPath = Path.Combine(zipFolder, zipFileName);

        //    using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        //    {
        //        foreach (var file in Directory.GetFiles(qrFolder, $"{rollno}_*.png"))
        //            zip.CreateEntryFromFile(file, Path.GetFileName(file));

        //        zip.CreateEntryFromFile(csvPath, csvFile);
        //    }

        //    byte[] zipBytes = await System.IO.File.ReadAllBytesAsync(zipPath);

        //    return File(zipBytes, "application/zip", zipFileName);
        //}

        //[HttpGet("GenerateQRCodeWithCSV")] // save in CSV file multiple QR with CSV and ZIP Download this is my old code 
        //public async Task<IActionResult> GenerateQRCodeWithCSV(string rollno)
        //{
        //    if (string.IsNullOrEmpty(rollno))
        //        return BadRequest("RollNo required");

        //    var rollCodes = await _dbHelper.GetRollCodesByRollNoAsync(rollno);
        //    if (!rollCodes.Any())
        //        return Content("No data found");

        //    var csvRows = new List<string>
        //        {
        //            "RollNo,RollCode,EncryptedValue"
        //        };

        //    string basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        //    string qrFolder = Path.Combine(basePath, "qr");
        //    string csvFolder = Path.Combine(basePath, "csv");
        //    string zipFolder = Path.Combine(basePath, "zip");

        //    Directory.CreateDirectory(qrFolder);
        //    Directory.CreateDirectory(csvFolder);
        //    Directory.CreateDirectory(zipFolder);

        //    foreach (var rc in rollCodes)
        //    {
        //        var student = await _dbHelper.GetStudentResultAsync(rc, rollno);
        //        if (student == null || student.Status != 1)
        //            continue;

        //        var json = JsonConvert.SerializeObject(student);
        //        var compressed = CompressionHelper.Compress(json);
        //        var encrypted = EncryptionHelper.Encrypt(compressed);
        //        // 🔹 Build full URL with encrypted data
        //        string qrPayload = $"http://115.243.18.52/t1/interResult.aspx?enc={encrypted}"; // test live url
        //        //string qrPayload = $"https://interresult-25.biharboardexam.com/interResult.aspx?enc={encrypted}"; // live url

        //        // Generate QR
        //        using var generator = new QRCodeGenerator();
        //        using var qrData = generator.CreateQrCode(qrPayload, QRCodeGenerator.ECCLevel.L);

        //        //using var qrData = generator.CreateQrCode(encrypted, QRCodeGenerator.ECCLevel.L);
        //        using var qrCode = new QRCode(qrData);
        //        using var bitmap = qrCode.GetGraphic(25);

        //        string qrFile = $"{rollno}_{rc}.png";
        //        string qrPath = Path.Combine(qrFolder, qrFile);
        //        bitmap.Save(qrPath, System.Drawing.Imaging.ImageFormat.Png);

        //        csvRows.Add($"{rollno},{rc},\"{qrPayload}\"");
        //        //csvRows.Add($"{rollno},{rc},\"{encrypted}\"");
        //    }

        //    if (csvRows.Count == 1)
        //        return Content("No valid result");

        //    // Save CSV
        //    string csvFile = $"qr_{rollno}.csv";
        //    string csvPath = Path.Combine(csvFolder, csvFile);
        //    System.IO.File.WriteAllLines(csvPath, csvRows);

        //    // Create ZIP
        //    string zipFileName = $"QR_{rollno}_{DateTime.Now:yyyyMMddHHmmss}.zip";
        //    string zipPath = Path.Combine(zipFolder, zipFileName);

        //    using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        //    {
        //        foreach (var file in Directory.GetFiles(qrFolder, $"{rollno}_*.png"))
        //            zip.CreateEntryFromFile(file, Path.GetFileName(file));

        //        zip.CreateEntryFromFile(csvPath, csvFile);
        //    }

        //    byte[] zipBytes = await System.IO.File.ReadAllBytesAsync(zipPath);

        //    return File(zipBytes, "application/zip", zipFileName);
        //}

        //[HttpGet("GenerateQRCodeWithCSV")]
        //public async Task<IActionResult> GenerateQRCodeWithCSV(string rollno)
        //{
        //    if (string.IsNullOrEmpty(rollno))
        //        return BadRequest("RollNo required");

        //    var rollCodes = await _dbHelper.GetRollCodesByRollNoAsync(rollno);
        //    if (!rollCodes.Any())
        //        return Content("No data found");

        //    var csvRows = new List<string> { "RollNo,RollCode,EncryptedQRPayload" };

        //    string basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        //    string qrFolder = Path.Combine(basePath, "qr");
        //    string csvFolder = Path.Combine(basePath, "csv");
        //    string zipFolder = Path.Combine(basePath, "zip");

        //    Directory.CreateDirectory(qrFolder);
        //    Directory.CreateDirectory(csvFolder);
        //    Directory.CreateDirectory(zipFolder);

        //    foreach (var rc in rollCodes)
        //    {
        //        var student = await _dbHelper.GetStudentResultAsync(rc, rollno);
        //        if (student == null || student.Status != 1)
        //            continue;

        //        // ✅ STEP 1 — Full student → Minified JSON
        //        var jsonSettings = new JsonSerializerSettings
        //        {
        //            NullValueHandling = NullValueHandling.Ignore,
        //            DefaultValueHandling = DefaultValueHandling.Ignore
        //        };
        //        string json = JsonConvert.SerializeObject(student, Formatting.None, jsonSettings);

        //        // ✅ STEP 2 — Minified JSON → GZip compressed byte[]
        //        byte[] compressed = CompressionHelper.Compress(json);

        //        // ✅ STEP 3 — compressed byte[] → AES encrypted byte[] (IV prepended)
        //        byte[] encrypted = EncryptionHelper.Encrypt(compressed);

        //        // ✅ STEP 4 — encrypted byte[] → Base64 string
        //        string base64Payload = Convert.ToBase64String(encrypted);

        //        // ✅ STEP 5 — Build QR URL
        //        string qrPayload = $"http://115.243.18.52/t1/interResult.aspx?enc={Uri.EscapeDataString(base64Payload)}";

        //        // ✅ STEP 6 — Generate clean QR (matches your sample image)
        //        using var generator = new QRCodeGenerator();
        //        using var qrData = generator.CreateQrCode(qrPayload, QRCodeGenerator.ECCLevel.M);
        //        using var qrCode = new QRCode(qrData);
        //        using var bitmap = qrCode.GetGraphic(
        //            pixelsPerModule: 10,
        //            darkColor: Color.Black,
        //            lightColor: Color.White,
        //            drawQuietZones: true
        //        );
        //        using var resized = new Bitmap(bitmap, new Size(180, 180));
        //        resized.SetResolution(180f, 180f);

        //        string qrFile = $"{rollno}{rc}.png";
        //        string qrPath = Path.Combine(qrFolder, qrFile);
        //        resized.Save(qrPath, System.Drawing.Imaging.ImageFormat.Png);

        //        // ✅ Save Base64 in CSV
        //        csvRows.Add($"{rollno},{rc},\"{base64Payload}\"");
        //    }

        //    if (csvRows.Count == 1)
        //        return Content("No valid result");

        //    // ✅ Save CSV
        //    string csvFile = $"qr{rollno}.csv";
        //    string csvPath = Path.Combine(csvFolder, csvFile);
        //    System.IO.File.WriteAllLines(csvPath, csvRows);

        //    // ✅ Create ZIP
        //    string zipFileName = $"QR_{rollno}{DateTime.Now:yyyyMMddHHmmss}.zip";
        //    string zipPath = Path.Combine(zipFolder, zipFileName);

        //    using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        //    {
        //        foreach (var file in Directory.GetFiles(qrFolder, $"{rollno}*.png"))
        //            zip.CreateEntryFromFile(file, Path.GetFileName(file));
        //        zip.CreateEntryFromFile(csvPath, csvFile);
        //    }

        //    byte[] zipBytes = await System.IO.File.ReadAllBytesAsync(zipPath);
        //    return File(zipBytes, "application/zip", zipFileName);
        //}

        //[HttpGet("GenerateQRCodeWithCSV")] // save in CSV file as are requ QR small
        //public async Task<IActionResult> GenerateQRCodeWithCSV(string rollno)
        //{
        //    if (string.IsNullOrEmpty(rollno))
        //        return BadRequest("RollNo required");

        //    var rollCodes = await _dbHelper.GetRollCodesByRollNoAsync(rollno);
        //    if (!rollCodes.Any())
        //        return Content("No data found");

        //    var csvRows = new List<string>
        //{
        //    "RollNo,RollCode,EncryptedQRPayload"
        //};

        //    string basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        //    string qrFolder = Path.Combine(basePath, "qr");
        //    string csvFolder = Path.Combine(basePath, "csv");
        //    string zipFolder = Path.Combine(basePath, "zip");

        //    Directory.CreateDirectory(qrFolder);
        //    Directory.CreateDirectory(csvFolder);
        //    Directory.CreateDirectory(zipFolder);

        //    foreach (var rc in rollCodes)
        //    {
        //        var student = await _dbHelper.GetStudentResultAsync(rc, rollno);
        //        if (student == null || student.Status != 1)
        //            continue;

        //        // 🔹 Serialize, compress, and encrypt the student info + rc + rollno
        //        var payloadObject = new
        //        {
        //            RollNo = rollno,
        //            RollCode = rc
        //        };
        //        var json = JsonConvert.SerializeObject(payloadObject);
        //        var compressed = CompressionHelper.Compress(json);
        //        var encrypted = EncryptionHelper.Encrypt(compressed);

        //        // 🔹 QR payload: only encrypted string
        //        string qrPayload = $"http://115.243.18.52/t1/interResult.aspx?enc={Uri.EscapeDataString(encrypted)}";

        //        // 🔹 Generate QR — small, clean
        //        using var generator = new QRCodeGenerator();
        //        using var qrData = generator.CreateQrCode(qrPayload, QRCodeGenerator.ECCLevel.L);
        //        using var qrCode = new QRCode(qrData);

        //        using var bitmap = qrCode.GetGraphic(3, Color.Black, Color.White, drawQuietZones: true);
        //        using var resized = new Bitmap(bitmap, new Size(150, 150));
        //        resized.SetResolution(150f, 150f);

        //        string qrFile = $"{rollno}_{rc}.png";
        //        string qrPath = Path.Combine(qrFolder, qrFile);
        //        resized.Save(qrPath, System.Drawing.Imaging.ImageFormat.Png);

        //        // 🔹 Save encrypted value in CSV
        //        csvRows.Add($"{rollno},{rc},\"{encrypted}\"");
        //    }

        //    if (csvRows.Count == 1)
        //        return Content("No valid result");

        //    // 🔹 Save CSV
        //    string csvFile = $"qr_{rollno}.csv";
        //    string csvPath = Path.Combine(csvFolder, csvFile);
        //    System.IO.File.WriteAllLines(csvPath, csvRows);

        //    // 🔹 Create ZIP
        //    string zipFileName = $"QR_{rollno}_{DateTime.Now:yyyyMMddHHmmss}.zip";
        //    string zipPath = Path.Combine(zipFolder, zipFileName);

        //    using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        //    {
        //        foreach (var file in Directory.GetFiles(qrFolder, $"{rollno}_*.png"))
        //            zip.CreateEntryFromFile(file, Path.GetFileName(file));

        //        zip.CreateEntryFromFile(csvPath, csvFile);
        //    }

        //    byte[] zipBytes = await System.IO.File.ReadAllBytesAsync(zipPath);
        //    return File(zipBytes, "application/zip", zipFileName);
        //}



        //[HttpGet("GenerateQRCodeWithCSV")] // save in CSV file
        //public async Task<IActionResult> GenerateQRCodeWithCSV(string rollno)
        //{
        //    if (string.IsNullOrEmpty(rollno))
        //        return BadRequest("RollNo required");

        //    var rollCodes = await _dbHelper.GetRollCodesByRollNoAsync(rollno);
        //    if (!rollCodes.Any())
        //        return Content("No data found");

        //    var csvRows = new List<string>
        //{
        //    "RollNo,RollCode,QRPayload"
        //};

        //    string basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        //    string qrFolder = Path.Combine(basePath, "qr");
        //    string csvFolder = Path.Combine(basePath, "csv");
        //    string zipFolder = Path.Combine(basePath, "zip");

        //    Directory.CreateDirectory(qrFolder);
        //    Directory.CreateDirectory(csvFolder);
        //    Directory.CreateDirectory(zipFolder);

        //    foreach (var rc in rollCodes)
        //    {
        //        var student = await _dbHelper.GetStudentResultAsync(rc, rollno);
        //        if (student == null || student.Status != 1)
        //            continue;

        //         ✅ SHORT URL ONLY — rc + rn keeps QR data minimal(~55 chars)
        //         interResult.aspx will fetch result from DB on scan using rc +rn
        //        string qrPayload = $"http://115.243.18.52/t1/interResult.aspx?rc={rc}&rn={rollno}";
        //        string qrPayload = $"https://interresult-25.biharboardexam.com/interResult.aspx?rc={rc}&rn={rollno}";

        //         🔹 Generate QR — short payload = very few modules = small clean QR
        //        using var generator = new QRCodeGenerator();
        //        using var qrData = generator.CreateQrCode(qrPayload, QRCodeGenerator.ECCLevel.L);
        //        using var qrCode = new QRCode(qrData);

        //         🔹 pixelsPerModule = 3, short data = small QR like your reference image
        //        using var bitmap = qrCode.GetGraphic(
        //            3,
        //            Color.Black,
        //            Color.White,
        //            drawQuietZones: true
        //        );

        //         🔹 300px at 300 DPI = exactly 1 inch when printed
        //        using var resized = new Bitmap(bitmap, new Size(150, 150));
        //        resized.SetResolution(150f, 150f);

        //        string qrFile = $"{rollno}_{rc}.png";
        //        string qrPath = Path.Combine(qrFolder, qrFile);
        //        resized.Save(qrPath, System.Drawing.Imaging.ImageFormat.Png);

        //         ✅ Save encrypted value separately in CSV for reference
        //        var json = JsonConvert.SerializeObject(student);
        //        var compressed = CompressionHelper.Compress(json);
        //        var encrypted = EncryptionHelper.Encrypt(compressed);
        //        csvRows.Add($"{rollno},{rc},\"{qrPayload}\",\"{encrypted}\"");
        //    }

        //    if (csvRows.Count == 1)
        //        return Content("No valid result");

        //     🔹 Save CSV
        //    string csvFile = $"qr_{rollno}.csv";
        //    string csvPath = Path.Combine(csvFolder, csvFile);
        //    System.IO.File.WriteAllLines(csvPath, csvRows);

        //     🔹 Create ZIP
        //    string zipFileName = $"QR_{rollno}_{DateTime.Now:yyyyMMddHHmmss}.zip";
        //    string zipPath = Path.Combine(zipFolder, zipFileName);

        //    using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        //    {
        //        foreach (var file in Directory.GetFiles(qrFolder, $"{rollno}_*.png"))
        //            zip.CreateEntryFromFile(file, Path.GetFileName(file));

        //        zip.CreateEntryFromFile(csvPath, csvFile);
        //    }

        //    byte[] zipBytes = await System.IO.File.ReadAllBytesAsync(zipPath);
        //    return File(zipBytes, "application/zip", zipFileName);
        //}

        [HttpGet("DecryptStudent")]
        public IActionResult DecryptStudent(string enc)
        {
            if (string.IsNullOrEmpty(enc))
                return BadRequest("Encrypted value required");

            try
            {
                var student = QrDecryptUtility.DecodeToStudent(enc);

                if (student == null)
                    return BadRequest(new { success = false, message = "Invalid data" });

                return Ok(new { success = true, data = student });
            }
            catch
            {
                return BadRequest(new { success = false, message = "Failed to decrypt QR" });
            }
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
