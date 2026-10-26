using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace ASTCUSA.Models
{
    // Стани авто за діаграмою станів (statediagram)
    public enum CarStatus
    {
        [Display(Name = "Доступно")] Available,
        [Display(Name = "Аукціон")] Auction,
        [Display(Name = "Зарезервовано")] Reserved,
        [Display(Name = "Оплачено")] Paid,
        [Display(Name = "В дорозі")] InTransit,
        [Display(Name = "Готово до видачі")] ReadyForPickup,
        [Display(Name = "Доставлено")] Delivered,
        [Display(Name = "Продано іншому")] SoldToOther
    }

    // Стани заявки за діаграмою діяльності.
    // Нові значення додаються в кінець, щоб не змінювати числові коди вже збережених заявок.
    public enum OrderStatus
    {
        [Display(Name = "Нова")] New,
        [Display(Name = "Запитано документи")] DocumentsRequested,
        [Display(Name = "В роботі")] InProgress,
        [Display(Name = "Ставку виграно")] BidWon,
        [Display(Name = "Ставку програно")] BidLost,
        [Display(Name = "Виставлено інвойс")] Invoiced,
        [Display(Name = "Оплата очікує підтвердження")] PaymentPending,
        [Display(Name = "Оплачено")] Paid,
        [Display(Name = "Перевезення")] Shipping,
        [Display(Name = "Завершено")] Completed,
        [Display(Name = "Скасовано")] Cancelled,
        [Display(Name = "Документи надано")] DocumentsProvided
    }

    public enum UserRole
    {
        [Display(Name = "Гість")] None,
        [Display(Name = "Клієнт")] Client,
        [Display(Name = "Менеджер")] Manager,
        [Display(Name = "Логіст")] Logistician
    }

    public static class EnumExtensions
    {
        // Повертає українську назву зі [Display(Name = ...)]
        public static string GetDisplayName(this Enum value)
        {
            var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
            var attr = member?.GetCustomAttribute<DisplayAttribute>();
            return attr?.Name ?? value.ToString();
        }

        public static string BadgeClass(this OrderStatus status)
        {
            switch (status)
            {
                case OrderStatus.Completed: return "bg-success";
                case OrderStatus.Cancelled:
                case OrderStatus.BidLost: return "bg-danger";
                case OrderStatus.DocumentsRequested:
                case OrderStatus.Invoiced:
                case OrderStatus.PaymentPending: return "bg-warning text-dark";
                case OrderStatus.Paid:
                case OrderStatus.Shipping: return "bg-info text-dark";
                case OrderStatus.New: return "bg-secondary";
                default: return "bg-primary";
            }
        }

        public static string BadgeClass(this CarStatus status)
        {
            switch (status)
            {
                case CarStatus.Available: return "bg-success";
                case CarStatus.SoldToOther: return "bg-danger";
                case CarStatus.Delivered: return "bg-dark";
                case CarStatus.Auction:
                case CarStatus.Reserved: return "bg-warning text-dark";
                default: return "bg-info text-dark";
            }
        }
    }
}
