using Microsoft.EntityFrameworkCore;

namespace BSEBExamResult_QRGenerate.Data
{
    public class AppDBContext : DbContext
    {
        public AppDBContext(DbContextOptions<AppDBContext> options) : base(options) { }
    }
}
