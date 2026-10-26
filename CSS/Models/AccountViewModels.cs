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

    // Реєстрація доступна лише для ролі «Клієнт»
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Вкажіть ім'я"), StringLength(100, ErrorMessage = "Ім'я задовге")]
        [Display(Name = "Ім'я")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Вкажіть e-mail"), EmailAddress(ErrorMessage = "Некоректний e-mail")]
        [StringLength(256, ErrorMessage = "E-mail задовгий")]
        [Display(Name = "E-mail")]
        public string Email { get; set; }

        [Phone(ErrorMessage = "Некоректний номер телефону"), StringLength(30, ErrorMessage = "Номер телефону задовгий")]
        [Display(Name = "Телефон")]
        public string PhoneNumber { get; set; }

        [StringLength(200, ErrorMessage = "Адреса задовга")]
        [Display(Name = "Адреса")]
        public string Address { get; set; }

        [Required(ErrorMessage = "Вкажіть пароль")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Пароль має містити щонайменше 8 символів")]
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
