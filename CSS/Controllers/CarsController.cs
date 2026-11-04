using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using ASTCUSA.Models;

namespace ASTCUSA.Controllers
{
    // Прецеденти «Переглянути каталог авто», «Шукати за фільтром», «Додати авто в обране»
    // і ведення каталогу менеджером
    public class CarsController : BaseController
    {
        private const string DuplicateVin = "Авто з таким VIN-кодом уже є в каталозі.";

        private CarCatalog Catalog { get { return new CarCatalog(db.Cars); } }

        private List<int> FavoriteIds
        {
            get
            {
                var list = Session["Favorites"] as List<int>;
                if (list == null) { list = new List<int>(); Session["Favorites"] = list; }
                return list;
            }
        }

        // GET: Cars — каталог з пошуком. Клієнт бачить лише доступні авто.
        [HttpGet]
        public ActionResult Index(CarSearchFilter filter)
        {
            bool onlyAvailable = CurrentRole == UserRole.Client;
            filter = filter ?? new CarSearchFilter();
            var model = new CarCatalogViewModel
            {
                Filter = filter,
                Cars = onlyAvailable ? SearchAvailable(filter) : Catalog.Search(filter, false),
                FavoriteIds = FavoriteIds
            };
            ViewBag.OnlyAvailable = onlyAvailable;
            return View(model);
        }

        // Каталог для клієнта (лише доступні авто): без фільтра — getAvailableCars(),
        // лише за маркою — findCarByMake(), інакше — повний пошук за фільтром
        private Car[] SearchAvailable(CarSearchFilter filter)
        {
            bool onlyMake = string.IsNullOrWhiteSpace(filter.Model) &&
                            !filter.YearFrom.HasValue && !filter.YearTo.HasValue &&
                            !filter.PriceFrom.HasValue && !filter.PriceTo.HasValue;
            if (onlyMake && string.IsNullOrWhiteSpace(filter.Make))
                return Catalog.GetAvailableCars();
            if (onlyMake)
                return Catalog.FindCarByMake(filter.Make)
                              .Where(c => c.Status == CarStatus.Available)
                              .ToArray();
            return Catalog.Search(filter, true);
        }

        // GET: Cars/Details/5
        [HttpGet]
        public ActionResult Details(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            Car car = db.Cars.Find(id);
            if (car == null) return HttpNotFound();
            ViewBag.IsFavorite = FavoriteIds.Contains(car.CarId);
            ViewBag.HasOrders = db.Orders.Any(o => o.CarId == car.CarId);
            return View(car);
        }

        // GET: Cars/Create
        [HttpGet]
        [AllowRoles(UserRole.Manager)]
        public ActionResult Create()
        {
            return View(new Car { Year = DateTime.Now.Year - 3 });
        }

        // POST: Cars/Create — «авто додано до каталогу» → стан «Доступно»
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowRoles(UserRole.Manager)]
        public ActionResult Create([Bind(Include = "VinCode,Make,Model,Year,AuctionPrice")] Car car)
        {
            if (db.Cars.Any(c => c.VinCode == car.VinCode))
                ModelState.AddModelError("VinCode", "Авто з таким VIN-кодом уже є в каталозі.");

            if (!ModelState.IsValid) return View(car);

            car.Status = CarStatus.Available;
            db.Cars.Add(car);
            try
            {
                db.SaveChanges();
            }
            catch (DbUpdateException)
            {
                // Унікальний індекс IX_Car_VinCode: такий самий VIN щойно додав інший менеджер
                ModelState.AddModelError("VinCode", DuplicateVin);
                return View(car);
            }
            TempData["Message"] = "Авто додано до каталогу.";
            return RedirectToAction("Index");
        }

        // GET: Cars/Edit/5
        [HttpGet]
        [AllowRoles(UserRole.Manager)]
        public ActionResult Edit(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            Car car = db.Cars.Find(id);
            if (car == null) return HttpNotFound();
            return View(car);
        }

        // POST: Cars/Edit/5 — оновлюються лише дані авто; ціна через Car.UpdatePrice.
        // Статус змінюється тільки бізнес-діями (аукціон, оплата, доставка).
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowRoles(UserRole.Manager)]
        public ActionResult Edit([Bind(Include = "CarId,VinCode,Make,Model,Year,AuctionPrice,RowVersion")] Car form)
        {
            var car = db.Cars.Find(form.CarId);
            if (car == null) return HttpNotFound();

            if (db.Cars.Any(c => c.VinCode == form.VinCode && c.CarId != form.CarId))
                ModelState.AddModelError("VinCode", DuplicateVin);

            if (!ModelState.IsValid)
            {
                form.Status = car.Status;
                return View(form);
            }

            // Перевірка паралельного доступу: порівнюємо з версією, яку менеджер бачив у формі,
            // а не з щойно прочитаною. Якщо хтось уже змінив авто, UPDATE не знайде рядок → виняток.
            if (form.RowVersion != null)
                db.Entry(car).Property(c => c.RowVersion).OriginalValue = form.RowVersion;

            car.VinCode = form.VinCode;
            car.Make = form.Make;
            car.Model = form.Model;
            car.Year = form.Year;
            car.UpdatePrice(form.AuctionPrice);
            try
            {
                db.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Показати актуальні дані з БД і нову версію, щоб менеджер міг повторити зміну
                db.Entry(car).Reload();
                ModelState.Clear();
                ModelState.AddModelError("", ConcurrencyMessage);
                return View(car);
            }
            catch (DbUpdateException)
            {
                form.Status = car.Status;
                ModelState.AddModelError("VinCode", DuplicateVin);
                return View(form);
            }
            TempData["Message"] = "Дані авто оновлено.";
            return RedirectToAction("Details", new { id = car.CarId });
        }

        // GET: Cars/Delete/5
        [HttpGet]
        [AllowRoles(UserRole.Manager)]
        public ActionResult Delete(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            Car car = db.Cars.Find(id);
            if (car == null) return HttpNotFound();
            ViewBag.OrdersCount = db.Orders.Count(o => o.CarId == car.CarId);
            return View(car);
        }

        // POST: Cars/Delete/5 — не видаляємо авто, до якого прив'язані заявки
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [AllowRoles(UserRole.Manager)]
        public ActionResult DeleteConfirmed(int id)
        {
            Car car = db.Cars.Find(id);
            if (car == null)
            {
                TempData["Message"] = "Авто вже видалено.";
                return RedirectToAction("Index");
            }
            int orders = db.Orders.Count(o => o.CarId == id);
            if (orders > 0)
            {
                TempData["Message"] = "Неможливо видалити авто: з ним пов'язано заявок — " + orders + ".";
                return RedirectToAction("Details", new { id = id });
            }
            db.Cars.Remove(car);
            try
            {
                db.SaveChanges();
            }
            catch (DbUpdateException)
            {
                // Між перевіркою і видаленням на авто створили заявку (FK) або авто змінили (RowVersion)
                TempData["Message"] = "Авто не видалено: його щойно змінили або на нього створили заявку. Спробуйте ще раз.";
                return RedirectToAction("Details", new { id = id });
            }
            FavoriteIds.Remove(id);
            TempData["Message"] = "Авто видалено з каталогу.";
            return RedirectToAction("Index");
        }

        // POST: Cars/AddToFavorites/5 — «Додати авто в обране» (extend «Переглянути каталог»)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowRoles(UserRole.Client)]
        public ActionResult AddToFavorites(int id, string returnUrl)
        {
            if (db.Cars.Find(id) != null && !FavoriteIds.Contains(id))
            {
                FavoriteIds.Add(id);
                TempData["Message"] = "Авто додано в обране.";
            }
            return RedirectToLocal(returnUrl);
        }

        // POST: Cars/RemoveFromFavorites/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowRoles(UserRole.Client)]
        public ActionResult RemoveFromFavorites(int id, string returnUrl)
        {
            FavoriteIds.Remove(id);
            TempData["Message"] = "Авто прибрано з обраного.";
            return RedirectToLocal(returnUrl);
        }

        // GET: Cars/Favorites
        [HttpGet]
        [AllowRoles(UserRole.Client)]
        public ActionResult Favorites()
        {
            return View(Catalog.GetByIds(FavoriteIds));
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return RedirectToAction("Index");
        }
    }
}
