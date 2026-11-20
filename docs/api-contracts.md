# Таблиця контрактів

Контракти між шарами системи ASTCUSA (ASP.NET MVC 5): дії контролерів, ролі, що мають до них доступ, та спільні переліки (enum). Складено за кодом у `CSS/Controllers` і `CSS/Models/Enums.cs`.

Навіщо цей документ: ризик R-11 у [Risk Register](confluence/parts/08-risk-register.pdf) і [post-mortem](confluence/parts/12-post-mortem-v1.0-mvp.pdf) описують збій через зміну переліку без спільного контракту. **Зміна enum або сигнатури дії є зміною контракту й потребує перевірки всіх споживачів** (контролери, Razor-вигляди, звіти, сідер `ImportInitializer.cs`).

Загальні правила:
- Вхід вимагає глобальний фільтр `[Authorize]`; відкриті дії позначені `[AllowAnonymous]`.
- Ролі перевіряє атрибут `[AllowRoles(...)]`.
- Усі дії, що змінюють дані, мають тільки `[HttpPost]` і `[ValidateAntiForgeryToken]`.
- `Car` і `Order` мають токен конкурентності `RowVersion`: правка застарілого запису відхиляється.

## 1. Переліки

### CarStatus (стани авто)

| Значення | Назва в UI |
|---|---|
| Available | Доступно |
| Auction | Аукціон |
| Reserved | Зарезервовано |
| Paid | Оплачено |
| InTransit | В дорозі |
| ReadyForPickup | Готово до видачі |
| Delivered | Доставлено |
| SoldToOther | Продано іншому |

### OrderStatus (стани заявки)

Нові значення додають лише в кінець, щоб не змінювати числові коди вже збережених заявок.

| Код | Значення | Назва в UI |
|---|---|---|
| 0 | New | Нова |
| 1 | DocumentsRequested | Запитано документи |
| 2 | InProgress | В роботі |
| 3 | BidWon | Ставку виграно |
| 4 | BidLost | Ставку програно |
| 5 | Invoiced | Виставлено інвойс |
| 6 | PaymentPending | Оплата очікує підтвердження |
| 7 | Paid | Оплачено |
| 8 | Shipping | Перевезення |
| 9 | Completed | Завершено |
| 10 | Cancelled | Скасовано |
| 11 | DocumentsProvided | Документи надано |

### BidResult (результат ставки)

| Значення | Назва в UI |
|---|---|
| Pending | Очікує результату |
| Outbid | Перебито |
| Won | Виграно |
| Lost | Програно |

### UserRole

| Значення | Назва в UI |
|---|---|
| None | Гість |
| Client | Клієнт |
| Manager | Менеджер |
| Logistician | Логіст |

## 2. Дії по замовленнях (`OrdersController`)

| Дія | Метод | Роль | Вхідні дані |
|---|---|---|---|
| Index | GET | усі авторизовані | `status?` |
| Auctions | GET | Клієнт, Менеджер | `result?` |
| Details | GET | за доступом до заявки | `id` |
| Create | GET, POST | Клієнт | `MaxBudget`, `CarId` |
| Edit | GET, POST | Клієнт | `OrderId`, `MaxBudget`, `CarId`, `RowVersion` |
| Cancel | POST | Клієнт | `id` |
| ProvideDocuments | POST | Клієнт | `id`, `files[]` |
| AddInsurance, EditInsurance, DeleteInsurance | GET, POST | Клієнт | `id`, `Type`, `Price` |
| PayInvoice | POST | Клієнт | `id` |
| TrackDelivery | GET | Клієнт | `id` |
| ConfirmReceipt | POST | Клієнт | `id` |
| LeaveReview | POST | Клієнт | `id`, `review` |
| ProcessOrder | POST | Менеджер | `id` |
| RequestDocuments | POST | Менеджер | `id`, `note` |
| PlaceBid, EditBid | GET, POST | Менеджер | `id`, `auction`, `lotNumber`, `lotUrl`, `amount` |
| SetBidResult | POST | Менеджер | `id`, `result`, `competitorAmount?` |
| DeleteBid | POST | Менеджер | `id` |
| CreateInvoice | POST | Менеджер | `id` |
| ConfirmPayment, RejectPayment | POST | Менеджер | `id` |
| Delete | GET, POST | Менеджер | `id` |
| CreateDelivery | POST | Логіст | `id` |
| GenerateMandate | POST | Логіст | `id` |
| AddCargoInsurance | GET, POST | Логіст | `id`, `cost?` |
| AddPhotoReport | GET, POST | Логіст | `id`, `note` |
| ClearCustoms | POST | Логіст | `id` |
| UpdateDeliveryStatus | POST | Логіст | `id`, `location`, `arrived` |

## 3. Дії по авто (`CarsController`)

| Дія | Метод | Роль | Вхідні дані |
|---|---|---|---|
| Index | GET | усі авторизовані (Клієнт бачить лише доступні авто) | `CarSearchFilter` |
| Details | GET | усі авторизовані | `id` |
| Create | GET, POST | Менеджер | `VinCode`, `Make`, `Model`, `Year`, `AuctionPrice` |
| Edit | GET, POST | Менеджер | ті самі поля + `CarId`, `RowVersion` |
| Delete | GET, POST | Менеджер | `id` |
| AddToFavorites, RemoveFromFavorites | POST | усі авторизовані | `id`, `returnUrl` |
| Favorites | GET | усі авторизовані | |

VIN унікальний (міграція `AddConcurrencyTokensAndUniqueVin`).

## 4. Звіти й аналітика

| Контролер | Роль | Призначення |
|---|---|---|
| `ReportsController` | Менеджер | Звіти по угодах, експорт |
| `AnalyticsController` (частини Cars, Invoices, Orders, Users, Deliveries, Tables, Parallel) | Менеджер | Аналітична БД, таблиці, CRUD для довідників |
| `ProfileController` | Клієнт | Профіль користувача |
| `AccountController` | гість (`[AllowAnonymous]`) | Вхід, реєстрація, Forms Authentication |

## 5. Правила зміни контракту

1. Перед зміною enum або сигнатури дії знайти всі використання (контролери, вигляди, звіти, сідер).
2. Нові значення enum додавати лише в кінець.
3. Змінити цю таблицю в тому самому коміті, що й код.
4. Позначити зміну в [Change Log](confluence/parts/11-change-log.pdf) і перевірити пов'язані пункти [UAT-чекліста](confluence/parts/24-uat-checklist.pdf).
