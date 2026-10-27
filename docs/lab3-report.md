# Звіт: лабораторна робота №3 (локальна база даних, основи LINQ)

Проєкт: ASTCUSA — «Система перевезення авто зі США» (ASP.NET MVC 5, .NET Framework 4.7.2, EF 6).
Лабораторні №1 і №2 не змінювалися: `ImportContext`, база `astcusa`, авторизація й логіка заявок залишилися як були.

Коротко:
- окрема база `astcusa_lab3` на `(localdb)\MSSQLLocalDB`;
- 6 таблиць, 7 зовнішніх ключів, від 12 до 26 рядків у кожній таблиці;
- сторінка «БД і LINQ» (лише для менеджера) з 6 запитами;
- біля кожного запиту показано LINQ-вираз, згенерований SQL і результат у таблиці.

---

## 1. Чек-лист вимог (з `docs/lab-requirements/ЛР_3_БД_LINQ.pdf`)

| № | Вимога | Стан | Де |
|---|---|---|---|
| 1 | Окрема локальна база за темою проєкту | ✅ | `astcusa_lab3` на LocalDB: `CSS/Web.config:13`, `docs/lab3/create_and_seed.sql:21` |
| 2 | Підключення до бази, рядок підключення в конфігурації | ✅ | `CSS/Web.config:13` (`Lab3Connection`), `CSS/Models/Lab3/Lab3Context.cs:12` |
| 3 | Таблиці й первинні ключі | ✅ | 6 таблиць: `create_and_seed.sql:39, 52, 71, 93, 105, 121` |
| 4 | Зовнішні ключі (зв'язки між таблицями) | ✅ | 7 FK: `create_and_seed.sql:82-84, 100, 114, 131-132` |
| 5 | Структура відповідає UML-діаграмі класів, 5–6 таблиць | ✅ | 6 таблиць ↔ класи User (+Client/Manager/Logistician), Car, Order, Insurance, Invoice, Delivery (розділ 3); відхилення описано в розділі 7 |
| 6 | Заповнення даними, від 10 рядків | ✅ | `create_and_seed.sql:138-267`: 16 / 26 / 25 / 14 / 16 / 12 рядків |
| 7 | Не менше 5 різних запитів, результат на екрані | ✅ | 6 запитів (+ підрахунок рядків): `CSS/Models/Lab3/Lab3Queries.cs`, сторінки `CSS/Views/Lab3/*` |
| 8 | Відбір даних за умовою (фільтрація) | ✅ | запити 1, 2, 4: `Lab3Queries.cs:54, 91, 196` |
| 9 | Узагальнення: сума за вказаний користувачем період (день, місяць або півріччя) | ✅ | запит 3: `Lab3Queries.cs:133`, межі періоду — `Lab3ViewModels.cs:120`; також запит 5 (Count/Average/Min/Max): `Lab3Queries.cs:255, 272` |
| 10 | Додавання, зміна й видалення записів хоча б в одній таблиці | ✅ | Insurances: `CSS/Controllers/Lab3Controller.cs:245, 286, 313` |
| 11 | Використання LINQ | ✅ | усі запити — LINQ to Entities через `Lab3Context`; текст запиту показується на сторінці |
| 12 | Створення таблиць через конструктор таблиць VS (план ЛР, п. 3–5) | ⚠️ | База створюється T-SQL-скриптом. PDF прямо дозволяє цей спосіб («виконавши інструкції Transact-SQL»). Результат можна відкрити в конструкторі (розділ 5) |
| 13 | Файл бази `.mdf` у проєкті («база, заснована на службах») | ⚠️ | Використано базу на сервері LocalDB, без `.mdf` у папці проєкту, як і в лабі №2. Перегляд — через SQL Server Object Explorer |

Додаткові вимоги завдання:

| Вимога | Стан | Де |
|---|---|---|
| Другий контекст `Lab3Context`, `SetInitializer<Lab3Context>(null)` | ✅ | `Lab3Context.cs:9`, `CSS/Global.asax.cs:19` |
| Ідемпотентний скрипт (повторний запуск не ламає базу) | ✅ | `IF DB_ID(...) IS NULL CREATE DATABASE`, таблиці видаляються й створюються заново. Перевірено двома прогонами поспіль |
| База створюється під час першого запуску, якщо її немає | ✅ | `CSS/Models/Lab3/Lab3Database.cs:44` (`EnsureCreated`), `:83` (`RunScript`); скрипт вбудовано в збірку: `CSS/ASTCUSA.csproj:247` |
| Сторінка лише для менеджера | ✅ | `[AllowRoles(UserRole.Manager)]` — `Lab3Controller.cs:13`; пункт меню — `CSS/Views/Shared/_Layout.cshtml:40` |
| Зміни даних лише через POST + антифорджері-токен | ✅ | `Lab3Controller.cs:244, 285, 312`; GET-запити тільки читають |
| Українські повідомлення валідації | ✅ | атрибути в `CSS/Models/Lab3/Lab3ViewModels.cs`, перевірки в контролері; «abc» у числах — через `ValidationMessages.resx` з лаби №2 |
| Назва, опис, LINQ-вираз, SQL (`ToString()`) і таблиця результату біля кожного запиту | ✅ | `CSS/Views/Lab3/_QueryInfo.cshtml`; SQL — `Lab3Controller.cs:77, 105, 142-143, 171, 195, 218` |

## 2. Нові та змінені файли

**Нові:**

| Файл | Що в ньому |
|---|---|
| `docs/lab3/create_and_seed.sql` | Ідемпотентний T-SQL-скрипт: створює базу, 6 таблиць з ключами й обмеженнями і заповнює їх даними. |
| `CSS/Models/Lab3/Lab3Entities.cs` | Шість простих класів-відображень таблиць (`Lab3User`, `Lab3Car`, `Lab3Order`, `Lab3Insurance`, `Lab3Invoice`, `Lab3Delivery`). |
| `CSS/Models/Lab3/Lab3Context.cs` | Другий `DbContext` для бази `astcusa_lab3` (рядок `Lab3Connection`), окремий від `ImportContext`. |
| `CSS/Models/Lab3/Lab3Database.cs` | Під час першого звернення перевіряє, чи є база, і за потреби виконує вбудований скрипт пакетами по `GO`. |
| `CSS/Models/Lab3/Lab3Queries.cs` | Усі LINQ-запити лаби й їхній текст (константи `*Linq`), який показується на сторінці. |
| `CSS/Models/Lab3/Lab3ViewModels.cs` | Моделі форм і рядків результатів з українськими повідомленнями валідації. Тут також обчислюються межі періоду (день, місяць, півріччя). |
| `CSS/Controllers/Lab3Controller.cs` | Контролер сторінки «БД і LINQ»: 6 запитів і додавання, зміна, видалення страховок. Доступний лише менеджеру. |
| `CSS/Views/Lab3/_Lab3Nav.cshtml` | Заголовок і вкладки між запитами. |
| `CSS/Views/Lab3/_QueryInfo.cshtml` | Картка запиту: назва, що демонструє, опис, LINQ-вираз і згорнутий згенерований SQL. |
| `CSS/Views/Lab3/_InsuranceFields.cshtml` | Спільні поля форми страховки для додавання й зміни. |
| `CSS/Views/Lab3/Index.cshtml` | Відомості про базу і кількість рядків у кожній таблиці. |
| `CSS/Views/Lab3/Cars.cshtml` | Запит 1: фільтр авто. |
| `CSS/Views/Lab3/ClientOrders.cshtml` | Запит 2: заявки клієнта за e-mail і статусом. |
| `CSS/Views/Lab3/PaidInvoices.cshtml` | Запит 3: сума й кількість оплат за день, місяць або півріччя. |
| `CSS/Views/Lab3/Deliveries.cshtml` | Запит 4: доставки зі з'єднанням п'яти таблиць. |
| `CSS/Views/Lab3/Stats.cshtml` | Запит 5: групування за статусом або маркою. |
| `CSS/Views/Lab3/Insurances.cshtml` | Запит 6: перелік страховок, форма додавання, кнопки зміни й видалення. |
| `CSS/Views/Lab3/EditInsurance.cshtml` | Запит 6: форма зміни страховки. |
| `docs/lab3-report.md` | Цей звіт. |

**Змінені (лише додано рядки, нічого не видалено):**

| Файл | Зміна |
|---|---|
| `CSS/Global.asax.cs` | `using ASTCUSA.Models.Lab3;` і `Database.SetInitializer<Lab3Context>(null);` з коментарем. |
| `CSS/Web.config` | Рядок підключення `Lab3Connection` до `astcusa_lab3`. |
| `CSS/Views/Shared/_Layout.cshtml` | Пункт меню «БД і LINQ» для менеджера. |
| `CSS/ASTCUSA.csproj` | Нові `.cs`, `.cshtml` і вбудований ресурс `ASTCUSA.Lab3.create_and_seed.sql` (посилання на `docs/lab3/create_and_seed.sql`). |

## 3. Схема бази `astcusa_lab3`

| Таблиця | Стовпці | Ключі та обмеження | Рядків |
|---|---|---|---|
| **Users** | Id, Name, Email, Role, PhoneNumber, Address | PK Id (IDENTITY); UNIQUE Email; CHECK Role ∈ {Client, Manager, Logistician} | 16 (12 клієнтів, 2 менеджери, 2 логісти) |
| **Cars** | CarId, VinCode, Make, Model, Year, AuctionPrice DECIMAL(12,2), Status | PK CarId; UNIQUE VinCode; CHECK довжина VIN 11–17, Year 1950–2100, ціна > 0, Status ∈ CarStatus | 26 |
| **Orders** | OrderId, ClientId, ManagerId, CarId, MaxBudget, FinalPrice, Status, CreatedAt | PK OrderId; FK ClientId → Users, ManagerId → Users (NULL), CarId → Cars (NULL); CHECK суми > 0, Status ∈ OrderStatus | 25 |
| **Insurances** | InsuranceId, OrderId, Type, Price | PK InsuranceId; FK OrderId → Orders; CHECK Price > 0 | 14 |
| **Invoices** | InvoiceId, OrderId, TotalAmount, IsPaid, PaidAt | PK InvoiceId; FK OrderId → Orders; UNIQUE OrderId; CHECK «оплачено ⇔ є дата оплати» | 16 (14 оплачених, лютий–жовтень 2026) |
| **Deliveries** | OrderId, LogisticianId, TrackingNumber, CurrentLocation, IsCustomsCleared, Status | PK OrderId (= ключ заявки); FK OrderId → Orders, LogisticianId → Users; UNIQUE TrackingNumber | 12 |

Разом: 6 PK, 7 FK, 4 UNIQUE, 11 CHECK, 2 DEFAULT.

**Відповідність діаграмі класів (`docs/diagrams/OOP3.webp`):**

| Зв'язок на діаграмі | У базі |
|---|---|
| User ← Client / Manager / Logistician (наслідування) | одна таблиця `Users` зі стовпцем `Role` (як TPH у лабі №2) |
| Client 1 — 0..* Order («створює») | `Orders.ClientId` NOT NULL → `Users` |
| Manager 0..1 — 0..* Order («обробляє») | `Orders.ManagerId` NULL → `Users` |
| Order 0..* — 0..1 Car («стосується») | `Orders.CarId` NULL → `Cars` |
| Order 1 — 0..* Insurance | `Insurances.OrderId` → `Orders` |
| Order 1 ◆— 0..1 Invoice | `Invoices.OrderId` → `Orders` + UNIQUE |
| Order 1 ◆— 0..1 Delivery | `Deliveries.OrderId` = PK і FK → `Orders` |
| Logistician 0..1 — 0..* Delivery | `Deliveries.LogisticianId` NULL → `Users` |

## 4. Запити

На сторінці кожен запит має назву, позначки «що демонструє», опис, текст LINQ, згорнутий згенерований SQL (`query.ToString()`) і таблицю результату. Нижче наведено приклади результатів на початкових даних.

### 0. Кількість рядків у таблицях (вкладка «База даних»)
Демонструє: узагальнення `Count()`.
```csharp
db.Users.Count();  db.Cars.Count();  db.Orders.Count();
db.Insurances.Count();  db.Invoices.Count();  db.Deliveries.Count();
```
Результат: Users 16, Cars 26, Orders 25, Insurances 14, Invoices 16, Deliveries 12.

### 1. Фільтр авто за маркою, роком і ціною
Демонструє: фільтрацію за кількома умовами, які вводить користувач; сортування. Порожні поля не обмежують результат.
```csharp
var query = from c in db.Cars
            where (make == "" || c.Make.Contains(make))
               && (yearFrom == null || c.Year >= yearFrom)
               && (yearTo == null || c.Year <= yearTo)
               && (priceFrom == null || c.AuctionPrice >= priceFrom)
               && (priceTo == null || c.AuctionPrice <= priceTo)
            orderby c.Make, c.Model, c.Year
            select c;
```
Приклад: Toyota, 2018–2024, $5 000–30 000 → 4 авто (Camry 2019 — $12 000, Camry 2020 — $15 800, Prius 2019 — $11 200, RAV4 2018 — $15 400).
Перевірки: «Рік до» < «Рік від», «Ціна до» < «Ціна від», рік поза 1950–2100, «abc» у числі.

### 2. Заявки клієнта за e-mail і статусом
Демонструє: фільтрацію; з'єднання Users–Orders–Cars і ліве з'єднання з Users (менеджер).
```csharp
var query = from u in db.Users
            join o in db.Orders on u.Id equals o.ClientId
            join c in db.Cars on o.CarId equals (int?)c.CarId
            join m in db.Users on o.ManagerId equals (int?)m.Id into managers
            from m in managers.DefaultIfEmpty()          // LEFT JOIN
            where u.Email == email && (status == "" || o.Status == status)
            orderby o.CreatedAt descending
            select new ClientOrderRow { ... };
```
Приклад: `ivan@mail.com`, усі статуси → 3 заявки:
- №13 Toyota Camry 2020 — оплачено;
- №25 Honda Civic — скасовано;
- №1 Toyota Camry 2019 — завершено.

Зі статусом «Завершено» → 1 заявка.
Перевірки: порожній e-mail, некоректний e-mail, невідомий статус.

### 3. Сума оплачених інвойсів за період
Демонструє: узагальнення (`group`, `Sum`, `Count`) за періодом, який задає користувач: тип (день, місяць або півріччя) і дата.
```csharp
PaidInvoicesInput.GetRange(period, date, out start, out end);   // [start; end)
var query = from i in db.Invoices
            where i.IsPaid && i.PaidAt >= start && i.PaidAt < end
            group i by i.IsPaid into g
            select new PaidInvoiceSummary { Total = g.Sum(x => x.TotalAmount), Count = g.Count() };
```
Під сумою — перелік цих оплат (з'єднання Invoices–Orders–Users).

Приклади:

| Період | Сума | Кількість оплат |
|---|---|---|
| Півріччя, дата 15.03.2026 (01.01–30.06.2026) | $75 700.00 | 5 |
| Місяць 10.2026 | $74 000.00 | 5 |
| День 12.08.2026 | $20 100.00 | 2 |
| День 01.01.2025 | 0 | 0, «Оплат за цей період немає» |

Згенерований SQL (півріччя):
```sql
SELECT 1 AS [C1], [GroupBy1].[A1] AS [C2], [GroupBy1].[A2] AS [C3]
FROM ( SELECT [Extent1].[IsPaid] AS [K1], SUM([Extent1].[TotalAmount]) AS [A1], COUNT(1) AS [A2]
       FROM [dbo].[Invoices] AS [Extent1]
       WHERE ([Extent1].[IsPaid] = 1) AND ([Extent1].[PaidAt] >= @p__linq__0) AND ([Extent1].[PaidAt] < @p__linq__1)
       GROUP BY [Extent1].[IsPaid] ) AS [GroupBy1]
```
Перевірки: порожня дата, «abc» у даті, не вибрано тип періоду, невідомий тип періоду.

### 4. Доставки за статусом
Демонструє: фільтрацію за статусом доставки й митницею; з'єднання п'яти таблиць: Deliveries, Orders, Cars, Users (клієнт), а також Users (логіст) через LEFT JOIN.
```csharp
var query = from d in db.Deliveries
            join o in db.Orders on d.OrderId equals o.OrderId
            join c in db.Cars on o.CarId equals (int?)c.CarId
            join cl in db.Users on o.ClientId equals cl.Id
            join l in db.Users on d.LogisticianId equals (int?)l.Id into logisticians
            from l in logisticians.DefaultIfEmpty()
            where (status == "" || d.Status == status)
               && (customs == null || d.IsCustomsCleared == customs)
            orderby d.OrderId
            select new DeliveryRow { ... };
```
Приклад: статус «В дорозі» → 2 доставки:
- №4 Tesla Model 3, порт Одеса, логіст Андрій Мельник;
- №5 BMW 328i, порт Гданськ, логіст Ірина Захарченко.

Фільтр «Розмитнено» → 8 доставок, без фільтрів — 12.

### 5. Групування заявок за статусом або маркою
Демонструє: групування (`group by`), узагальнення (`Count`, `Average`, `Min`, `Max`) і з'єднання Orders–Cars.
```csharp
var query = from o in db.Orders
            join c in db.Cars on o.CarId equals (int?)c.CarId
            group c by o.Status into g          // або: group c by c.Make
            orderby g.Count() descending, g.Key
            select new GroupRow { Key = g.Key, OrdersCount = g.Count(),
                AvgCarPrice = g.Average(x => x.AuctionPrice),
                MinCarPrice = g.Min(x => x.AuctionPrice), MaxCarPrice = g.Max(x => x.AuctionPrice) };
```
Приклад за статусом: 12 груп, разом 25 заявок.

| Статус | Заявок | Середня ціна авто |
|---|---|---|
| Оплачено | 5 | $12 560 |
| Завершено | 4 | $12 800 |
| Перевезення | 4 | $13 625 |
| … | | |

Приклад за маркою: 14 груп.

| Марка | Заявок | Середня ціна авто |
|---|---|---|
| Honda | 5 | $14 980 |
| Ford | 3 | $16 667 |
| Toyota | 3 | $13 000 |
| … | | |

### 6. Страховки: додавання, зміна, видалення
Демонструє: INSERT, UPDATE і DELETE через `Lab3Context`; з'єднання Insurances–Orders–Users для переліку.
```csharp
bool orderExists = db.Orders.Any(o => o.OrderId == input.OrderId);
db.Insurances.Add(new Lab3Insurance { OrderId = orderId, Type = type, Price = price }); db.SaveChanges();
var insurance = db.Insurances.Find(id); insurance.Type = type; /* ... */ db.SaveChanges();
db.Insurances.Remove(db.Insurances.Find(id)); db.SaveChanges();
```
Як це працює:
- Заявку вибирають зі списку; тип страховки — текст із підказками; ціна — від 1 до 1 000 000.
- Додавання, зміна й видалення відбуваються лише через POST з `@Html.AntiForgeryToken()`.
- Видалення потребує підтвердження (`confirm`).
- Після дії сторінка повертається до переліку з повідомленням «Страховку №N додано / змінено / видалено».

Перевірки:
- порожні поля дають три повідомлення: «Оберіть заявку», «Вкажіть тип страховки», «Вкажіть ціну»;
- «abc» у ціні;
- ціна 0;
- неіснуюча заявка;
- неіснуюча страховка;
- POST без токена;
- спроба змінити дані через GET.

## 5. Як запустити і де подивитися базу

1. Відкрити `CSS.slnx` у Visual Studio і запустити проєкт (F5).
2. Увійти менеджером: `manager@mail.com` / `Manager#2026`.
3. У меню вибрати «БД і LINQ». Під час першого відкриття `Lab3Database.EnsureCreated()` підключається до `master`. Якщо бази `astcusa_lab3` (або таблиці `Deliveries` у ній) немає, він виконує вбудований скрипт `docs/lab3/create_and_seed.sql`. Це займає 1–5 секунд. Далі база вже є, і дані, змінені на сторінці, зберігаються.
4. Перегляд бази: **View → SQL Server Object Explorer → SQL Server → (localdb)\MSSQLLocalDB → Databases → astcusa_lab3 → Tables**.
   - Правою кнопкою на таблиці: **View Data** (дані) або **View Designer** (стовпці, ключі).
   - Рядок підключення: правою кнопкою на базі → **Properties**.
5. Створити базу вручну без сайту можна так: відкрити `docs/lab3/create_and_seed.sql` у VS (або SSMS), підключитися до `(localdb)\MSSQLLocalDB` і виконати. Скрипт ідемпотентний. Повторний запуск повертає початкові дані: таблиці видаляються й заповнюються заново.

## 6. Що перевірено і як

Перевірку виконано один раз наприкінці, у тимчасовій копії проєкту з окремими тимчасовими базами. Базу `astcusa` не чіпали, `astcusa_lab3` тести не створювали. Усі тимчасові файли й бази після перевірки видалено.

- **Збірка:** MSBuild з `MvcBuildViews=true` дала 0 помилок і 0 попереджень (у тому числі компіляція всіх `.cshtml`).
- **Скрипт окремо:** `sqlcmd` двічі поспіль на тимчасовій базі. Обидва прогони пройшли без помилок, кількість рядків однакова.
  - Перевірено: 6 PK, 7 FK, 4 UNIQUE, 11 CHECK.
  - У даних немає заявок, де ClientId вказує не на клієнта, ManagerId — не на менеджера, а LogisticianId у доставці — не на логіста.
- **Сайт (IIS Express, HTTPS):** автоматизований прохід — 68 перевірок, 0 невдач.
  - Лаба №2 працює: вхід усіма ролями, каталог, заявки, звітність.
  - Доступ:
    - клієнт і логіст на `/Lab3/*` отримують переадресацію з повідомленням «Ця дія доступна лише для ролі: Менеджер»;
    - анонім потрапляє на сторінку входу;
    - у меню клієнта пункту «БД і LINQ» немає.
  - База створилася сайтом під час першого відкриття вкладки «База даних»: у кожній таблиці 12–26 рядків.
  - Усі 6 запитів повертають непорожній результат. LINQ-текст і SQL (`SELECT`, `INNER JOIN`, `LEFT OUTER JOIN`, `GROUP BY`, `SUM`) показуються.
  - Додавання, зміна й видалення страховки відбуваються з повідомленнями, а кількість рядків після видалення повертається до початкової.
  - Негативні випадки: порожні поля, «abc» у числах і даті, порожня дата, невідомі статуси й тип періоду, неіснуючі заявка чи страховка, POST без токена, GET замість POST. Усі дають українські повідомлення або відмову.
  - Клієнтська валідація теж українська: `data-val-number="Поле «Рік від» має бути числом."`.
- **Виправлено під час перевірки:** `EnumDropDownListFor` падав з помилкою 500, якщо в адресі був невідомий період (`?Period=Week`). Список періодів тепер будується вручну, і показується повідомлення валідації.

**Відомі обмеження:**
- POST без антифорджері-токена (підроблений запит) отримує стандартну сторінку помилки ASP.NET, як і всі форми лаби №2. Через звичайні форми сайту так не буває.
- Зовнішні ключі не перевіряють роль: `Orders.ClientId` технічно може вказувати на менеджера. Узгодженість забезпечують дані скрипта й форми.
- Запити 2, 4 і 5 з'єднують заявку з авто через INNER JOIN, тому заявка без авто в них не потрапила б. У початкових даних таких заявок немає.
- Сайт запускає скрипт лише тоді, коли бази або таблиці `Deliveries` немає. Щоб повернути початкові дані після змін на сторінці, треба вручну виконати скрипт (розділ 5, п. 5) або видалити базу.
- Дані бази `astcusa_lab3` не пов'язані з базою лаби №2 `astcusa`: це окремий навчальний набір.
- Оплати в даних — з лютого по жовтень 2026 року. Сторінка запиту 3 за замовчуванням відкриває поточний місяць, тож пізніше він може бути порожнім (дата змінюється у формі).

## 7. Зміни в діаграмі класів

Таблиці відповідають класам діаграми. Додати в Draw.io варто одне поле:
- **Invoice: поля** → додати `-paidAt: dateTime` після `-isPaid`. Дата оплати потрібна для запиту «сума оплат за період»; у лабі №2 її роль виконувала дата створення заявки.

Особливості реалізації, які не потребують змін діаграми, але про них варто знати:
- Наслідування User → Client / Manager / Logistician зберігається в одній таблиці `Users` зі стовпцем `Role`.
- Гроші зберігаються як `DECIMAL(12,2)`, а не `double` з діаграми: так немає похибок округлення в сумах.
- Статуси (`OrderStatus`, `CarStatus`) зберігаються рядками з іменами перелічень, обмеженими через CHECK. Статус доставки — український текст, як у класі Delivery.
- У базі лаби №3 немає стовпців `passwordHash`, `notification`, `review` та інших полів процесу з лаби №2. Ця база для запитів, а не для входу.

Решта змін діаграм — у `docs/diagram-changes.md` (з лаби №2).

## 8. Що треба зробити вручну

1. Відкрити проєкт у Visual Studio, зібрати й запустити. Увійти як `manager@mail.com` / `Manager#2026` і відкрити «БД і LINQ»: база `astcusa_lab3` створиться сама.
2. Зробити скріншоти для звіту:
   - SQL Server Object Explorer з базою `astcusa_lab3` і таблицями;
   - View Designer однієї таблиці з ключами;
   - View Data;
   - кожна вкладка сторінки «БД і LINQ».
3. За бажанням додати в діаграму класів `Invoice.paidAt` (розділ 7).
4. Перевірити зміни (`git status`: 4 змінені файли, нові `CSS/Controllers/Lab3Controller.cs`, `CSS/Models/Lab3/`, `CSS/Views/Lab3/`, `docs/lab3/`, `docs/lab3-report.md`) і зробити коміт.
