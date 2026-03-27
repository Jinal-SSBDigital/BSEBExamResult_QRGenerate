using BSEBExamResult_QRGenerate.Data;
using BSEBExamResult_QRGenerate.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Text;

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

        [HttpGet("GenerateQRCodeWithCSV")]
        public async Task<IActionResult> GenerateQRCodeWithCSV(string rollno)
        {
            if (string.IsNullOrEmpty(rollno))
                return BadRequest("RollNo required");

            var rollCodes = await _dbHelper.GetRollCodesByRollNoAsync(rollno);
            if (!rollCodes.Any())
                return Content("No data found");

            var csvRows = new List<string> { "RollNo,RollCode,EncryptedQRPayload" };

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

                // ✅ STEP 1 — Use MINI DTO with short keys (saves ~50% JSON size)
                var dto = StudentMiniDto.FromStudentResult(student);

                var jsonSettings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    DefaultValueHandling = DefaultValueHandling.Ignore
                };
                string json = JsonConvert.SerializeObject(dto, Formatting.None, jsonSettings);

                // ✅ STEP 2 — GZip compress
                byte[] compressed = CompressionHelper.Compress(json);

                // ✅ STEP 3 — AES-256 encrypt
                byte[] encrypted = EncryptionHelper.Encrypt(compressed);

                // ✅ STEP 4 — Base64 encode for URL
                string base64Payload = Convert.ToBase64String(encrypted);

                // ✅ STEP 5 — Build QR string payload
                // Use URL-safe Base64 (replace + / = to avoid URL encoding bloat)
                string urlSafeBase64 = base64Payload
                    .Replace("+", "-")
                    .Replace("/", "_")
                    .Replace("=", ""); // remove padding — saves extra chars

                string qrPayload = $"http://115.243.18.52/t1/interResult.aspx?enc={urlSafeBase64}";

                // ✅ STEP 6 — Log payload size so you can monitor
                int payloadBytes = Encoding.UTF8.GetByteCount(qrPayload);
                Console.WriteLine($"[QR] RollNo={rollno} RC={rc} PayloadSize={payloadBytes} bytes | JSON={json.Length} chars");

                // ✅ STEP 7 — Generate QR
                // ECCLevel.L = lowest error correction = MAXIMUM data capacity
                // pixelsPerModule=5 on 400x400 = clean scannable QR even with large data
                // ✅ STEP 7 — Generate QR at clean fixed size like reference image

                // ✅ STEP 7 — High resolution QR, resized to 150x150 with sharp quality
                using var generator = new QRCodeGenerator();
                using var qrData = generator.CreateQrCode(qrPayload, QRCodeGenerator.ECCLevel.L);
                using var qrCode = new QRCode(qrData);

                // Generate at HIGH resolution first (10px per module = crisp)
                using var bitmap = qrCode.GetGraphic(
                    pixelsPerModule: 10,
                    darkColor: Color.Black,
                    lightColor: Color.White,
                    drawQuietZones: true
                );

                // ✅ Resize to 150x150 using HIGH QUALITY interpolation (sharp, not blurry)
                using var resized = new Bitmap(150, 150);
                resized.SetResolution(300f, 300f); // 300 DPI = mobile scannable
                using (var g = Graphics.FromImage(resized))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None; // keep edges sharp
                    g.DrawImage(bitmap, 0, 0, 150, 150);
                }

                string qrFile = $"{rollno}{rc}.png";
                string qrPath = Path.Combine(qrFolder, qrFile);
                resized.Save(qrPath, System.Drawing.Imaging.ImageFormat.Png);
                //// ✅ STEP 7 — Generate QR at Version 1 size (small, compact) // workking 
                //using var generator = new QRCodeGenerator();
                //using var qrData = generator.CreateQrCode(qrPayload, QRCodeGenerator.ECCLevel.L);
                //using var qrCode = new QRCode(qrData);

                //using var bitmap = qrCode.GetGraphic(
                //    pixelsPerModule: 3,        // ✅ 3px per module = small Version 1 size
                //    darkColor: Color.Black,
                //    lightColor: Color.White,
                //    drawQuietZones: true
                //);

                //// ✅ Resize to 150x150 — Version 1 compact size
                //using var resized = new Bitmap(bitmap, new Size(150, 150));
                //resized.SetResolution(96f, 96f);

                //string qrFile = $"{rollno}{rc}.png";
                //string qrPath = Path.Combine(qrFolder, qrFile);
                //resized.Save(qrPath, System.Drawing.Imaging.ImageFormat.Png);

                // Save at actual generated size (don't resize down — that blurs modules)
                //string qrFile = $"{rollno}{rc}.png";
                //string qrPath = Path.Combine(qrFolder, qrFile);
                //bitmap.Save(qrPath, System.Drawing.Imaging.ImageFormat.Png);

                csvRows.Add($"{rollno},{rc},\"{base64Payload}\"");
            }

            if (csvRows.Count == 1)
                return Content("No valid result");

            string csvFile = $"qr{rollno}.csv";
            string csvPath = Path.Combine(csvFolder, csvFile);
            System.IO.File.WriteAllLines(csvPath, csvRows);

            string zipFileName = $"QR_{rollno}{DateTime.Now:yyyyMMddHHmmss}.zip";
            string zipPath = Path.Combine(zipFolder, zipFileName);

            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (var file in Directory.GetFiles(qrFolder, $"{rollno}*.png"))
                    zip.CreateEntryFromFile(file, Path.GetFileName(file));
                zip.CreateEntryFromFile(csvPath, csvFile);
            }

            byte[] zipBytes = await System.IO.File.ReadAllBytesAsync(zipPath);
            return File(zipBytes, "application/zip", zipFileName);
        }


        //[HttpGet("GenerateQRCodeWithCSV")] // 25/03
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

        //    [HttpGet("GenerateQRCodeWithCSV")] // save in CSV file as are requ QR small
        //    public async Task<IActionResult> GenerateQRCodeWithCSV(string rollno)
        //    {
        //        if (string.IsNullOrEmpty(rollno))
        //            return BadRequest("RollNo required");

        //        var rollCodes = await _dbHelper.GetRollCodesByRollNoAsync(rollno);
        //        if (!rollCodes.Any())
        //            return Content("No data found");

        //        var csvRows = new List<string>
        //{
        //    "RollNo,RollCode,EncryptedQRPayload"
        //};

        //        string basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        //        string qrFolder = Path.Combine(basePath, "qr");
        //        string csvFolder = Path.Combine(basePath, "csv");
        //        string zipFolder = Path.Combine(basePath, "zip");

        //        Directory.CreateDirectory(qrFolder);
        //        Directory.CreateDirectory(csvFolder);
        //        Directory.CreateDirectory(zipFolder);

        //        foreach (var rc in rollCodes)
        //        {
        //            var student = await _dbHelper.GetStudentResultAsync(rc, rollno);
        //            if (student == null || student.Status != 1)
        //                continue;

        //            // 🔹 Serialize, compress, and encrypt the student info + rc + rollno
        //            var payloadObject = new
        //            {
        //                RollNo = rollno,
        //                RollCode = rc
        //            };
        //            var json = JsonConvert.SerializeObject(payloadObject);
        //            var compressed = CompressionHelper.Compress(json);
        //            var encrypted = EncryptionHelper.Encrypt(compressed);

        //            // 🔹 QR payload: only encrypted string
        //            string qrPayload = $"http://115.243.18.52/t1/interResult.aspx?enc={Uri.EscapeDataString(encrypted)}";

        //            // 🔹 Generate QR — small, clean
        //            using var generator = new QRCodeGenerator();
        //            using var qrData = generator.CreateQrCode(qrPayload, QRCodeGenerator.ECCLevel.L);
        //            using var qrCode = new QRCode(qrData);

        //            using var bitmap = qrCode.GetGraphic(3, Color.Black, Color.White, drawQuietZones: true);
        //            using var resized = new Bitmap(bitmap, new Size(150, 150));
        //            resized.SetResolution(150f, 150f);

        //            string qrFile = $"{rollno}_{rc}.png";
        //            string qrPath = Path.Combine(qrFolder, qrFile);
        //            resized.Save(qrPath, System.Drawing.Imaging.ImageFormat.Png);

        //            // 🔹 Save encrypted value in CSV
        //            csvRows.Add($"{rollno},{rc},\"{encrypted}\"");
        //        }

        //        if (csvRows.Count == 1)
        //            return Content("No valid result");

        //        // 🔹 Save CSV
        //        string csvFile = $"qr_{rollno}.csv";
        //        string csvPath = Path.Combine(csvFolder, csvFile);
        //        System.IO.File.WriteAllLines(csvPath, csvRows);

        //        // 🔹 Create ZIP
        //        string zipFileName = $"QR_{rollno}_{DateTime.Now:yyyyMMddHHmmss}.zip";
        //        string zipPath = Path.Combine(zipFolder, zipFileName);

        //        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        //        {
        //            foreach (var file in Directory.GetFiles(qrFolder, $"{rollno}_*.png"))
        //                zip.CreateEntryFromFile(file, Path.GetFileName(file));

        //            zip.CreateEntryFromFile(csvPath, csvFile);
        //        }

        //        byte[] zipBytes = await System.IO.File.ReadAllBytesAsync(zipPath);
        //        return File(zipBytes, "application/zip", zipFileName);
        //    }



        //    [HttpGet("GenerateQRCodeWithCSV")] // save in CSV file
        //    public async Task<IActionResult> GenerateQRCodeWithCSV(string rollno)
        //    {
        //        if (string.IsNullOrEmpty(rollno))
        //            return BadRequest("RollNo required");

        //        var rollCodes = await _dbHelper.GetRollCodesByRollNoAsync(rollno);
        //        if (!rollCodes.Any())
        //            return Content("No data found");

        //        var csvRows = new List<string>
        //{
        //    "RollNo,RollCode,QRPayload"
        //};

        //        string basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        //        string qrFolder = Path.Combine(basePath, "qr");
        //        string csvFolder = Path.Combine(basePath, "csv");
        //        string zipFolder = Path.Combine(basePath, "zip");

        //        Directory.CreateDirectory(qrFolder);
        //        Directory.CreateDirectory(csvFolder);
        //        Directory.CreateDirectory(zipFolder);

        //        foreach (var rc in rollCodes)
        //        {
        //            var student = await _dbHelper.GetStudentResultAsync(rc, rollno);
        //            if (student == null || student.Status != 1)
        //                continue;

        //            // ✅ SHORT URL ONLY — rc + rn keeps QR data minimal (~55 chars)
        //            // interResult.aspx will fetch result from DB on scan using rc + rn
        //            string qrPayload = $"http://115.243.18.52/t1/interResult.aspx?rc={rc}&rn={rollno}";
        //            // string qrPayload = $"https://interresult-25.biharboardexam.com/interResult.aspx?rc={rc}&rn={rollno}";

        //            // 🔹 Generate QR — short payload = very few modules = small clean QR
        //            using var generator = new QRCodeGenerator();
        //            using var qrData = generator.CreateQrCode(qrPayload, QRCodeGenerator.ECCLevel.L);
        //            using var qrCode = new QRCode(qrData);

        //            // 🔹 pixelsPerModule=3, short data = small QR like your reference image
        //            using var bitmap = qrCode.GetGraphic(
        //                3,
        //                Color.Black,
        //                Color.White,
        //                drawQuietZones: true
        //            );

        //            // 🔹 300px at 300 DPI = exactly 1 inch when printed
        //            using var resized = new Bitmap(bitmap, new Size(150, 150));
        //            resized.SetResolution(150f, 150f);

        //            string qrFile = $"{rollno}_{rc}.png";
        //            string qrPath = Path.Combine(qrFolder, qrFile);
        //            resized.Save(qrPath, System.Drawing.Imaging.ImageFormat.Png);

        //            // ✅ Save encrypted value separately in CSV for reference
        //            var json = JsonConvert.SerializeObject(student);
        //            var compressed = CompressionHelper.Compress(json);
        //            var encrypted = EncryptionHelper.Encrypt(compressed);
        //            csvRows.Add($"{rollno},{rc},\"{qrPayload}\",\"{encrypted}\"");
        //        }

        //        if (csvRows.Count == 1)
        //            return Content("No valid result");

        //        // 🔹 Save CSV
        //        string csvFile = $"qr_{rollno}.csv";
        //        string csvPath = Path.Combine(csvFolder, csvFile);
        //        System.IO.File.WriteAllLines(csvPath, csvRows);

        //        // 🔹 Create ZIP
        //        string zipFileName = $"QR_{rollno}_{DateTime.Now:yyyyMMddHHmmss}.zip";
        //        string zipPath = Path.Combine(zipFolder, zipFileName);

        //        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        //        {
        //            foreach (var file in Directory.GetFiles(qrFolder, $"{rollno}_*.png"))
        //                zip.CreateEntryFromFile(file, Path.GetFileName(file));

        //            zip.CreateEntryFromFile(csvPath, csvFile);
        //        }

        //        byte[] zipBytes = await System.IO.File.ReadAllBytesAsync(zipPath);
        //        return File(zipBytes, "application/zip", zipFileName);
        //    }






        //[HttpPost("VerifyQRCode")] // only check encypt QR Data 
        //public IActionResult VerifyQRCode([FromBody] QrRequest request)
        //{
        //    if (string.IsNullOrWhiteSpace(request.EncryptedValue))
        //        return BadRequest("QR data is required");

        //    try
        //    {
        //        // 🔐 STEP 1: DECRYPT FIRST
        //        var decrypted = EncryptionHelper.Decrypt(request.EncryptedValue);

        //        // 📦 STEP 2: DECOMPRESS
        //        var json = CompressionHelper.Decompress(decrypted);

        //        // 🔁 STEP 3: DESERIALIZE
        //        var student = JsonConvert.DeserializeObject<dynamic>(json);

        //        return Ok(student); // or return View(student)
        //    }
        //    catch (Exception ex)
        //    {
        //        return BadRequest("Invalid or corrupted QR Code");
        //    }
        //}




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
