using System;
using System.Collections.Generic;
using ASTCUSA.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ASTCUSA.Tests
{
    // Правила аукціону: Manager.PlaceBid і Manager.SetBidResult
    [TestClass]
    public class AuctionBidTests
    {
        private const string LotUrl = "https://www.copart.com/lot/12345678";

        private static Order OrderInWork(double budget = 20000)
        {
            return new Order
            {
                OrderId = 1,
                MaxBudget = budget,
                Status = OrderStatus.InProgress,
                Car = new Car { CarId = 1, AuctionPrice = 15000, Status = CarStatus.Available },
                Bids = new List<AuctionBid>()
            };
        }

        [TestMethod]
        public void PlaceBid_RecordsPendingBid_AndPutsCarOnAuction()
        {
            var order = OrderInWork();

            var bid = new Manager().PlaceBid(order, "Copart", "12345678", LotUrl, 16000);

            Assert.AreEqual(BidResult.Pending, bid.Result);
            Assert.AreEqual(CarStatus.Auction, order.Car.Status);
            Assert.AreEqual(1, order.Bids.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void PlaceBid_AboveClientBudget_IsRejected()
        {
            new Manager().PlaceBid(OrderInWork(budget: 20000), "Copart", "12345678", LotUrl, 20001);
        }

        [TestMethod]
        public void PlaceBid_AfterOutbid_MustBeHigherThanOtherBidder()
        {
            var manager = new Manager();
            var order = OrderInWork();
            var first = manager.PlaceBid(order, "Copart", "12345678", LotUrl, 16000);
            manager.SetBidResult(order, first, BidResult.Outbid, competitorAmount: 17000);

            Assert.AreEqual(CarStatus.Available, order.Car.Status);
            Assert.ThrowsException<ArgumentException>(() => manager.PlaceBid(order, "Copart", "12345678", LotUrl, 17000));
            manager.PlaceBid(order, "Copart", "12345678", LotUrl, 17500);
        }

        [TestMethod]
        public void SetBidResult_Won_ReservesCar_AndSetsFinalPrice()
        {
            var manager = new Manager();
            var order = OrderInWork();
            var bid = manager.PlaceBid(order, "IAAI", "A-77", "https://www.iaai.com/lot/77", 18000);

            manager.SetBidResult(order, bid, BidResult.Won);

            Assert.AreEqual(OrderStatus.BidWon, order.Status);
            Assert.AreEqual(CarStatus.Reserved, order.Car.Status);
            Assert.AreEqual(18000, order.FinalPrice);
        }

        [TestMethod]
        public void PlaceBid_LinkMustBeHttp()
        {
            Assert.ThrowsException<ArgumentException>(() =>
                new Manager().PlaceBid(OrderInWork(), "Copart", "12345678", "javascript:alert(1)", 16000));
        }
    }
}
