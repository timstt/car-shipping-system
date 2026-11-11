using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;

namespace ASTCUSA.Models.Analytics
{
    // ЛР6: програмне доповнення таблиці Users до ~100 000 записів і повернення до початкового стану.
    // Згенеровані записи — клієнти з e-mail у домені gen.test: за ним їх можна знайти й видалити.
    public static class UsersGenerator
    {
        public const int TargetCount = 100000;
        public const string GeneratedDomain = "@gen.test";

        private static readonly string[] FirstNames =
        {
            "Іван", "Марія", "Олег", "Наталія", "Андрій", "Світлана", "Дмитро", "Юлія", "Сергій", "Ольга",
            "Максим", "Катерина", "Віктор", "Ірина", "Богдан", "Оксана", "Тарас", "Леся", "Роман", "Анна"
        };

        private static readonly string[] LastNames =
        {
            "Петренко", "Шевчук", "Бондаренко", "Кравченко", "Ткаченко", "Олійник", "Коваленко", "Мороз",
            "Поліщук", "Савченко", "Руденко", "Лисенко", "Гончар", "Мельник", "Захарченко", "Бойко"
        };

        private static readonly string[] Cities =
        {
            "Київ", "Львів", "Одеса", "Харків", "Дніпро", "Чернігів", "Полтава", "Вінниця", "Житомир", "Суми"
        };

        private static readonly string[] Streets =
        {
            "вул. Шевченка", "вул. Соборна", "просп. Свободи", "вул. Київська", "бульв. Шевченка", "вул. Миру"
        };

        // Коди мобільних операторів: 67, 68, 96, 97, 98 — Київстар, решта — інші оператори
        private static readonly string[] OperatorCodes = { "67", "68", "96", "97", "98", "50", "66", "95", "99", "63", "73", "93" };

        private static string ConnectionString
        {
            get { return ConfigurationManager.ConnectionStrings[AnalyticsDatabase.ConnectionName].ConnectionString; }
        }

        public static int CountAll()
        {
            return Scalar("SELECT COUNT(*) FROM dbo.Users");
        }

        public static int CountGenerated()
        {
            return Scalar("SELECT COUNT(*) FROM dbo.Users WHERE Email LIKE N'%" + GeneratedDomain + "'");
        }

        // Доповнює таблицю до TargetCount записів (повторне натискання нічого не додає).
        // Повертає кількість доданих записів; elapsedMs — час вставки.
        public static int FillTo(int target, out long elapsedMs)
        {
            AnalyticsDatabase.EnsureCreated();
            var watch = Stopwatch.StartNew();
            int missing = target - CountAll();
            if (missing <= 0)
            {
                elapsedMs = 0;
                return 0;
            }

            var existing = ExistingGeneratedEmails();
            var table = new DataTable();
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Email", typeof(string));
            table.Columns.Add("Role", typeof(string));
            table.Columns.Add("PhoneNumber", typeof(string));
            table.Columns.Add("Address", typeof(string));

            var random = new Random(2026); // фіксоване зерно — однакові дані при кожному заповненні
            int number = 0;
            while (table.Rows.Count < missing)
            {
                number++;
                string email = MakeEmail(number, random);
                if (existing.Contains(email)) continue;

                table.Rows.Add(
                    Pick(FirstNames, random) + " " + Pick(LastNames, random),
                    email,
                    "Client",
                    MakePhone(random),
                    Pick(Cities, random) + ", " + Pick(Streets, random) + ", " + random.Next(1, 200));
            }

            // SqlBulkCopy вставляє всі рядки одним потоком даних — у сотні разів швидше за 100 000 INSERT
            using (var bulk = new SqlBulkCopy(ConnectionString))
            {
                bulk.DestinationTableName = "dbo.Users";
                bulk.BatchSize = 10000;
                bulk.BulkCopyTimeout = 300;
                foreach (DataColumn column in table.Columns)
                    bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                bulk.WriteToServer(table);
            }

            elapsedMs = watch.ElapsedMilliseconds;
            return table.Rows.Count;
        }

        // Видаляє згенеровані записи: у таблиці знову лише 16 записів зі скрипта
        public static int RemoveGenerated()
        {
            AnalyticsDatabase.EnsureCreated();
            using (var connection = new SqlConnection(ConnectionString))
            using (var command = new SqlCommand("DELETE FROM dbo.Users WHERE Email LIKE N'%" + GeneratedDomain + "'", connection))
            {
                command.CommandTimeout = 300;
                connection.Open();
                return command.ExecuteNonQuery();
            }
        }

        // ~5 % адрес містять кирилицю — такі e-mail запит має відкинути
        private static string MakeEmail(int number, Random random)
        {
            string login = random.Next(100) < 5 ? "клієнт" : "client";
            return login + number.ToString("D6") + GeneratedDomain;
        }

        // ~10 % номерів записано без +380 — такі номери запит має відкинути
        private static string MakePhone(Random random)
        {
            string code = Pick(OperatorCodes, random);
            string digits = random.Next(0, 10000000).ToString("D7");
            return random.Next(100) < 10 ? "0" + code + digits : "+380" + code + digits;
        }

        private static string Pick(string[] values, Random random)
        {
            return values[random.Next(values.Length)];
        }

        private static HashSet<string> ExistingGeneratedEmails()
        {
            var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var connection = new SqlConnection(ConnectionString))
            using (var command = new SqlCommand("SELECT Email FROM dbo.Users WHERE Email LIKE N'%" + GeneratedDomain + "'", connection))
            {
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read()) emails.Add(reader.GetString(0));
                }
            }
            return emails;
        }

        private static int Scalar(string sql)
        {
            AnalyticsDatabase.EnsureCreated();
            using (var connection = new SqlConnection(ConnectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                connection.Open();
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }
    }
}
