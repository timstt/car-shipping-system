using System.Linq;
using System.Web.Mvc;
using ASTCUSA.Models;

namespace ASTCUSA.Controllers
{
    public class HomeController : BaseController
    {
        // GET: / — опис системи для гостя або головна панель поточної ролі
        [HttpGet]
        [AllowAnonymous]
        public ActionResult Index()
        {
            if (CurrentRole == UserRole.Client && CurrentUserId.HasValue)
            {
                int clientId = CurrentUserId.Value;
                var client = db.Clients.Find(clientId);
                ViewBag.Notification = client != null ? client.Notification : null;
                ViewBag.MyOrders = db.Orders.Count(o => o.ClientId == clientId);
            }
            else if (CurrentRole == UserRole.Manager)
            {
                ViewBag.NewOrders = db.Orders.Count(o => o.Status == OrderStatus.New || o.Status == OrderStatus.DocumentsProvided);
                ViewBag.WaitingPayment = db.Orders.Count(o => o.Status == OrderStatus.PaymentPending);
            }
            else if (CurrentRole == UserRole.Logistician)
            {
                ViewBag.PaidOrders = db.Orders.Count(o => o.Status == OrderStatus.Paid);
                ViewBag.Shipping = db.Orders.Count(o => o.Status == OrderStatus.Shipping);
            }
            return View();
        }
    }
}
