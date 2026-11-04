using System.Linq;
using System.Web.Mvc;
using ASTCUSA.Models;

namespace ASTCUSA.Controllers
{
    // Профіль клієнта: перегляд, зміна даних і видалення облікового запису (ЛР5 — CRUD для користувача).
    // Працівники (менеджер, логіст) створюються під час заповнення бази, а не через сайт.
    [AllowRoles(UserRole.Client)]
    public class ProfileController : BaseController
    {
        private Client LoadClient()
        {
            return CurrentUserId.HasValue ? db.Clients.Find(CurrentUserId.Value) : null;
        }

        private ProfileViewModel ToModel(Client client)
        {
            return new ProfileViewModel
            {
                Email = client.Email,
                Name = client.Name,
                PhoneNumber = client.PhoneNumber,
                Address = client.Address,
                OrdersCount = db.Orders.Count(o => o.ClientId == client.Id)
            };
        }

        // GET: Profile
        [HttpGet]
        public ActionResult Index()
        {
            var client = LoadClient();
            if (client == null) return HttpNotFound();
            return View(ToModel(client));
        }

        // GET: Profile/Edit
        [HttpGet]
        public ActionResult Edit()
        {
            var client = LoadClient();
            if (client == null) return HttpNotFound();
            return View(ToModel(client));
        }

        // POST: Profile/Edit — e-mail і пароль тут не змінюються
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Name,PhoneNumber,Address")] ProfileViewModel model)
        {
            var client = LoadClient();
            if (client == null) return HttpNotFound();
            if (!ModelState.IsValid)
            {
                model.Email = client.Email;
                return View(model);
            }

            client.Name = model.Name.Trim();
            client.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
            client.Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim();
            db.SaveChanges();

            // Ім'я зберігається в авторизаційному cookie — видати новий, щоб у меню було нове ім'я
            Response.Cookies.Add(AuthTicket.CreateCookie(client));
            TempData["Message"] = "Профіль оновлено.";
            return RedirectToAction("Index");
        }

        // GET: Profile/Delete — підтвердження видалення облікового запису
        [HttpGet]
        public ActionResult Delete()
        {
            var client = LoadClient();
            if (client == null) return HttpNotFound();
            return View(ToModel(client));
        }

        // POST: Profile/Delete — видалити можна лише клієнта без заявок: заявки — історія угод
        // (інвойси, доставки), а зовнішній ключ Orders.ClientId має каскадне видалення
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed()
        {
            var client = LoadClient();
            if (client == null) return HttpNotFound();

            int orders = db.Orders.Count(o => o.ClientId == client.Id);
            if (orders > 0)
            {
                TempData["Message"] = "Обліковий запис не можна видалити: у вас є заявки (" + orders + ").";
                return RedirectToAction("Index");
            }

            db.Clients.Remove(client);
            db.SaveChanges();
            Models.User.SignOut(HttpContext);
            TempData["Message"] = "Обліковий запис видалено.";
            return RedirectToAction("Index", "Home");
        }
    }
}
