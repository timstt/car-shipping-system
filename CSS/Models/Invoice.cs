using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASTCUSA.Models
{
    // «interface» IPayable
    public interface IPayable
    {
        bool CheckPaymentStatus();
        void MarkAsPaid();
    }

    public class Invoice : IPayable
    {
        // invoiceId: int — власний первинний ключ, як на діаграмі
        [Display(Name = "№ інвойсу")]
        public int InvoiceId { get; set; }

        // Зовнішній ключ на заявку; унікальний індекс гарантує не більше одного інвойсу на заявку (0..1)
        [Index("IX_Invoice_OrderId", IsUnique = true)]
        public int OrderId { get; set; }
        public virtual Order Order { get; set; }

        [Display(Name = "Сума, $")]
        public double TotalAmount { get; set; }

        [Display(Name = "Оплачено")]
        public bool IsPaid { get; set; }

        public bool CheckPaymentStatus() { return IsPaid; }
        public void MarkAsPaid() { IsPaid = true; }
    }
}
