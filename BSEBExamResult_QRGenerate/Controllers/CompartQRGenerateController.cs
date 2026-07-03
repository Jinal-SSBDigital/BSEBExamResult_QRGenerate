using BSEBExamResult_QRGenerate.Data;
using BSEBExamResult_QRGenerate.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BSEBExamResult_QRGenerate.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CompartQRGenerateController : ControllerBase
    {
        #region Jinal Nagar
        private readonly DbHelper _dbHelper;
        private readonly ILogger<CompartQRGenerateController> _logger;

        public CompartQRGenerateController(AppDBContext context, ILogger<CompartQRGenerateController> logger)
        {
            _dbHelper = new DbHelper(context);
            _logger = logger;
        }


        // ✅ Bulk encrypt ALL students and save to DB
        [HttpPost("CompartQRGenerateAndSaveAllEncrypted")]
        public async Task<IActionResult> CompartQRGenerateAndSaveAllEncrypted()
        {
            _logger.LogInformation("Bulk QR Encryption started at {Time}", DateTime.Now);

            var allRolls = await _dbHelper.GetAllRollCodesAsync();
            _logger.LogInformation("Total students fetched: {Count}", allRolls.Count);

            int successCount = 0;
            int failCount = 0;
            int skippedCount = 0;

            const int BATCH_SIZE = 5000;
            var batch = new List<EXAM_QRComprtEncryptedData>(BATCH_SIZE);

            foreach (var (rollCode, rollNo) in allRolls)
            {
                try
                {
                    var student = await _dbHelper.GetCompartStudentResultAsync(rollCode, rollNo);

                    if (student == null || student.Status != 1)
                    {
                        skippedCount++;
                        continue;
                    }

                    string encrypted = string.Equals(student.ExamType, "COMPARTMENTAL", StringComparison.OrdinalIgnoreCase)
                        ? "2C" + QrUtility.CompartQRGenerateEncryptedPayload(student)
                        : "S" + QrUtility.CompartQRGenerateEncryptedPayload(student);

                    int encryptedLength = encrypted.Length;

                    batch.Add(new EXAM_QRComprtEncryptedData
                    {
                        RollCode = rollCode,
                        RollNo = rollNo,
                        EncryptedData = encrypted,
                        Length = encryptedLength,
                        CreatedOn = DateTime.Now
                    });

                    successCount++;

                    // ✅ Flush batch to DB every BATCH_SIZE records
                    if (batch.Count >= BATCH_SIZE)
                    {
                        await _dbHelper.BulkSaveCompartEncryptedDataAsync(batch);
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

            // ✅ Save any remaining records
            if (batch.Count > 0)
            {
                await _dbHelper.BulkSaveCompartEncryptedDataAsync(batch);
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
                // don't return the full batch anymore — it may be huge and is already saved to DB
            });
        }
    }
        #endregion
}
