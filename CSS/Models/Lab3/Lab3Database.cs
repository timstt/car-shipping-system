using System;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ASTCUSA.Models.Lab3
{
    // Створення бази astcusa_lab3 під час першого звернення до неї.
    // Виконує той самий скрипт docs/lab3/create_and_seed.sql, вбудований у збірку
    // як ресурс (див. ASTCUSA.csproj, EmbeddedResource). Якщо база й таблиці вже є,
    // нічого не робить, тож дані, змінені на сторінці «БД і LINQ», зберігаються.
    public static class Lab3Database
    {
        public const string ConnectionName = "Lab3Connection";
        private const string ScriptResource = "ASTCUSA.Lab3.create_and_seed.sql";
        private const string ScriptDatabaseName = "astcusa_lab3";

        private static readonly object Sync = new object();
        private static volatile bool ready;

        public static string DatabaseName
        {
            get { return new SqlConnectionStringBuilder(ConnectionString).InitialCatalog; }
        }

        public static string ServerName
        {
            get { return new SqlConnectionStringBuilder(ConnectionString).DataSource; }
        }

        private static string ConnectionString
        {
            get
            {
                var setting = ConfigurationManager.ConnectionStrings[ConnectionName];
                if (setting == null)
                    throw new InvalidOperationException("У Web.config немає рядка підключення " + ConnectionName + ".");
                return setting.ConnectionString;
            }
        }

        public static void EnsureCreated()
        {
            if (ready) return;
            lock (Sync)
            {
                if (ready) return;

                var builder = new SqlConnectionStringBuilder(ConnectionString);
                string database = builder.InitialCatalog;
                if (!Regex.IsMatch(database ?? "", "^[A-Za-z0-9_]+$"))
                    throw new InvalidOperationException("Некоректне ім'я бази даних у " + ConnectionName + ": " + database);

                // Підключення до master: самої бази ще може не бути
                builder.InitialCatalog = "master";
                using (var connection = new SqlConnection(builder.ConnectionString))
                {
                    connection.Open();
                    if (!TablesExist(connection, database))
                        RunScript(connection, database);
                }
                ready = true;
            }
        }

        // База є і в ній є остання з таблиць скрипта (Deliveries)
        private static bool TablesExist(SqlConnection connection, string database)
        {
            const string sql =
                "SELECT CASE WHEN DB_ID(@db) IS NULL THEN 0 " +
                "WHEN OBJECT_ID(QUOTENAME(@db) + N'.dbo.Deliveries', N'U') IS NULL THEN 0 ELSE 1 END";
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@db", database);
                return Convert.ToInt32(command.ExecuteScalar()) == 1;
            }
        }

        // Виконує скрипт пакетами, розділеними рядками «GO» (як sqlcmd / SSMS).
        // Ім'я бази в скрипті замінюється на ім'я з рядка підключення.
        private static void RunScript(SqlConnection connection, string database)
        {
            string script = ReadScript().Replace(ScriptDatabaseName, database);
            foreach (string batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                using (var command = new SqlCommand(batch, connection))
                {
                    command.CommandTimeout = 120;
                    command.ExecuteNonQuery();
                }
            }
        }

        private static string ReadScript()
        {
            var stream = typeof(Lab3Database).Assembly.GetManifestResourceStream(ScriptResource);
            if (stream == null)
                throw new InvalidOperationException("У збірці немає вбудованого скрипта " + ScriptResource + ".");
            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
