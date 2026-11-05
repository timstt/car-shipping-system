using System.ComponentModel.DataAnnotations;

namespace ASTCUSA.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Вкажіть e-mail"), EmailAddress(ErrorMessage = "Некоректний e-mail")]
        [Display(Name = "E-mail")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Вкажіть пароль")]
        [DataType(DataType.Password)]
        [Display(Name = "Пароль")]
        public string Password { get; set; }
    }

    // Профіль клієнта: e-mail (логін) лише показується, змінюються ім'я, телефон і адреса
    public class ProfileViewModel
    {
        [Display(Name = "E-mail")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Вкажіть ім'я"), StringLength(100, ErrorMessage = "Ім'я задовге")]
        [Display(Name = "Ім'я")]
        public string Name { get; set; }

        [RegularExpression(PhoneNumbers.Pattern, ErrorMessage = PhoneNumbers.Error), StringLength(30, ErrorMessage = "Номер телефону задовгий")]
        [Display(Name = "Телефон")]
        public string PhoneNumber { get; set; }

        [StringLength(200, ErrorMessage = "Адреса задовга")]
        [Display(Name = "Адреса")]
        public string Address { get; set; }

        // Кількість заявок: клієнта із заявками видалити не можна
        public int OrdersCount { get; set; }
    }

    // Правила для логіна (e-mail) і пароля: лише латиниця. Шаблони перевіряються атрибутом
    // [RegularExpression] і в браузері, і на сервері, тож обійти їх запитом в обхід форми не вийде.
    public static class Credentials
    {
        // Латинський e-mail з крапкою в домені: name@mail.com
        public const string EmailPattern = @"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}$";
        public const string EmailError = "Вкажіть e-mail латиницею, наприклад name@mail.com";

        // Пароль: латинські літери, цифри й символи ASCII (! # $ % тощо), без пробілів і кирилиці
        public const string PasswordPattern = @"^[\x21-\x7E]+$";
        public const string PasswordError = "Пароль може містити лише латинські літери, цифри та символи, без пробілів";
    }

    // Реєстрація доступна лише для ролі «Клієнт»
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Вкажіть ім'я"), StringLength(100, ErrorMessage = "Ім'я задовге")]
        [Display(Name = "Ім'я")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Вкажіть e-mail"), EmailAddress(ErrorMessage = "Некоректний e-mail")]
        [RegularExpression(Credentials.EmailPattern, ErrorMessage = Credentials.EmailError)]
        [StringLength(256, ErrorMessage = "E-mail задовгий")]
        [Display(Name = "E-mail")]
        public string Email { get; set; }

        [RegularExpression(PhoneNumbers.Pattern, ErrorMessage = PhoneNumbers.Error), StringLength(30, ErrorMessage = "Номер телефону задовгий")]
        [Display(Name = "Телефон")]
        public string PhoneNumber { get; set; }

        [StringLength(200, ErrorMessage = "Адреса задовга")]
        [Display(Name = "Адреса")]
        public string Address { get; set; }

        [Required(ErrorMessage = "Вкажіть пароль")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Пароль має містити щонайменше 8 символів")]
        [RegularExpression(Credentials.PasswordPattern, ErrorMessage = Credentials.PasswordError)]
        [DataType(DataType.Password)]
        [Display(Name = "Пароль")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Підтвердіть пароль")]
        [System.ComponentModel.DataAnnotations.Compare("Password", ErrorMessage = "Паролі не збігаються")]
        [DataType(DataType.Password)]
        [Display(Name = "Підтвердження пароля")]
        public string ConfirmPassword { get; set; }
    }
}
