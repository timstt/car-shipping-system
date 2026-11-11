using System;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Web.Mvc;
using ASTCUSA.Models.Analytics;

namespace ASTCUSA.Controllers
{
    // ЛР6: паралельна обробка даних. Заповнення таблиці Users до 100 000 записів
    // і порівняння часу одного запиту при послідовному та паралельному виконанні.
    public partial class AnalyticsController
    {
        private const int BenchmarkRepeats = 10;

        // GET: Analytics/Parallel
        [HttpGet]
        public ActionResult Parallel()
        {
            return View(ParallelModel(null));
        }

        // POST: Analytics/RunParallel — виконати заміри на поточних даних таблиці
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RunParallel()
        {
            // Дані читаються з бази один раз (без відстеження змін), далі вимірюється лише сам запит
            var watch = Stopwatch.StartNew();
            var users = AnalyticsDb.Users.AsNoTracking().OrderBy(u => u.Id).ToList();
            watch.Stop();

            var report = ParallelUsersQuery.Run(users, watch.ElapsedMilliseconds, BenchmarkRepeats);
            return View("Parallel", ParallelModel(report));
        }

        // POST: Analytics/FillUsers — програмно доповнити таблицю Users до 100 000 записів
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult FillUsers()
        {
            long ms;
            int added = UsersGenerator.FillTo(UsersGenerator.TargetCount, out ms);
            TempData["Message"] = added == 0
                ? "У таблиці вже є " + UsersGenerator.TargetCount.ToString("N0") + " записів."
                : "Додано " + added.ToString("N0") + " записів за " + ms.ToString("N0") + " мс.";
            return RedirectToAction("Parallel");
        }

        // POST: Analytics/RemoveGeneratedUsers — повернути таблицю до початкових записів
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RemoveGeneratedUsers()
        {
            int removed = UsersGenerator.RemoveGenerated();
            TempData["Message"] = "Видалено " + removed.ToString("N0") + " згенерованих записів.";
            return RedirectToAction("Parallel");
        }

        private ParallelPage ParallelModel(BenchmarkReport report)
        {
            return new ParallelPage
            {
                TotalUsers = UsersGenerator.CountAll(),
                GeneratedUsers = UsersGenerator.CountGenerated(),
                ProcessorCount = Environment.ProcessorCount,
                Report = report
            };
        }
    }
}
