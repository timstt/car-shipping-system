using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace ASTCUSA.Models
{
    // Хешування паролів через PBKDF2 (Rfc2898DeriveBytes, SHA-256) з випадковою сіллю.
    // Формат поля PasswordHash: "PBKDF2$<ітерації>$<сіль base64>$<хеш base64>"
    public static class PasswordHasher
    {
        private const string Prefix = "PBKDF2";
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 100000;

        // Хеш-заглушка, щоб перевірка для неіснуючого email тривала стільки ж, скільки для існуючого
        private static readonly Lazy<string> DummyHash = new Lazy<string>(() => Hash(Guid.NewGuid().ToString()));

        public static string Hash(string password)
        {
            if (password == null) throw new ArgumentNullException("password");

            var salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            byte[] hash = Derive(password, salt, Iterations, HashSize);
            return string.Join("$", Prefix, Iterations.ToString(), Convert.ToBase64String(salt), Convert.ToBase64String(hash));
        }

        public static bool Verify(string password, string storedHash)
        {
            if (password == null || string.IsNullOrEmpty(storedHash)) return false;

            var parts = storedHash.Split('$');
            int iterations;
            if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], out iterations) || iterations <= 0)
                return false;

            byte[] salt, expected;
            try
            {
                salt = Convert.FromBase64String(parts[2]);
                expected = Convert.FromBase64String(parts[3]);
            }
            catch (FormatException)
            {
                return false;
            }

            byte[] actual = Derive(password, salt, iterations, expected.Length);
            return FixedTimeEquals(actual, expected);
        }

        // Імітує перевірку пароля, коли користувача не знайдено (не видає, чи існує email)
        public static void VerifyDummy(string password)
        {
            Verify(password ?? string.Empty, DummyHash.Value);
        }

        private static byte[] Derive(string password, byte[] salt, int iterations, int length)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(length);
            }
        }

        // Порівняння в постійний час: час не залежить від позиції першого розбіжного байта
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
