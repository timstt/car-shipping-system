using System.ComponentModel.DataAnnotations;

namespace ASTCUSA.Models
{
    public class Insurance
    {
        public int InsuranceId { get; set; }

        [Required(ErrorMessage = "Вкажіть тип страховки")]
        [Display(Name = "Тип страховки")]
        public string Type { get; set; }

        [Range(1, 1000000, ErrorMessage = "Ціна має бути більшою за 0")]
        [Display(Name = "Ціна, $")]
        public double Price { get; set; }

        public int OrderId { get; set; }
        public virtual Order Order { get; set; }

        // getCoverageDetails(): String
        public string GetCoverageDetails()
        {
            return string.Format("{0} — ${1:N0}", Type, Price);
        }
    }
}
