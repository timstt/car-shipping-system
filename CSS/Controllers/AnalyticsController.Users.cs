using System.Linq;
using System.Web.Mvc;
using ASTCUSA.Models;
using ASTCUSA.Models.Analytics;

namespace ASTCUSA.Controllers
{
    // Таблиця Users: перегляд, додавання, зміна, видалення
    public partial class AnalyticsController
    {
        // GET: Analytics/UsersTable
        [HttpGet]
        public ActionResult UsersTable()
        {
            return View(AnalyticsDb.Users.OrderBy(u => u.Id).ToList());
        }

        // GET: Analytics/CreateUser
        [HttpGet]
        public ActionResult CreateUser()
        {
            return UserForm(new UserInput { Role = UserRole.Client.ToString() });
        }

        // POST: Analytics/CreateUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateUser([Bind(Include = "Name,Email,Role,PhoneNumber,Address")] UserInput input)
        {
            ValidateUser(input, null);
            if (!ModelState.IsValid) return UserForm(input);

            var user = new AnalyticsUser();
            CopyUser(input, user);
            AnalyticsDb.Users.Add(user);
            if (!TrySave("Не вдалося додати користувача. Перевірте введені дані.")) return UserForm(input);

            TempData["Message"] = "Користувача №" + user.Id + " додано.";
            return RedirectToAction("UsersTable");
        }

        // GET: Analytics/EditUser/5
        [HttpGet]
        public ActionResult EditUser(int? id)
        {
            var user = id.HasValue ? AnalyticsDb.Users.Find(id.Value) : null;
            if (user == null) return NotFoundRedirect("Користувача не знайдено.", "UsersTable");

            return UserForm(new UserInput
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                PhoneNumber = user.PhoneNumber,
                Address = user.Address
            });
        }

        // POST: Analytics/EditUser/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditUser([Bind(Include = "Id,Name,Email,Role,PhoneNumber,Address")] UserInput input)
        {
            var user = input.Id.HasValue ? AnalyticsDb.Users.Find(input.Id.Value) : null;
            if (user == null) return NotFoundRedirect("Користувача не знайдено.", "UsersTable");

            ValidateUser(input, user);
            if (!ModelState.IsValid) return UserForm(input);

            CopyUser(input, user);
            if (!TrySave("Не вдалося змінити користувача. Перевірте введені дані.")) return UserForm(input);

            TempData["Message"] = "Користувача №" + user.Id + " змінено.";
            return RedirectToAction("UsersTable");
        }

        // POST: Analytics/DeleteUser/5 — не можна видалити користувача, пов'язаного із заявками чи доставками
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteUser(int? id)
        {
            var user = id.HasValue ? AnalyticsDb.Users.Find(id.Value) : null;
            if (user == null) return NotFoundRedirect("Користувача не знайдено.", "UsersTable");

            int orders = AnalyticsDb.Orders.Count(o => o.ClientId == user.Id || o.ManagerId == user.Id);
            int deliveries = AnalyticsDb.Deliveries.Count(d => d.LogisticianId == user.Id);
            if (orders > 0 || deliveries > 0)
            {
                TempData["Message"] = string.Format(
                    "Користувача №{0} не можна видалити: з ним пов'язано заявок — {1}, доставок — {2}.",
                    user.Id, orders, deliveries);
                return RedirectToAction("UsersTable");
            }

            AnalyticsDb.Users.Remove(user);
            TempData["Message"] = TrySave("")
                ? "Користувача №" + id.Value + " видалено."
                : "Користувача №" + id.Value + " не вдалося видалити.";
            return RedirectToAction("UsersTable");
        }

        private ActionResult UserForm(UserInput input)
        {
            ViewBag.Roles = AnalyticsChoices.Enum(input.Role, UserRole.None);
            return View("UserForm", input);
        }

        private void ValidateUser(UserInput input, AnalyticsUser existing)
        {
            if (!string.IsNullOrEmpty(input.Role) && (!AnalyticsChoices.IsEnumName<UserRole>(input.Role) || input.Role == UserRole.None.ToString()))
                ModelState.AddModelError("Role", "Невідома роль");

            string email = Clean(input.Email);
            int ownId = existing != null ? existing.Id : 0;
            if (email != null && ModelState.IsValidField("Email")
                && AnalyticsDb.Users.Any(u => u.Email == email && u.Id != ownId))
                ModelState.AddModelError("Email", "Користувач з таким e-mail уже є");

            // Роль не можна змінити, якщо користувач уже виконує стару роль у заявках чи доставках
            if (existing != null && ModelState.IsValidField("Role") && input.Role != existing.Role)
            {
                bool busy = AnalyticsDb.Orders.Any(o => o.ClientId == existing.Id || o.ManagerId == existing.Id)
                    || AnalyticsDb.Deliveries.Any(d => d.LogisticianId == existing.Id);
                if (busy)
                    ModelState.AddModelError("Role", "Роль не можна змінити: користувач уже пов'язаний із заявками або доставками");
            }
        }

        private static void CopyUser(UserInput input, AnalyticsUser user)
        {
            bool client = input.Role == UserRole.Client.ToString();
            user.Name = input.Name.Trim();
            user.Email = input.Email.Trim();
            user.Role = input.Role;
            // Телефон і адреса є лише в клієнта
            user.PhoneNumber = client ? PhoneNumbers.Normalize(input.PhoneNumber) : null;
            user.Address = client ? Clean(input.Address) : null;
        }

        private ActionResult NotFoundRedirect(string message, string action)
        {
            TempData["Message"] = message;
            return RedirectToAction(action);
        }
    }
}
