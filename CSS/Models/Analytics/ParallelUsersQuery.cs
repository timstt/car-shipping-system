using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ASTCUSA.Models.Analytics
{
    // ЛР6: один і той самий запит на вибірку, виконаний послідовно і паралельно різними технологіями.
    // Запит: клієнти-абоненти Київстар (+380 67/68/96/97/98) з коректним латинським e-mail.
    // Дані таблиці Users один раз читаються з бази, далі вимірюється лише сам запит над списком.
    public static class ParallelUsersQuery
    {
        private const string KyivstarPhonePattern = @"^\+380(67|68|96|97|98)\d{7}$";
        private const string LatinEmailPattern = @"^[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}$";

        // Кожен потік має власні об'єкти Regex (ThreadLocal). Спільний Regex теж працює правильно,
        // але кешує лише один внутрішній обробник: інші потоки щоразу створюють новий,
        // і паралельні способи майже не прискорюються через навантаження на збирач сміття.
        private static readonly ThreadLocal<Regex> KyivstarPhone = new ThreadLocal<Regex>(() => new Regex(KyivstarPhonePattern));
        private static readonly ThreadLocal<Regex> LatinEmail = new ThreadLocal<Regex>(() => new Regex(LatinEmailPattern));

        // Умова вибірки — однакова для всіх способів виконання
        public static bool Matches(AnalyticsUser user)
        {
            return user.Role == "Client"
                && user.PhoneNumber != null && KyivstarPhone.Value.IsMatch(user.PhoneNumber)
                && user.Email != null && LatinEmail.Value.IsMatch(user.Email);
        }

        // 1. Послідовно: звичайний LINQ, один потік
        public static List<AnalyticsUser> Sequential(IList<AnalyticsUser> users)
        {
            return users.Where(Matches).ToList();
        }

        // 2. PLINQ без обмежень: кількість потоків обирає .NET (за кількістю ядер)
        public static List<AnalyticsUser> Plinq(IList<AnalyticsUser> users)
        {
            return users.AsParallel().Where(Matches).ToList();
        }

        // 3. PLINQ на заданій кількості ядер
        public static List<AnalyticsUser> PlinqOnCores(IList<AnalyticsUser> users, int cores)
        {
            return users.AsParallel().WithDegreeOfParallelism(cores).Where(Matches).ToList();
        }

        // 4. Багатопоточність: потоки Thread створюються вручну, кожен перевіряє свою частину списку
        //    і пише у власний список (спільний List<T> з кількох потоків змінювати не можна)
        public static List<AnalyticsUser> Threads(IList<AnalyticsUser> users, int threadCount)
        {
            var parts = new List<AnalyticsUser>[threadCount];
            var threads = new Thread[threadCount];
            int size = (users.Count + threadCount - 1) / threadCount;

            for (int t = 0; t < threadCount; t++)
            {
                int index = t; // окрема змінна для кожного потоку
                int from = index * size;
                int to = Math.Min(from + size, users.Count);
                parts[index] = new List<AnalyticsUser>();
                threads[index] = new Thread(() =>
                {
                    for (int i = from; i < to; i++)
                        if (Matches(users[i])) parts[index].Add(users[i]);
                });
                threads[index].Start();
            }

            foreach (var thread in threads) thread.Join(); // чекаємо завершення всіх потоків
            return parts.SelectMany(p => p).ToList();
        }

        // 5. TPL: Parallel.ForEach по діапазонах індексів. Кожен робочий потік збирає результат
        //    у локальний список (localInit), наприкінці списки об'єднуються під lock (localFinally)
        public static List<AnalyticsUser> Tpl(IList<AnalyticsUser> users, int maxThreads)
        {
            var result = new List<AnalyticsUser>();
            var sync = new object();
            var ranges = Partitioner.Create(0, users.Count, Math.Max(1, users.Count / (maxThreads * 4)));
            var options = new ParallelOptions { MaxDegreeOfParallelism = maxThreads };

            Parallel.ForEach(ranges, options,
                () => new List<AnalyticsUser>(),
                (range, state, local) =>
                {
                    for (int i = range.Item1; i < range.Item2; i++)
                        if (Matches(users[i])) local.Add(users[i]);
                    return local;
                },
                local => { lock (sync) result.AddRange(local); });

            return result;
        }

        // Заміри: прогрів (JIT, пул потоків), далі кожен спосіб виконується repeats разів
        public static BenchmarkReport Run(IList<AnalyticsUser> users, long loadMs, int repeats)
        {
            int cores = Environment.ProcessorCount;
            var degrees = new List<int>();
            for (int n = 2; n < cores; n *= 2) degrees.Add(n);
            degrees.Add(cores);

            var methods = new List<Tuple<string, string, int, Func<List<AnalyticsUser>>>>
            {
                Method("Послідовно", "LINQ", 1, () => Sequential(users)),
                Method("PLINQ", "AsParallel()", cores, () => Plinq(users))
            };
            foreach (int n in degrees)
                methods.Add(Method("Кілька ядер", "PLINQ WithDegreeOfParallelism(" + n + ")", n, () => PlinqOnCores(users, n)));
            foreach (int n in degrees)
                methods.Add(Method("Багатопоточність", "Thread × " + n, n, () => Threads(users, n)));
            foreach (int n in degrees)
                methods.Add(Method("TPL", "Parallel.ForEach, до " + n + " потоків", n, () => Tpl(users, n)));

            foreach (var m in methods) m.Item4(); // прогрів

            var report = new BenchmarkReport
            {
                RowCount = users.Count,
                LoadMs = loadMs,
                Repeats = repeats,
                ProcessorCount = cores
            };
            double sequentialMs = 0;
            foreach (var m in methods)
            {
                var times = new List<double>();
                int found = 0;
                for (int r = 0; r < repeats; r++)
                {
                    var watch = Stopwatch.StartNew();
                    found = m.Item4().Count;
                    watch.Stop();
                    times.Add(watch.Elapsed.TotalMilliseconds);
                }

                var row = new BenchmarkRow
                {
                    Technology = m.Item1,
                    Method = m.Item2,
                    Threads = m.Item3,
                    AverageMs = times.Average(),
                    MinMs = times.Min(),
                    Found = found
                };
                if (report.Rows.Count == 0) sequentialMs = row.AverageMs;
                row.Speedup = row.AverageMs > 0 ? sequentialMs / row.AverageMs : 0;
                report.Rows.Add(row);
            }
            report.ExpectedFound = report.Rows[0].Found;
            return report;
        }

        private static Tuple<string, string, int, Func<List<AnalyticsUser>>> Method(
            string technology, string method, int threads, Func<List<AnalyticsUser>> run)
        {
            return Tuple.Create(technology, method, threads, run);
        }
    }

    public class BenchmarkRow
    {
        public string Technology { get; set; }
        public string Method { get; set; }
        public int Threads { get; set; }
        public double AverageMs { get; set; }
        public double MinMs { get; set; }
        public int Found { get; set; }
        public double Speedup { get; set; }
    }

    public class BenchmarkReport
    {
        public BenchmarkReport() { Rows = new List<BenchmarkRow>(); }

        public int RowCount { get; set; }
        public long LoadMs { get; set; }
        public int Repeats { get; set; }
        public int ProcessorCount { get; set; }
        public int ExpectedFound { get; set; }
        public List<BenchmarkRow> Rows { get; set; }
    }

    public class ParallelPage
    {
        public int TotalUsers { get; set; }
        public int GeneratedUsers { get; set; }
        public int ProcessorCount { get; set; }
        public BenchmarkReport Report { get; set; }
    }
}
