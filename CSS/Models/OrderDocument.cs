using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace ASTCUSA.Models
{
    // Документ, який клієнт прикріпив до заявки на запит менеджера (Order 1 — 0..* OrderDocument).
    // Вміст файлу зберігається в базі разом із заявкою, тож файл і запис про нього не можуть розійтися.
    public class OrderDocument
    {
        public const int MaxSize = 5 * 1024 * 1024;   // 5 МБ на файл
        public const int MaxFiles = 10;               // за одне надання документів

        public int OrderDocumentId { get; set; }

        public int OrderId { get; set; }
        public virtual Order Order { get; set; }

        [Required, StringLength(255)]
        [Display(Name = "Файл")]
        public string FileName { get; set; }

        [Required, StringLength(100)]
        public string ContentType { get; set; }

        [Display(Name = "Розмір")]
        public int Size { get; set; }

        [Required]
        public byte[] Content { get; set; }

        [Display(Name = "Завантажено")]
        public DateTime UploadedAt { get; set; }

        // Дозволені формати: тип визначається за першими байтами файлу (сигнатурою), а не за розширенням,
        // тому під виглядом PDF не вийде завантажити, наприклад, сторінку HTML чи програму
        private static readonly (string ContentType, byte[] Signature, string[] Extensions)[] Formats =
        {
            ("application/pdf", new byte[] { 0x25, 0x50, 0x44, 0x46 }, new[] { ".pdf" }),                 // %PDF
            ("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF }, new[] { ".jpg", ".jpeg" }),
            ("image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, new[] { ".png" }),
        };

        public const string AllowedExtensions = ".pdf,.jpg,.jpeg,.png";

        // Створити документ із завантаженого файлу; кидає ArgumentException, якщо файл не підходить
        public static OrderDocument FromUpload(string fileName, byte[] content)
        {
            string name = System.IO.Path.GetFileName(fileName ?? string.Empty).Trim();
            if (name.Length == 0) throw new ArgumentException("Файл без назви.");
            if (name.Length > 255) name = name.Substring(name.Length - 255);
            if (content == null || content.Length == 0) throw new ArgumentException("Файл «" + name + "» порожній.");
            if (content.Length > MaxSize) throw new ArgumentException("Файл «" + name + "» більший за 5 МБ.");

            string ext = System.IO.Path.GetExtension(name).ToLowerInvariant();
            var format = Formats.FirstOrDefault(f => f.Extensions.Contains(ext));
            if (format.ContentType == null)
                throw new ArgumentException("Файл «" + name + "»: дозволено лише PDF, JPG і PNG.");
            if (content.Length < format.Signature.Length || !format.Signature.SequenceEqual(content.Take(format.Signature.Length)))
                throw new ArgumentException("Файл «" + name + "» не є справжнім " + ext.TrimStart('.').ToUpperInvariant() + ".");

            return new OrderDocument
            {
                FileName = name,
                ContentType = format.ContentType,
                Size = content.Length,
                Content = content,
                UploadedAt = DateTime.Now
            };
        }

        // Розмір для показу: «245 КБ», «1,2 МБ»
        public static string FormatSize(int bytes)
        {
            if (bytes < 1024 * 1024) return Math.Max(1, bytes / 1024) + " КБ";
            return (bytes / 1024.0 / 1024.0).ToString("0.0") + " МБ";
        }
    }
}
