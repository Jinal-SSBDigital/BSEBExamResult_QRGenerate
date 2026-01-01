using BSEBExamResult_QRGenerate.Data;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace BSEBExamResult_QRGenerate.Controllers
{
    public class QRGenerate : Controller
    {
        private readonly DbHelper _dbHelper;
        public QRGenerate(AppDBContext context)
        {
            _dbHelper = new DbHelper(context);
        }
        public async Task<IActionResult> GenerateQRCode(string rollcode, string rollno)
        {

            if(string.IsNullOrEmpty(rollcode) || string.IsNullOrEmpty(rollno))
                return BadRequest("Roll code and roll number are required.");

            var StudentDetails = await _dbHelper.GetStudentResultAsync(rollcode,rollno);

            if(StudentDetails == null || StudentDetails.Status != 1)
                return NotFound("No student details found for the provided roll code and roll number.");

            var QrDto= new 
            {
                StudentDetails.RollCode,
                StudentDetails.RollNo,
                StudentDetails.NameoftheCandidate,
                StudentDetails.FathersName,
                StudentDetails.CollegeName,
                StudentDetails.Faculty,
                StudentDetails.TotalAggregateMarkinNumber,
                StudentDetails.Division,
                Subjects = StudentDetails.SubjectResults.Select(s => new
                {
                    s.Sub,
                    s.TotSub
                })
            };

            var QrJson=JsonConvert.SerializeObject(QrDto);
            return View();
        }
    }
}
