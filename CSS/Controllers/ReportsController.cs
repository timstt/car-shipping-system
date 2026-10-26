using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using ASTCUSA.Models;

namespace ASTCUSA.Controllers
{
    // «Сформувати звітність» і «Експортувати звіт» (extend) — прецеденти менеджера.
    // Лише читання даних, тому всі дії — GET.
    [AllowRoles(UserRole.Manager)]
    public class ReportsController : BaseController
    {
        private OrderReport BuildReport(DateTime? from, DateTime? to)
        {
            var end = (to ?? DateTime.Today).Date;
            var start = (from ?? end.AddMonths(-1)).Date;
            if (start > end) { var t = start; start = end; end = t; }
            var endExclusive = end.AddDays(1);

            var orders = db.Orders
                .Include(o => o.Client).Include(o => o.Manager).Include(o => o.Car)
                .Where(o => o.CreatedAt >= start && o.CreatedAt < endExclusive)
                .ToList();
            return new OrderReport(start, end, orders);
        }

        // GET: Reports?from=2026-10-01&to=2026-10-31
        [HttpGet]
        public ActionResult Index(DateTime? from, DateTime? to)
        {
            return View(BuildReport(from, to));
        }

        // GET: Reports/Export — CSV-файл, який відкривається в Excel
        [HttpGet]
        public ActionResult Export(DateTime? from, DateTime? to)
        {
            var report = BuildReport(from, to);
            var fileName = string.Format("report_{0:yyyyMMdd}_{1:yyyyMMdd}.csv", report.From, report.To);
            return File(report.ToCsv(), "text/csv", fileName);
        }
    }
}
