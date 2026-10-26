using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using ASTCUSA.Models;

namespace ASTCUSA.Controllers
{
    // Заявки: прецеденти клієнта, менеджера і логіста за діаграмами прецедентів і діяльності.
    // Усі дії, що змінюють дані, — лише [HttpPost] + [ValidateAntiForgeryToken].
    public class OrdersController : BaseController
    {
        // ---------- Допоміжні методи ----------

        private Order LoadOrder(int id)
        {
            return db.Orders
                .Include(o => o.Car)
                .Include(o => o.Client)
                .Include(o => o.Manager)
                .Include(o => o.Invoices)
                .Include(o => o.Delivery)
                .Include(o => o.Delivery.Logistician)
                .Include(o => o.Insurances)
                .FirstOrDefault(o => o.OrderId == id);
        }

        // Клієнт бачить лише свої заявки, логіст — лише оплачені й далі
        private bool CanView(Order order)
        {
            switch (CurrentRole)
            {
                case UserRole.Client: return order.ClientId == CurrentUserId;
                case UserRole.Manager: return true;
                case UserRole.Logistician:
                    return order.Status == OrderStatus.Paid || order.Status == OrderStatus.Shipping ||
                           order.Status == OrderStatus.Completed;
                default: return false;
            }
        }

        // Завантажити заявку і перевірити доступ; null — заявку не знайдено або вона чужа
        private Order FindAccessibleOrder(int id)
        {
            var order = LoadOrder(id);
            return order != null && CanView(order) ? order : null;
        }

        private ActionResult OrderNotFound()
        {
            TempData["Message"] = "Заявку не знайдено або вона вам недоступна.";
            return RedirectToAction("Index");
        }

        private Client CurrentClient { get { return CurrentUserId.HasValue ? db.Clients.Find(CurrentUserId.Value) : null; } }
        private Manager CurrentManager { get { return CurrentUserId.HasValue ? db.Managers.Find(CurrentUserId.Value) : null; } }
        private Logistician CurrentLogistician { get { return CurrentUserId.HasValue ? db.Logisticians.Find(CurrentUserId.Value) : null; } }

        // Авто можна обрати, якщо воно «Доступно» і не входить до іншої активної заявки
        // (так у коді забезпечується кратність Car 0..1 — Order 1 з діаграми класів)
        private IQueryable<Car> FreeCars(int? exceptOrderId)
        {
            int except = exceptOrderId ?? 0;
            var inactive = Order.InactiveStatuses;
            return db.Cars.Where(c => c.Status == CarStatus.Available &&
                !db.Orders.Any(o => o.CarId == c.CarId && o.OrderId != except &&
                                    !inactive.Contains(o.Status)));
        }

        private void FillCarList(int? selectedCarId, int? exceptOrderId)
        {
            var cars = FreeCars(exceptOrderId).OrderBy(c => c.Make).ToList()
                .Select(c => new { c.CarId, Title = c.GetDetails() });
            ViewBag.CarId = new SelectList(cars, "CarId", "Title", selectedCarId);
        }

        private ActionResult Run(int id, Action action, string successMessage)
        {
            try
            {
                action();
                db.SaveChanges();
                return BackToOrder(id, successMessage);
            }
            catch (InvalidOperationException ex)
            {
                return BackToOrder(id, ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BackToOrder(id, ex.Message);
            }
        }

        // ---------- Перегляд ----------

        // GET: Orders
        [HttpGet]
        public ActionResult Index(OrderStatus? status)
        {
            int userId = CurrentUserId ?? 0;
            var orders = db.Orders.Include(o => o.Car).Include(o => o.Client).Include(o => o.Manager);
            switch (CurrentRole)
            {
                case UserRole.Client:
                    orders = orders.Where(o => o.ClientId == userId);
                    break;
                case UserRole.Logistician:
                    orders = orders.Where(o => o.Status == OrderStatus.Paid ||
                                               o.Status == OrderStatus.Shipping ||
                                               o.Status == OrderStatus.Completed);
                    break;
            }
            if (status.HasValue)
            {
                var s = status.Value;
                orders = orders.Where(o => o.Status == s);
            }

            ViewBag.Status = status;
            return View(orders.OrderByDescending(o => o.CreatedAt).ToList());
        }

        // GET: Orders/Details/5
        [HttpGet]
        public ActionResult Details(int? id)
        {
            if (id == null) return OrderNotFound();
            var order = FindAccessibleOrder(id.Value);
            if (order == null) return OrderNotFound();
            return View(order);
        }

        // ---------- Клієнт ----------

        // GET: Orders/Create — «Створити заявку на підбір авто»
        [HttpGet]
        [AllowRoles(UserRole.Client)]
        public ActionResult Create(int? carId)
        {
            FillCarList(carId, null);
            return View(new Order { CarId = carId, MaxBudget = 15000 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowRoles(UserRole.Client)]
        public ActionResult Create([Bind(Include = "MaxBudget,CarId")] Order form)
        {
            Car car = form.CarId.HasValue ? FreeCars(null).FirstOrDefault(c => c.CarId == form.CarId.Value) : null;
            if (car == null)
                ModelState.AddModelError("CarId", "Оберіть доступне авто з каталогу.");

            if (ModelState.IsValid)
            {
                var order = CurrentClient.CreateOrder(form.MaxBudget);
                order.AssignCar(car);
                db.Orders.Add(order);
                db.SaveChanges();
                TempData["Message"] = "Заявку №" + order.OrderId + " створено. Менеджер візьме її в роботу.";
                return RedirectToAction("Details", new { id = order.OrderId });
            }

            FillCarList(form.CarId, null);
            return View(form);
        }

        private static bool ClientCanEdit(Order order)
        {
            return order.Status == OrderStatus.New || order.Status == OrderStatus.DocumentsRequested ||
                   order.Status == OrderStatus.DocumentsProvided || order.Status == OrderStatus.BidLost;
        }

        // GET: Orders/Edit/5 — «Вибрати інше авто або змінити бюджет»
        [HttpGet]
        [AllowRoles(UserRole.Client)]
        public ActionResult Edit(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var order = FindAccessibleOrder(id.Value);
            if (order == null) return OrderNotFound();
            if (!ClientCanEdit(order)) return BackToOrder(order.OrderId, "На цьому етапі заявку змінити не можна.");

            FillCarList(order.CarId, order.OrderId);
            return View(order);
        }

        // POST: Orders/Edit/5 — змінюються лише бюджет і авто; інші поля (відгук, підтвердження) не чіпаються
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowRoles(UserRole.Client)]
        public ActionResult Edit([Bind(Include = "OrderId,MaxBudget,CarId")] Order form)
        {
            var order = FindAccessibleOrder(form.OrderId);
            if (order == null) return OrderNotFound();
            if (!ClientCanEdit(order)) return BackToOrder(order.OrderId, "На цьому етапі заявку змінити не можна.");

            Car newCar = null;
            if (form.CarId != order.CarId || order.Car == null || order.Car.Status != CarStatus.Available)
            {
                newCar = form.CarId.HasValue
                    ? FreeCars(order.OrderId).FirstOrDefault(c => c.CarId == form.CarId.Value)
                    : null;
                if (newCar == null)
                    ModelState.AddModelError("CarId", "Оберіть доступне авто (попереднє вже недоступне).");
            }

            if (!ModelState.IsValid)
            {
                FillCarList(form.CarId, order.OrderId);
                form.Status = order.Status;
                form.Car = order.Car;
                return View(form);
            }

            order.ChangeBudget(form.MaxBudget);
            if (newCar != null) order.AssignCar(newCar);
            // Після програної ставки клієнт продовжує пошук — заявка знову потрапляє до менеджера
            if (order.Status == OrderStatus.BidLost)
            {
                order.FinalPrice = null;
                order.UpdateStatus(OrderStatus.New);
            }
            db.SaveChanges();
            return BackToOrder(order.OrderId, "Заявку оновлено.");
        }

        // POST: Orders/Cancel/5 — «Скасувати заявку»
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowRoles(UserRole.Client)]
        public ActionResult Cancel(int id)
        {
            var order = FindAccessibleOrder(id);
            if (order == null) return OrderNotFound();
            return Run(id, () =>
            {
                if (!ClientCanEdit(order) && order.Status != OrderStatus.InProgress)
                    throw new InvalidOperationException("Заявку, за якою виграно ставку, скасувати не можна.");
                order.UpdateStatus(OrderStatus.Cancelled);
            }, "Заявку скасовано.");
        }

        // POST: Orders/ProvideDocuments/5 — «Надати документи» (клієнт)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowRoles(UserRole.Client)]
        public ActionResult ProvideDocuments(int id)
        {
            var order = FindAccessibleOrder(id);
            if (order == null) return OrderNotFound();
            return Run(id, () => CurrentClient.ProvideDocuments(order), "Документи надано, менеджер перевірить їх.");
        }

        // GET: Orders/AddInsurance/5 — «Додати страховий пакет» (extend «Створити заявку»)
        [HttpGet]
        [AllowRoles(UserRole.Client)]
        public ActionResult AddInsurance(int id)
        {
            var order = FindAccessibleOrder(id);
            if (order == null) return OrderNotFound();
            ViewBag.Order = order;
            return View(new Insurance { OrderId = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowRoles(UserRole.Client)]
        public ActionResult AddInsurance(int id, [Bind(Include = "Type,Price")] Insurance insurance)
        {
            var order = FindAccessibleOrder(id);
            if (order == null) return OrderNotFound();
            if (!ModelState.IsValid)
            {
                ViewBag.Order = order;
                insurance.OrderId = id;
                return View(insurance);
            }
            return Run(id, () => order.AddInsurance(insurance), "Страховий пакет додано.");
        }

        // POST: Orders/PayInvoice/5 — «Оплатити інвойс»
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowRoles(UserRole.Client)]
        public ActionResult PayInvoice(int id)
        {
            var order = FindAccessibleOrder(id);
            if (order == null) return OrderNotFound();
            return Run(id, () =>
            {
                if (order.Status != OrderStatus.Invoiced || order.Invoice == null)
                    throw new InvalidOperationException("Інвойс ще не виставлено або вже оплачено.");
                if (!CurrentClient.PayInvoice(order.Invoice))
                    throw new InvalidOperationException("Інвойс уже оплачено.");
                order.UpdateStatus(OrderStatus.PaymentPending);
            }, "Інвойс оплачено. Менеджер підтвердить надходження оплати.");
        }

        // GET: Orders/TrackDelivery/5 — «Відстежити доставку» + «Переглянути митний статус»
        [HttpGet]
        public ActionResult TrackDelivery(int id)
        {
            var order = FindAccessibleOrder(id);
            if (order == null) return OrderNotFound();
            if (order.Delivery == null) return BackToOrder(id, "Доставку ще не організовано.");

            ViewBag.TrackingInfo = CurrentRole == UserRole.Client
                ? CurrentClient.TrackDelivery(order.Delivery.TrackingNumber)
                : null;
            return View(order);
        }

        // POST: Orders/ConfirmReceipt/5 — «Підтвердити отримання авто»
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowRoles(UserRole.Client)]
        public ActionResult ConfirmReceipt(int id)
        {
            var order = FindAccessibleOrder(id);
            if (order == null) return OrderNotFound();
            return Run(id, () => CurrentClient.ConfirmReceipt(order), "Отримання авто підтверджено. Заявку завершено.");
        }

        // POST: Orders/LeaveReview/5 — «Залишити відгук» (extend «Підтвердити отримання»)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowRoles(UserRole.Client)]
        public ActionResult LeaveReview(int id, string review)
        {
            var order = FindAccessibleOrder(id);
            if (order == null) return OrderNotFound();
            return Run(id, () =>
            {
                if (!order.IsConfirmedByClient)
                    throw new InvalidOperationException("Відгук можна залишити після підтвердження отримання.");
                if (string.IsNullOrWhiteSpace(review))
                    throw new InvalidOperationException("Відгук порожній.");
                order.Review = review.Trim();
            }, "Дякуємо за відгук!");
        }

        // ---------- Менеджер ----------

        // POST: Orders/ProcessOrder/5 — «Обробити заявку клієнта» (takeOrderInWork)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowRoles(UserRole.Manager)]
        public ActionResult ProcessOrder(int id)
        {
            var order = FindAccessibleOrder(id);
            if (order == null) return OrderNotFound();
            return Run(id, () => CurrentManager.TakeOrderInWork(order), "Заявку взято в роботу.");
        }

        // POST: Orders/RequestDocuments/5 — «Запитати додаткові документи»
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowRoles(UserRole.Manager)]
        public ActionResult RequestDocuments(int id)
        {
            var order = FindAccessibleOrder(id);
            if (order == null) return OrderNotFound();
            return Run(id, () =>
            {
                var manager = CurrentManager;
                manager.RequestDocuments(order);
                manager.NotifyClient(order.Client, "За заявкою №" + order.OrderId + " потрібні додаткові документи.");
            }, "Клієнту надіслано запит на документи.");
        }

        // GET: Orders/PlaceBid/5 — «Взяти участь в аукціоні»
        [HttpGet]
        [AllowRoles(UserRole.Manager)]
        public ActionResult PlaceBid(int id)
        {
            var order = FindAccessibleOrder(id);
            if (order == null) return OrderNotFound();
            if (order.Status != OrderStatus.InProgress || order.Car == null)
                return BackToOrder(id, "Ставку можна зробити лише для заявки в роботі з вибраним авто.");
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowRoles(UserRole.Manager)]
        public ActionResult PlaceBid(int id, double? bidAmount)
        {
            var order = FindAccessibleOrder(id);
            if (order == null) return OrderNotFound();
            if (order.Status != OrderStatus.InProgress || order.Car == null)
                return BackToOrder(id, "Ставку можна зробити лише для заявки в роботі з вибраним авто.");

            var manager = CurrentManager;

            // «Бюджет дозволяє?» — ні: сповістити клієнта про неуспішну ставку
            if (order.Car.AuctionPrice > order.MaxBudget)
            {
                order.UpdateStatus(OrderStatus.BidLost);
                manager.NotifyClient(order.Client, string.Format(
                    "Заявка №{0}: бюджет ${1:N0} менший за ціну авто ${2:N0}. Оберіть інше авто або змініть бюджет.",
                    order.OrderId, order.MaxBudget, order.Car.AuctionPrice));
                db.SaveChanges();
                return BackToOrder(id, "Бюджет клієнта не дозволяє брати участь. Клієнта сповіщено.");
            }

            if (bidAmount == null || bidAmount <= 0)
                ModelState.AddModelError("bidAmount", "Вкажіть суму ставки.");
            else if (bidAmount > order.MaxBudget)
                ModelState.AddModelError("bidAmount", "Ставка не може перевищувати бюджет клієнта.");
            if (!ModelState.IsValid) return View(order);

            try
            {
                bool won = manager.PlaceBid(order.Car, bidAmount.Value);
                if (won)
                {
                    order.FinalPrice = bidAmount.Value;
                    order.UpdateStatus(OrderStatus.BidWon);
                }
                else
                {
                    order.UpdateStatus(OrderStatus.BidLost);
                    manager.NotifyClient(order.Client, "Заявка №" + order.OrderId +
                        ": ставку програно, авто продано іншому. Оберіть інше авто або скасуйте заявку.");
                }
                db.SaveChanges();
                return BackToOrder(id, won ? "Ставку виграно! Авто зарезервовано." : "Ставку програно. Клієнта сповіщено.");
            }
            catch (InvalidOperationException ex)
            {
                return BackToOrder(id, ex.Message);
            }
        }

        // POST: Orders/CreateInvoice/5 — «Ініціювати формування інвойсу». Сума = виграна ставка.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowRoles(UserRole.Manager)]
        public ActionResult CreateInvoice(int id)
        {
            var order = FindAccessibleOrder(id);
            if (order == null) return OrderNotFound();
            return Run(id, () =>
            {
                if (order.Invoice != null) throw new InvalidOperationException("Інвойс уже сформовано.");
                var manager = CurrentManager;
                db.Invoices.Add(manager.GenerateInvoice(order));
                manager.NotifyClient(order.Client, string.Format("Заявка №{0}: виставлено інвойс на ${1:N0}.",
                    order.OrderId, order.FinalPrice));
            }, "Інвойс сформовано й надіслано клієнту.");
        }

        // POST: Orders/ConfirmPayment/5 — «Зафіксувати оплату»
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowRoles(UserRole.Manager)]
        public ActionResult ConfirmPayment(int id)
        {
            var order = FindAccessibleOrder(id);
            if (order == null) return OrderNotFound();
            return Run(id, () =>
            {
                if (order.Status != OrderStatus.PaymentPending || order.Invoice == null)
                    throw new InvalidOperationException("Немає оплати, що очікує підтвердження.");
                CurrentManager.ConfirmPayment(order.Invoice);
            }, "Оплату зафіксовано. Заявка передана логісту.");
        }

        // POST: Orders/RejectPayment/5 — «Оплата надійшла? — Ні»: клієнт має оплатити ще раз
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowRoles(UserRole.Manager)]
        public ActionResult RejectPayment(int id)
        {
            var order = FindAccessibleOrder(id);
            if (order == null) return OrderNotFound();
            return Run(id, () =>
            {
                if (order.Status != OrderStatus.PaymentPending || order.Invoice == null)
                    throw new InvalidOperationException("Немає оплати, що очікує підтвердження.");
                order.Invoice.IsPaid = false;
                order.UpdateStatus(OrderStatus.Invoiced);
                CurrentManager.NotifyClient(order.Client, "Заявка №" + order.OrderId + ": оплата не надійшла, повторіть оплату інвойсу.");
            }, "Оплату не підтверджено, клієнта сповіщено.");
        }

        // GET: Orders/Delete/5 — менеджер видаляє лише нові, скасовані або програні заявки
        [HttpGet]
        [AllowRoles(UserRole.Manager)]
        public ActionResult Delete(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            var order = FindAccessibleOrder(id.Value);
            if (order == null) return OrderNotFound();
            ViewBag.CanDelete = CanDelete(order);
            return View(order);
        }

        private static bool CanDelete(Order order)
        {
            return order.Status == OrderStatus.New || order.Status == OrderStatus.Cancelled ||
                   order.Status == OrderStatus.BidLost;
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [AllowRoles(UserRole.Manager)]
        public ActionResult DeleteConfirmed(int id)
        {
            var order = FindAccessibleOrder(id);
            if (order == null) return OrderNotFound();
            if (!CanDelete(order)) return BackToOrder(id, "Видалити можна лише нову, скасовану або програну заявку.");

            db.Insurances.RemoveRange(order.Insurances.ToList());
            db.Invoices.RemoveRange(order.Invoices.ToList());
            if (order.Delivery != null) db.Deliveries.Remove(order.Delivery);
            db.Orders.Remove(order);
            db.SaveChanges();
            TempData["Message"] = "Заявку №" + id + " видалено.";
            return RedirectToAction("Index");
        }

        // ---------- Логіст ----------

        // POST: Orders/CreateDelivery/5 — «Призначити перевізника» (лише для оплаченої заявки)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowRoles(UserRole.Logistician)]
        public ActionResult CreateDelivery(int id)
        {
            var order = FindAccessibleOrder(id);
            if (order == null) return OrderNotFound();
            return Run(id, () =>
            {
                if (order.Status != OrderStatus.Paid)
                    throw new InvalidOperationException("Перевізника можна призначити лише для оплаченої заявки.");
                if (order.Delivery != null)
                    throw new InvalidOperationException("Перевізника вже призначено.");
                var delivery = new Delivery { OrderId = order.OrderId, Order = order };
                CurrentLogistician.AssignCarrier(delivery);
                db.Deliveries.Add(delivery);
            }, "Перевізника призначено.");
        }

        private ActionResult WithDelivery(int id, Func<Order, ActionResult> body)
        {
            var order = FindAccessibleOrder(id);
            if (order == null) return OrderNotFound();
            if (order.Delivery == null) return BackToOrder(id, "Спочатку призначте перевізника.");
            return body(order);
        }

        // POST: Orders/GenerateMandate/5 — «Згенерувати доручення»
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowRoles(UserRole.Logistician)]
        public ActionResult GenerateMandate(int id)
        {
            return WithDelivery(id, order =>
            {
                CurrentLogistician.GenerateMandate(order.Delivery);
                db.SaveChanges();
                return RedirectToAction("Mandate", new { id = id });
            });
        }

        // GET: Orders/Mandate/5 — сторінка доручення для друку
        [HttpGet]
        public ActionResult Mandate(int id)
        {
            return WithDelivery(id, order =>
            {
                if (order.Delivery.MandateGeneratedAt == null)
                    return BackToOrder(id, "Доручення ще не сформовано.");
                return View(order);
            });
        }

        // GET/POST: Orders/AddCargoInsurance/5 — «Замовити додаткове страхування вантажу»
        [HttpGet]
        [AllowRoles(UserRole.Logistician)]
        public ActionResult AddCargoInsurance(int id)
        {
            return WithDelivery(id, order => View(order));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowRoles(UserRole.Logistician)]
        public ActionResult AddCargoInsurance(int id, double? cost)
        {
            return WithDelivery(id, order =>
            {
                if (cost == null || cost <= 0)
                {
                    ModelState.AddModelError("cost", "Вкажіть вартість страхування більшу за 0.");
                    return View(order);
                }
                order.Delivery.HasCargoInsurance = true;
                order.Delivery.CargoInsuranceCost = cost.Value;
                db.SaveChanges();
                return BackToOrder(id, "Страхування вантажу оформлено.");
            });
        }

        // GET/POST: Orders/AddPhotoReport/5 — «Додати фотозвіт стану авто»
        [HttpGet]
        [AllowRoles(UserRole.Logistician)]
        public ActionResult AddPhotoReport(int id)
        {
            return WithDelivery(id, order => View(order));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowRoles(UserRole.Logistician)]
        public ActionResult AddPhotoReport(int id, string note)
        {
            return WithDelivery(id, order =>
            {
                if (string.IsNullOrWhiteSpace(note))
                {
                    ModelState.AddModelError("note", "Опишіть стан авто.");
                    return View(order);
                }
                order.Delivery.PhotoReportNote = note.Trim();
                db.SaveChanges();
                return BackToOrder(id, "Фотозвіт додано.");
            });
        }

        // POST: Orders/ClearCustoms/5 — «Оформити митні документи»: авто «В дорозі»
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowRoles(UserRole.Logistician)]
        public ActionResult ClearCustoms(int id)
        {
            return WithDelivery(id, order =>
                Run(id, () => CurrentLogistician.CreateCustomsDeclaration(order.Delivery),
                    "Митні документи оформлено, авто в дорозі."));
        }

        // POST: Orders/UpdateDeliveryStatus/5 — «Оновити статус доставки»
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowRoles(UserRole.Logistician)]
        public ActionResult UpdateDeliveryStatus(int id, string location, bool arrived = false)
        {
            return WithDelivery(id, order => Run(id, () =>
            {
                if (order.Status != OrderStatus.Shipping)
                    throw new InvalidOperationException("Статус можна оновлювати лише під час перевезення.");
                var logistician = CurrentLogistician;
                order.Delivery.UpdateLocation(location);
                logistician.UpdateDeliveryStatus(order.Delivery, arrived ? "Готово до видачі" : "В дорозі");
                if (arrived && order.Car != null)
                {
                    order.Car.Status = CarStatus.ReadyForPickup;
                    order.Client.Notification = string.Format("{0:dd.MM.yyyy HH:mm} — Заявка №{1}: авто прибуло й готове до видачі.",
                        DateTime.Now, order.OrderId);
                }
            }, arrived ? "Авто готове до видачі. Клієнт має підтвердити отримання." : "Статус доставки оновлено."));
        }
    }
}
