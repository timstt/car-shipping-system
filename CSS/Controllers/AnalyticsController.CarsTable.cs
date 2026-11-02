using System.Linq;
using System.Web.Mvc;
using ASTCUSA.Models;
using ASTCUSA.Models.Analytics;

namespace ASTCUSA.Controllers
{
    // Таблиця Cars: перегляд, додавання, зміна, видалення
    // (вкладка «Авто» з ЛР3 — це окремий пошук, вона не змінюється)
    public partial class AnalyticsController
    {
        // GET: Analytics/CarsTable
        [HttpGet]
        public ActionResult CarsTable()
        {
            return View(AnalyticsDb.Cars.OrderBy(c => c.CarId).ToList());
        }

        // GET: Analytics/CreateCar
        [HttpGet]
        public ActionResult CreateCar()
        {
            return CarForm(new CarInput { Status = CarStatus.Available.ToString() });
        }

        // POST: Analytics/CreateCar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateCar([Bind(Include = "VinCode,Make,Model,Year,AuctionPrice,Status")] CarInput input)
        {
            ValidateCar(input, null);
            if (!ModelState.IsValid) return CarForm(input);

            var car = new AnalyticsCar();
            CopyCar(input, car);
            AnalyticsDb.Cars.Add(car);
            if (!TrySave("Не вдалося додати авто. Перевірте введені дані.")) return CarForm(input);

            TempData["Message"] = "Авто №" + car.CarId + " додано.";
            return RedirectToAction("CarsTable");
        }

        // GET: Analytics/EditCar/5
        [HttpGet]
        public ActionResult EditCar(int? id)
        {
            var car = id.HasValue ? AnalyticsDb.Cars.Find(id.Value) : null;
            if (car == null) return NotFoundRedirect("Авто не знайдено.", "CarsTable");

            return CarForm(new CarInput
            {
                CarId = car.CarId,
                VinCode = car.VinCode,
                Make = car.Make,
                Model = car.Model,
                Year = car.Year,
                AuctionPrice = car.AuctionPrice,
                Status = car.Status
            });
        }

        // POST: Analytics/EditCar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditCar([Bind(Include = "CarId,VinCode,Make,Model,Year,AuctionPrice,Status")] CarInput input)
        {
            var car = input.CarId.HasValue ? AnalyticsDb.Cars.Find(input.CarId.Value) : null;
            if (car == null) return NotFoundRedirect("Авто не знайдено.", "CarsTable");

            ValidateCar(input, car);
            if (!ModelState.IsValid) return CarForm(input);

            CopyCar(input, car);
            if (!TrySave("Не вдалося змінити авто. Перевірте введені дані.")) return CarForm(input);

            TempData["Message"] = "Авто №" + car.CarId + " змінено.";
            return RedirectToAction("CarsTable");
        }

        // POST: Analytics/DeleteCar/5 — авто, на яке є заявки, видалити не можна
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteCar(int? id)
        {
            var car = id.HasValue ? AnalyticsDb.Cars.Find(id.Value) : null;
            if (car == null) return NotFoundRedirect("Авто не знайдено.", "CarsTable");

            int orders = AnalyticsDb.Orders.Count(o => o.CarId == car.CarId);
            if (orders > 0)
            {
                TempData["Message"] = string.Format("Авто №{0} не можна видалити: на нього є заявки ({1}).", car.CarId, orders);
                return RedirectToAction("CarsTable");
            }

            AnalyticsDb.Cars.Remove(car);
            TempData["Message"] = TrySave("")
                ? "Авто №" + id.Value + " видалено."
                : "Авто №" + id.Value + " не вдалося видалити.";
            return RedirectToAction("CarsTable");
        }

        private ActionResult CarForm(CarInput input)
        {
            ViewBag.Statuses = AnalyticsChoices.Enum<CarStatus>(input.Status);
            return View("CarForm", input);
        }

        private void ValidateCar(CarInput input, AnalyticsCar existing)
        {
            if (!string.IsNullOrEmpty(input.Status) && !AnalyticsChoices.IsEnumName<CarStatus>(input.Status))
                ModelState.AddModelError("Status", "Невідомий статус авто");

            string vin = Clean(input.VinCode);
            int ownId = existing != null ? existing.CarId : 0;
            if (vin != null && ModelState.IsValidField("VinCode")
                && AnalyticsDb.Cars.Any(c => c.VinCode == vin && c.CarId != ownId))
                ModelState.AddModelError("VinCode", "Авто з таким VIN уже є");
        }

        private static void CopyCar(CarInput input, AnalyticsCar car)
        {
            car.VinCode = input.VinCode.Trim().ToUpperInvariant();
            car.Make = input.Make.Trim();
            car.Model = input.Model.Trim();
            car.Year = input.Year.Value;
            car.AuctionPrice = input.AuctionPrice.Value;
            car.Status = input.Status;
        }
    }
}
