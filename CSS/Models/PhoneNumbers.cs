using System.Linq;

namespace ASTCUSA.Models
{
    // Український номер телефону: 0XX XXX XX XX з необов'язковим кодом країни +38.
    // Допускаються пробіли, дефіси й дужки: «+380 50 111 22 33», «050-111-22-33», «(050) 1112233».
    // У базі номер зберігається в одному вигляді: +380XXXXXXXXX.
    public static class PhoneNumbers
    {
        // Для атрибута [RegularExpression]: той самий шаблон перевіряється в браузері й на сервері
        public const string Pattern = @"^\s*(\+?38)?[\s\-]*\(?0\d{2}\)?([\s\-]*\d){7}\s*$";

        public const string Error = "Вкажіть номер у форматі +380XXXXXXXXX";

        // Привести перевірений номер до вигляду +380XXXXXXXXX; порожній — null
        public static string Normalize(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;
            string digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length < 9) return phone.Trim();
            return "+380" + digits.Substring(digits.Length - 9);
        }
    }
}
