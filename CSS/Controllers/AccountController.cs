using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Web.Mvc;
using ASTCUSA.Models;

namespace ASTCUSA.Controllers
{
    // Прецедент «Автентифікація / Реєстрація»: вхід, реєстрація клієнта, вихід
    [AllowAnonymous]
    public class AccountController : BaseController
    {
        private const string InvalidLogin = "Невірний email або пароль";

        // GET: Account/Login
        [HttpGet]
        public ActionResult Login(string returnUrl)
        {
            if (Request.IsAuthenticated) return RedirectToLocal(returnUrl);
            ViewBag.ReturnUrl = returnUrl;
            return View(new LoginViewModel());
        }

        // POST: Account/Login — User.Login(): перевірка пароля і видача авторизаційного cookie
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model, string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            if (!ModelState.IsValid) return View(model);

            string email = Models.User.NormalizeEmail(model.Email);
            var user = db.Users.FirstOrDefault(u => u.Email == email);

            bool ok;
            if (user == null)
            {
                PasswordHasher.VerifyDummy(model.Password); // однаковий час відповіді для будь-якого email
                ok = false;
            }
            else
            {
                ok = user.Login(model.Password, HttpContext);
            }

            if (!ok)
            {
                // Не розкриваємо, що саме невірне — email чи пароль
                ModelState.AddModelError("", InvalidLogin);
                model.Password = null;
                return View(model);
            }

            TempData["Message"] = "Ви увійшли як " + user.Name + " (" + user.Role.GetDisplayName() + ").";
            return RedirectToLocal(returnUrl);
        }

        // GET: Account/Register — реєстрація лише клієнта
        [HttpGet]
        public ActionResult Register()
        {
            if (Request.IsAuthenticated) return RedirectToAction("Index", "Home");
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(RegisterViewModel model)
        {
            if (Request.IsAuthenticated) return RedirectToAction("Index", "Home");

            string email = Models.User.NormalizeEmail(model.Email);
            if (ModelState.IsValidField("Email") && db.Users.Any(u => u.Email == email))
                ModelState.AddModelError("Email", "Користувач із таким e-mail уже зареєстрований.");

            if (!ModelState.IsValid)
            {
                model.Password = model.ConfirmPassword = null;
                return View(model);
            }

            var client = new Client
            {
                Name = model.Name.Trim(),
                Email = email,
                PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
                Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim()
            };
            client.SetPassword(model.Password);
            db.Clients.Add(client);
            try
            {
                db.SaveChanges();
            }
            catch (DbUpdateException)
            {
                // Унікальний індекс на Email: одночасна реєстрація з тим самим e-mail
                ModelState.AddModelError("Email", "Користувач із таким e-mail уже зареєстрований.");
                model.Password = model.ConfirmPassword = null;
                return View(model);
            }

            client.Login(model.Password, HttpContext);
            TempData["Message"] = "Реєстрацію завершено. Вітаємо, " + client.Name + "!";
            return RedirectToAction("Index", "Home");
        }

        // POST: Account/Logout — User.Logout()
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            var user = CurrentUserId.HasValue ? db.Users.Find(CurrentUserId.Value) : null;
            if (user != null) user.Logout(HttpContext);
            else Models.User.SignOut(HttpContext);

            TempData["Message"] = "Ви вийшли із системи.";
            return RedirectToAction("Index", "Home");
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }
    }
}
