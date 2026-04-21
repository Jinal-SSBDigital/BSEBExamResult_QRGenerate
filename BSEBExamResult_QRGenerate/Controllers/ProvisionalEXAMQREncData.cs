using BSEBExamResult_QRGenerate.Data;
using BSEBExamResult_QRGenerate.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BSEBExamResult_QRGenerate.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProvisionalEXAMQREncData : ControllerBase
    {
        private readonly DbHelper _dbHelper;
        private readonly ILogger<ProvisionalEXAMQREncData> _logger;

        public ProvisionalEXAMQREncData(AppDBContext context, ILogger<ProvisionalEXAMQREncData> logger)
        {
            _dbHelper = new DbHelper(context);
            _logger = logger;
        }

        // ✅ Bulk encrypt ALL students and save to DB
        [HttpPost("GenerateProvisionalEXAMQREncData")]
        public async Task<IActionResult> GenerateProvisionalEXAMQREncData()
        {
            _logger.LogInformation("Bulk QR Encryption started at {Time}", DateTime.Now);

            // Step 1: Get all RollCode + RollNo pairs directly from table
            var allRolls = await _dbHelper.Provisional_RollCodesForQR();
            _logger.LogInformation("Total students fetched: {Count}", allRolls.Count);

            int successCount = 0;
            int failCount = 0;
            int skippedCount = 0;

            const int BATCH_SIZE = 5000;
            var batch = new List<ProvisionalEXAMQREncdData>(BATCH_SIZE);

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
                    //string encrypted = QrUtility.GenerateEncryptedPayloadCompact(student);
                    string encrypted = "P" + QrUtility.GenerateProvisionalEncryptedPayload(student);
                 
                    // var qrPath = GenerateQrImage(encrypted, rollNo, rollCode);
                    int encryptedLength = encrypted.Length;
                    //if (qrPath == null)
                    //{
                    //    skippedCount++;
                    //    continue;
                    //}

                    // ✅ Only valid records go to DB
                    //batch.Add(new QREncryptedData
                    //{
                    //    RollCode = rollCode,
                    //    RollNo = rollNo,
                    //    EncryptedData = encrypted,
                    //    Length = encryptedLength,
                    //    // QrPath = "",
                    //    CreatedOn = DateTime.Now
                    //});
                    batch.Add(new ProvisionalEXAMQREncdData
                    {
                        RollCode = rollCode,
                        RollNo = rollNo,
                        EncryptedData = encrypted,
                        QRLength = encryptedLength
                    });
                    successCount++;

                    // Step 5: Flush batch to DB every 5000 records
                    if (batch.Count >= BATCH_SIZE)
                    {
                        await _dbHelper.BulkSaveProvisionalEXAMQREncData(batch);
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
                await _dbHelper.BulkSaveProvisionalEXAMQREncData(batch);
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
                skipped = skippedCount,
                data = batch   // 👈 THIS IS WHAT YOU NEED
            });
        }
    }
}
