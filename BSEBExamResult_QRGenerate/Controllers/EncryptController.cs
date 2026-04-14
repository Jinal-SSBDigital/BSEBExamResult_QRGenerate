using BSEBExamResult_QRGenerate.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace BSEBExamResult_QRGenerate.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EncryptController : ControllerBase
    {
        [HttpPost("encrypt")]
        public IActionResult EncryptData([FromBody] EncryptRequest request)
        {
            if (!ValidateRequest(request, out string errorMessage))
            {
                return BadRequest(new { success = false, message = errorMessage});
            }


            try
            {
                // 🔹 Combine data if needed
                string input = $"{request.PlainText}";
                //string input = $"{request.RollCode}|{request.RollNo}|{request.PlainText}";

                // 🔹 Encrypt
                var result = QrUtility.EncryptPlainText(input.Trim(), request.encryptkey);

                return Ok(new {success = true, rollCode = request.RollCode, rollNo = request.RollNo, encryptedData = result }); // 
                //return Ok(new {success = true, encryptedData = $"{request.RollCode},{request.RollNo},{result}" }); // RollCode and RollNo along with encrypted data
                //return Ok(new {success = true, encryptedData = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        private bool ValidateRequest(EncryptRequest request, out string error)
        {
            error = "";

            if (request == null)
            {
                error = "Request body is required";
                return false;
            }

            if (!IsValidKey(request.encryptkey, out error))
                return false;

            if (!request.RollCode.HasValue || request.RollCode <= 0)
            {
                error = "RollCode must be a valid number greater than 0";
                return false;
            }

            if (!request.RollNo.HasValue || request.RollNo <= 0)
            {
                error = "RollNo must be a valid number greater than 0";
                return false;
            }

            if (IsInvalidInput(request.PlainText))
            {
                error = "PlainText is required";
                return false;
            }

            return true;
        }
        private bool IsInvalidInput(string value)
        {
            return string.IsNullOrWhiteSpace(value) || value.Trim().ToLower() == "string";
        }
        private bool IsValidKey(string key, out string error)
        {
            error = "";

            if (string.IsNullOrWhiteSpace(key))
            {
                error = "Encryption key is required.";
                return false;
            }

            // Length check (32 characters = 32 bytes for ASCII)
            if (key.Length != 32)
            {
                error = "Encryption key must be exactly 32 characters.";
                return false;
            }

            // Pattern check (only alphanumeric)
            var regex = new System.Text.RegularExpressions.Regex("^[A-Za-z0-9]{32}$");
            if (!regex.IsMatch(key))
            {
                error = "Key must contain only A-Z, a-z, 0-9 (no spaces or special characters).";
                return false;
            }

            return true;
        }

        [HttpPost("decrypt")]
        public IActionResult DecryptData([FromBody] EncryptRequest request)
        {
           

            try
            {
                var result = QrUtility.DecryptPayload(request.PlainText, request.encryptkey);

                return Ok(new { success = true,decryptedData = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Decryption failed",
                    error = ex.Message
                });
            }
        }
    }
}
public class EncryptRequest
{
    public string encryptkey { get; set; }
    public int? RollCode { get; set; }
    public int? RollNo { get; set; }
    public string PlainText { get; set; }
}