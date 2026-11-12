using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;

namespace ASTCUSA.Models
{
    public enum BidResult
    {
        [Display(Name = "Очікує результату")] Pending,
        [Display(Name = "Перебито")] Outbid,
        [Display(Name = "Виграно")] Won,
        [Display(Name = "Програно")] Lost
    }

    // Ставка менеджера на зовнішньому аукціоні (Copart, IAAI, Manheim) за заявкою клієнта
    // (Order 1 — 0..* AuctionBid). Сама ставка робиться на сайті аукціону, а система зберігає
    // лот, посилання на нього, суму й результат торгів — так клієнт бачить історію ставок.
    public class AuctionBid
    {
        public static readonly string[] Auctions = { "Copart", "IAAI", "Manheim", "Інший" };

        public int AuctionBidId { get; set; }

        public int OrderId { get; set; }
        public virtual Order Order { get; set; }

        [Required, StringLength(50)]
        [Display(Name = "Аукціон")]
        public string Auction { get; set; }

        [Required, StringLength(30)]
        [Display(Name = "Номер лоту")]
        public string LotNumber { get; set; }

        [Required, StringLength(500)]
        [Display(Name = "Посилання на лот")]
        public string LotUrl { get; set; }

        [Display(Name = "Сума ставки, $")]
        public double Amount { get; set; }

        [Display(Name = "Результат")]
        public BidResult Result { get; set; }

        // Ставка іншого учасника, яка перебила нашу (для «Перебито» і «Програно»). Аукціони не розкривають,
        // хто поставив, тому зберігається лише сума — менеджер бачить її на сайті аукціону.
        [Display(Name = "Ставка іншого учасника, $")]
        public double? CompetitorAmount { get; set; }

        // Найбільша ставка за весь час торгів за заявкою: наша або іншого учасника. Нова ставка має бути більшою.
        public static double HighestAmount(IEnumerable<AuctionBid> bids)
        {
            return bids.Where(b => b.Result != BidResult.Pending)
                       .Select(b => Math.Max(b.Amount, b.CompetitorAmount ?? 0))
                       .DefaultIfEmpty(0).Max();
        }

        [Display(Name = "Дата ставки")]
        public DateTime PlacedAt { get; set; }

        // Перевірити й записати дані лоту та суму; кидає ArgumentException з поясненням
        public void SetDetails(string auction, string lotNumber, string lotUrl, double amount)
        {
            auction = (auction ?? "").Trim();
            if (Array.IndexOf(Auctions, auction) < 0) throw new ArgumentException("Оберіть аукціон зі списку.");

            lotNumber = (lotNumber ?? "").Trim();
            if (!Regex.IsMatch(lotNumber, @"^[A-Za-z0-9\-]{1,30}$"))
                throw new ArgumentException("Номер лоту: латинські літери, цифри або дефіс, до 30 символів.");

            // Лише повне посилання http/https: інакше в href могло б потрапити, наприклад, javascript:
            Uri uri;
            lotUrl = (lotUrl ?? "").Trim();
            if (lotUrl.Length > 500 || !Uri.TryCreate(lotUrl, UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                throw new ArgumentException("Вкажіть повне посилання на лот, що починається з https://.");

            if (amount <= 0) throw new ArgumentException("Сума ставки має бути більшою за 0.");

            Auction = auction;
            LotNumber = lotNumber;
            LotUrl = uri.AbsoluteUri;
            Amount = amount;
        }

        public static string BadgeClass(BidResult result)
        {
            switch (result)
            {
                case BidResult.Won: return "bg-success";
                case BidResult.Lost: return "bg-danger";
                case BidResult.Outbid: return "bg-secondary";
                default: return "bg-warning text-dark";
            }
        }
    }
}
