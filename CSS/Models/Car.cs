using System;
using System.ComponentModel.DataAnnotations;

namespace ASTCUSA.Models
{
    public class Car
    {
        [Required(ErrorMessage = "Не вказано авто")]
        public int CarId { get; set; }

        [Required(ErrorMessage = "Вкажіть VIN-код")]
        [StringLength(17, MinimumLength = 11, ErrorMessage = "VIN-код має містити 11–17 символів")]
        [Display(Name = "VIN-код")]
        public string VinCode { get; set; }

        [Required(ErrorMessage = "Вкажіть марку")]
        [Display(Name = "Марка")]
        public string Make { get; set; }

        [Required(ErrorMessage = "Вкажіть модель")]
        [Display(Name = "Модель")]
        public string Model { get; set; }

        [Required(ErrorMessage = "Вкажіть рік")]
        [Range(1950, 2100, ErrorMessage = "Некоректний рік")]
        [Display(Name = "Рік")]
        public int Year { get; set; }

        [Required(ErrorMessage = "Вкажіть ціну")]
        [Range(1, 10000000, ErrorMessage = "Ціна має бути більшою за 0")]
        [Display(Name = "Ціна на аукціоні, $")]
        public double AuctionPrice { get; set; }

        [Display(Name = "Статус")]
        public CarStatus Status { get; set; }

        // getDetails(): String
        public string GetDetails()
        {
            return string.Format("{0} {1} ({2}), VIN {3}, ${4:N0}", Make, Model, Year, VinCode, AuctionPrice);
        }

        // updatePrice(newPrice: double): void
        public void UpdatePrice(double newPrice)
        {
            if (newPrice <= 0) throw new ArgumentException("Ціна має бути більшою за 0.");
            AuctionPrice = newPrice;
        }
    }
}
