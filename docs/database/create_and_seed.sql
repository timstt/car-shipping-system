/*
    Окрема локальна база даних astcusa_analytics
    (Система перевезення авто зі США).

    Скрипт створює базу (якщо її немає), 6 таблиць за діаграмою класів
    з первинними й зовнішніми ключами та обмеженнями і заповнює їх тестовими даними.

    Скрипт ідемпотентний: під час повторного запуску таблиці видаляються,
    створюються заново й знову заповнюються тими самими даними (зміни, зроблені
    на сторінці «Аналітика», при цьому скидаються).

    Запуск:
      • Visual Studio: SQL Server Object Explorer → (localdb)\MSSQLLocalDB → New Query → вставити скрипт → Execute;
      • командний рядок: sqlcmd -S "(localdb)\MSSQLLocalDB" -E -i create_and_seed.sql;
      • застосунок виконує цей самий скрипт автоматично під час першого відкриття сторінки
        «Аналітика», якщо бази ще немає (див. Models/Analytics/AnalyticsDatabase.cs).
*/
SET NOCOUNT ON;

IF DB_ID(N'astcusa_analytics') IS NULL
    CREATE DATABASE [astcusa_analytics];
GO

USE [astcusa_analytics];
GO

-- Видалення таблиць у порядку, зворотному до залежностей (для повторного запуску)
IF OBJECT_ID(N'dbo.Deliveries', N'U') IS NOT NULL DROP TABLE dbo.Deliveries;
IF OBJECT_ID(N'dbo.Invoices', N'U') IS NOT NULL DROP TABLE dbo.Invoices;
IF OBJECT_ID(N'dbo.Insurances', N'U') IS NOT NULL DROP TABLE dbo.Insurances;
IF OBJECT_ID(N'dbo.Orders', N'U') IS NOT NULL DROP TABLE dbo.Orders;
IF OBJECT_ID(N'dbo.Cars', N'U') IS NOT NULL DROP TABLE dbo.Cars;
IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL DROP TABLE dbo.Users;
GO

/* ---------- Структура ---------- */

-- «abstract» User і нащадки Client / Manager / Logistician в одній таблиці; роль — у стовпці Role
CREATE TABLE dbo.Users
(
    Id          INT            IDENTITY(1, 1) NOT NULL,
    Name        NVARCHAR(100)  NOT NULL,
    Email       NVARCHAR(256)  NOT NULL,
    Role        NVARCHAR(20)   NOT NULL,
    PhoneNumber NVARCHAR(30)   NULL,   -- лише для Client
    Address     NVARCHAR(200)  NULL,   -- лише для Client
    CONSTRAINT PK_Users PRIMARY KEY (Id),
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT CK_Users_Role CHECK (Role IN (N'Client', N'Manager', N'Logistician'))
);

CREATE TABLE dbo.Cars
(
    CarId        INT            IDENTITY(1, 1) NOT NULL,
    VinCode      NVARCHAR(17)   NOT NULL,
    Make         NVARCHAR(50)   NOT NULL,
    Model        NVARCHAR(50)   NOT NULL,
    Year         INT            NOT NULL,
    AuctionPrice DECIMAL(12, 2) NOT NULL,
    Status       NVARCHAR(20)   NOT NULL,
    CONSTRAINT PK_Cars PRIMARY KEY (CarId),
    CONSTRAINT UQ_Cars_VinCode UNIQUE (VinCode),
    CONSTRAINT CK_Cars_VinCode CHECK (LEN(VinCode) BETWEEN 11 AND 17),
    CONSTRAINT CK_Cars_Year CHECK (Year BETWEEN 1950 AND 2100),
    CONSTRAINT CK_Cars_AuctionPrice CHECK (AuctionPrice > 0),
    CONSTRAINT CK_Cars_Status CHECK (Status IN (N'Available', N'Auction', N'Reserved', N'Paid',
        N'InTransit', N'ReadyForPickup', N'Delivered', N'SoldToOther'))
);

-- Client 1 — 0..* Order («створює»), Manager 0..1 — 0..* Order («обробляє»), Order — Car («стосується»)
CREATE TABLE dbo.Orders
(
    OrderId    INT            IDENTITY(1, 1) NOT NULL,
    ClientId   INT            NOT NULL,
    ManagerId  INT            NULL,
    CarId      INT            NULL,
    MaxBudget  DECIMAL(12, 2) NOT NULL,
    FinalPrice DECIMAL(12, 2) NULL,
    Status     NVARCHAR(30)   NOT NULL,
    CreatedAt  DATETIME2(0)   NOT NULL,
    CONSTRAINT PK_Orders PRIMARY KEY (OrderId),
    CONSTRAINT FK_Orders_Users_Client FOREIGN KEY (ClientId) REFERENCES dbo.Users (Id),
    CONSTRAINT FK_Orders_Users_Manager FOREIGN KEY (ManagerId) REFERENCES dbo.Users (Id),
    CONSTRAINT FK_Orders_Cars FOREIGN KEY (CarId) REFERENCES dbo.Cars (CarId),
    CONSTRAINT CK_Orders_MaxBudget CHECK (MaxBudget > 0),
    CONSTRAINT CK_Orders_FinalPrice CHECK (FinalPrice IS NULL OR FinalPrice > 0),
    CONSTRAINT CK_Orders_Status CHECK (Status IN (N'New', N'DocumentsRequested', N'InProgress', N'BidWon',
        N'BidLost', N'Invoiced', N'PaymentPending', N'Paid', N'Shipping', N'Completed', N'Cancelled',
        N'DocumentsProvided'))
);

-- Order 1 — 0..* Insurance («має страховку»)
CREATE TABLE dbo.Insurances
(
    InsuranceId INT            IDENTITY(1, 1) NOT NULL,
    OrderId     INT            NOT NULL,
    Type        NVARCHAR(100)  NOT NULL,
    Price       DECIMAL(12, 2) NOT NULL,
    CONSTRAINT PK_Insurances PRIMARY KEY (InsuranceId),
    CONSTRAINT FK_Insurances_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders (OrderId),
    CONSTRAINT CK_Insurances_Price CHECK (Price > 0)
);

-- Order 1 ◆— 0..1 Invoice («містить»): унікальний OrderId
CREATE TABLE dbo.Invoices
(
    InvoiceId   INT            IDENTITY(1, 1) NOT NULL,
    OrderId     INT            NOT NULL,
    TotalAmount DECIMAL(12, 2) NOT NULL,
    IsPaid      BIT            NOT NULL CONSTRAINT DF_Invoices_IsPaid DEFAULT (0),
    PaidAt      DATETIME2(0)   NULL,   -- дата оплати: потрібна для звіту за період
    CONSTRAINT PK_Invoices PRIMARY KEY (InvoiceId),
    CONSTRAINT UQ_Invoices_OrderId UNIQUE (OrderId),
    CONSTRAINT FK_Invoices_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders (OrderId),
    CONSTRAINT CK_Invoices_TotalAmount CHECK (TotalAmount > 0),
    CONSTRAINT CK_Invoices_PaidAt CHECK ((IsPaid = 1 AND PaidAt IS NOT NULL) OR (IsPaid = 0 AND PaidAt IS NULL))
);

-- Order 1 — 0..1 Delivery («включає»): ключ доставки = ключ заявки;
-- Logistician 0..1 — 0..* Delivery («організовує»)
CREATE TABLE dbo.Deliveries
(
    OrderId          INT           NOT NULL,
    LogisticianId    INT           NULL,
    TrackingNumber   NVARCHAR(30)  NOT NULL,
    CurrentLocation  NVARCHAR(100) NULL,
    IsCustomsCleared BIT           NOT NULL CONSTRAINT DF_Deliveries_IsCustomsCleared DEFAULT (0),
    Status           NVARCHAR(50)  NOT NULL,
    CONSTRAINT PK_Deliveries PRIMARY KEY (OrderId),
    CONSTRAINT UQ_Deliveries_TrackingNumber UNIQUE (TrackingNumber),
    CONSTRAINT FK_Deliveries_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders (OrderId),
    CONSTRAINT FK_Deliveries_Users_Logistician FOREIGN KEY (LogisticianId) REFERENCES dbo.Users (Id)
);
GO

/* ---------- Дані ---------- */

SET IDENTITY_INSERT dbo.Users ON;
INSERT INTO dbo.Users (Id, Name, Email, Role, PhoneNumber, Address) VALUES
 (1,  N'Іван Петренко',       N'ivan@mail.com',      N'Client',      N'+380501112233', N'Чернігів, вул. Шевченка, 12'),
 (2,  N'Марія Шевчук',        N'maria@mail.com',     N'Client',      N'+380671234567', N'Київ, вул. Хрещатик, 5'),
 (3,  N'Олег Бондаренко',     N'oleg.b@mail.com',    N'Client',      N'+380632223344', N'Львів, просп. Свободи, 20'),
 (4,  N'Наталія Кравченко',   N'natalia.k@mail.com', N'Client',      N'+380953334455', N'Одеса, вул. Дерибасівська, 3'),
 (5,  N'Андрій Ткаченко',     N'andriy.t@mail.com',  N'Client',      N'+380674445566', N'Харків, вул. Сумська, 41'),
 (6,  N'Світлана Олійник',    N'svitlana@mail.com',  N'Client',      N'+380505556677', N'Дніпро, просп. Яворницького, 18'),
 (7,  N'Дмитро Коваленко',    N'dmytro.k@mail.com',  N'Client',      N'+380936667788', N'Полтава, вул. Соборності, 7'),
 (8,  N'Юлія Мороз',          N'yulia.m@mail.com',   N'Client',      N'+380687778899', N'Вінниця, вул. Пирогова, 9'),
 (9,  N'Сергій Поліщук',      N'serhiy.p@mail.com',  N'Client',      N'+380998889900', N'Житомир, вул. Київська, 15'),
 (10, N'Ольга Савченко',      N'olha.s@mail.com',    N'Client',      N'+380661112244', N'Суми, вул. Соборна, 2'),
 (11, N'Максим Руденко',      N'maksym.r@mail.com',  N'Client',      N'+380731113355', N'Черкаси, бульв. Шевченка, 30'),
 (12, N'Катерина Лисенко',    N'kateryna.l@mail.com',N'Client',      N'+380502224466', N'Ужгород, вул. Корзо, 11'),
 (13, N'Олена Коваль',        N'manager@mail.com',   N'Manager',     NULL, NULL),
 (14, N'Віктор Гончар',       N'viktor.h@mail.com',  N'Manager',     NULL, NULL),
 (15, N'Андрій Мельник',      N'logist@mail.com',    N'Logistician', NULL, NULL),
 (16, N'Ірина Захарченко',    N'iryna.z@mail.com',   N'Logistician', NULL, NULL);
SET IDENTITY_INSERT dbo.Users OFF;

SET IDENTITY_INSERT dbo.Cars ON;
INSERT INTO dbo.Cars (CarId, VinCode, Make, Model, Year, AuctionPrice, Status) VALUES
 (1,  N'1HGCM82633A004352', N'Toyota',        N'Camry',          2019, 12000.00, N'Delivered'),
 (2,  N'2T1BURHE0JC034461', N'Honda',         N'Civic',          2020, 14500.00, N'Delivered'),
 (3,  N'1FA6P8TH5K5123456', N'Ford',          N'Mustang',        2018, 17800.00, N'Delivered'),
 (4,  N'5YJ3E1EA7KF317000', N'Tesla',         N'Model 3',        2021, 24500.00, N'InTransit'),
 (5,  N'WBA8E9C51GK644321', N'BMW',           N'328i',           2016,  9900.00, N'InTransit'),
 (6,  N'JTDKARFU0K3087654', N'Toyota',        N'Prius',          2019, 11200.00, N'ReadyForPickup'),
 (7,  N'1N4AL3AP8JC231456', N'Nissan',        N'Altima',         2018,  8700.00, N'Paid'),
 (8,  N'3VW2B7AJ5HM012345', N'Volkswagen',    N'Jetta',          2017,  7600.00, N'Paid'),
 (9,  N'1C4RJFBG0KC765432', N'Jeep',          N'Grand Cherokee', 2019, 21300.00, N'Paid'),
 (10, N'5NPE34AF4JH678901', N'Hyundai',       N'Sonata',         2018,  9400.00, N'Paid'),
 (11, N'KNDJP3A55H7123987', N'Kia',           N'Soul',           2017,  6900.00, N'Delivered'),
 (12, N'1G1ZD5ST4JF246810', N'Chevrolet',     N'Malibu',         2018,  8900.00, N'ReadyForPickup'),
 (13, N'4T1B11HK5KU135791', N'Toyota',        N'Camry',          2020, 15800.00, N'Paid'),
 (14, N'WAUENAF41KN112233', N'Audi',          N'A4',             2019, 19900.00, N'Reserved'),
 (15, N'JM1BPACL5K1445566', N'Mazda',         N'3',              2019, 10800.00, N'Reserved'),
 (16, N'1FTEW1EP7JKD77889', N'Ford',          N'F-150',          2018, 23900.00, N'Reserved'),
 (17, N'5UXCR6C05L9B99001', N'BMW',           N'X5',             2020, 33500.00, N'Reserved'),
 (18, N'WDDZF4JB3KA223344', N'Mercedes-Benz', N'E 300',          2019, 27900.00, N'SoldToOther'),
 (19, N'19XFC2F59KE556677', N'Honda',         N'Civic',          2019, 12900.00, N'Available'),
 (20, N'2HKRW2H85KH889900', N'Honda',         N'CR-V',           2019, 18400.00, N'Available'),
 (21, N'JN8AT2MV4KW101112', N'Nissan',        N'Rogue',          2019, 13700.00, N'Available'),
 (22, N'1HGCV1F34KA131415', N'Honda',         N'Accord',         2019, 16200.00, N'Available'),
 (23, N'3FA6P0H74JR161718', N'Ford',          N'Fusion',         2018,  8300.00, N'Available'),
 (24, N'5YJSA1E26JF192021', N'Tesla',         N'Model S',        2018, 31900.00, N'Available'),
 (25, N'JTMBFREV5JD222324', N'Toyota',        N'RAV4',           2018, 15400.00, N'Available'),
 (26, N'KMHD84LF5JU252627', N'Hyundai',       N'Elantra',        2018,  8500.00, N'Available');
SET IDENTITY_INSERT dbo.Cars OFF;

SET IDENTITY_INSERT dbo.Orders ON;
INSERT INTO dbo.Orders (OrderId, ClientId, ManagerId, CarId, MaxBudget, FinalPrice, Status, CreatedAt) VALUES
 (1,  1,  13,   1,  13000.00, 12000.00, N'Completed',          '2026-01-20T10:15:00'),
 (2,  2,  13,   2,  15000.00, 14500.00, N'Completed',          '2026-02-10T12:40:00'),
 (3,  3,  14,   3,  19000.00, 17800.00, N'Completed',          '2026-03-25T09:05:00'),
 (4,  4,  13,   4,  26000.00, 24500.00, N'Shipping',           '2026-05-28T14:30:00'),
 (5,  5,  14,   5,  11000.00,  9900.00, N'Shipping',           '2026-06-20T11:00:00'),
 (6,  6,  13,   6,  12000.00, 11200.00, N'Shipping',           '2026-07-28T16:20:00'),
 (7,  7,  14,   7,   9500.00,  8700.00, N'Paid',               '2026-09-01T10:00:00'),
 (8,  8,  13,   8,   8000.00,  7600.00, N'Paid',               '2026-09-18T13:45:00'),
 (9,  9,  14,   9,  23000.00, 21300.00, N'Paid',               '2026-10-01T09:30:00'),
 (10, 10, 13,  10,  10000.00,  9400.00, N'Paid',               '2026-10-08T15:10:00'),
 (11, 11, 14,  11,   7500.00,  6900.00, N'Completed',          '2026-05-02T10:50:00'),
 (12, 12, 13,  12,   9500.00,  8900.00, N'Shipping',           '2026-07-30T12:00:00'),
 (13, 1,  14,  13,  16500.00, 15800.00, N'Paid',               '2026-10-10T11:25:00'),
 (14, 2,  13,  14,  21000.00, 19900.00, N'PaymentPending',     '2026-10-12T17:05:00'),
 (15, 3,  14,  15,  11500.00, 10800.00, N'Invoiced',           '2026-10-15T10:40:00'),
 (16, 4,  13,  16,  25000.00, 23900.00, N'Invoiced',           '2026-10-16T14:15:00'),
 (17, 5,  14,  17,  35000.00, 33500.00, N'BidWon',             '2026-10-18T09:55:00'),
 (18, 6,  13,  18,  25000.00, NULL,     N'BidLost',            '2026-10-19T16:30:00'),
 (19, 7,  14,  19,  14000.00, NULL,     N'InProgress',         '2026-10-20T10:05:00'),
 (20, 8,  NULL, 20, 19000.00, NULL,     N'New',                '2026-10-22T18:20:00'),
 (21, 9,  13,  21,  14500.00, NULL,     N'DocumentsRequested', '2026-10-21T11:35:00'),
 (22, 10, 14,  22,  17000.00, NULL,     N'DocumentsProvided',  '2026-10-21T15:50:00'),
 (23, 11, NULL, 23,  9000.00, NULL,     N'Cancelled',          '2026-09-05T08:45:00'),
 (24, 12, NULL, 24, 33000.00, NULL,     N'New',                '2026-10-25T19:10:00'),
 (25, 1,  13,  19,  12000.00, NULL,     N'Cancelled',          '2026-08-14T13:00:00');
SET IDENTITY_INSERT dbo.Orders OFF;

SET IDENTITY_INSERT dbo.Insurances ON;
INSERT INTO dbo.Insurances (InsuranceId, OrderId, Type, Price) VALUES
 (1,  1,  N'Базове (КАСКО на час перевезення)',       250.00),
 (2,  2,  N'Розширене (КАСКО + пошкодження в порту)', 380.00),
 (3,  3,  N'Повне (усі ризики)',                      520.00),
 (4,  4,  N'Повне (усі ризики)',                      690.00),
 (5,  5,  N'Базове (КАСКО на час перевезення)',       210.00),
 (6,  6,  N'Розширене (КАСКО + пошкодження в порту)', 300.00),
 (7,  9,  N'Повне (усі ризики)',                      610.00),
 (8,  11, N'Базове (КАСКО на час перевезення)',       180.00),
 (9,  13, N'Розширене (КАСКО + пошкодження в порту)', 420.00),
 (10, 14, N'Повне (усі ризики)',                      560.00),
 (11, 15, N'Базове (КАСКО на час перевезення)',       240.00),
 (12, 17, N'Повне (усі ризики)',                      900.00),
 (13, 19, N'Розширене (КАСКО + пошкодження в порту)', 330.00),
 (14, 4,  N'Базове (КАСКО на час перевезення)',       150.00);
SET IDENTITY_INSERT dbo.Insurances OFF;

SET IDENTITY_INSERT dbo.Invoices ON;
INSERT INTO dbo.Invoices (InvoiceId, OrderId, TotalAmount, IsPaid, PaidAt) VALUES
 (1,  1,  12000.00, 1, '2026-02-14T10:30:00'),
 (2,  2,  14500.00, 1, '2026-03-03T12:00:00'),
 (3,  3,  17800.00, 1, '2026-04-18T09:15:00'),
 (4,  11,  6900.00, 1, '2026-05-22T16:40:00'),
 (5,  4,  24500.00, 1, '2026-06-10T11:05:00'),
 (6,  5,   9900.00, 1, '2026-07-05T14:20:00'),
 (7,  6,  11200.00, 1, '2026-08-12T10:00:00'),
 (8,  12,  8900.00, 1, '2026-08-12T15:30:00'),
 (9,  7,   8700.00, 1, '2026-09-15T13:10:00'),
 (10, 8,   7600.00, 1, '2026-10-02T10:45:00'),
 (11, 9,  21300.00, 1, '2026-10-14T12:30:00'),
 (12, 10,  9400.00, 1, '2026-10-20T17:00:00'),
 (13, 13, 15800.00, 1, '2026-10-21T11:20:00'),
 (14, 14, 19900.00, 1, '2026-10-24T09:50:00'),
 (15, 15, 10800.00, 0, NULL),
 (16, 16, 23900.00, 0, NULL);
SET IDENTITY_INSERT dbo.Invoices OFF;

INSERT INTO dbo.Deliveries (OrderId, LogisticianId, TrackingNumber, CurrentLocation, IsCustomsCleared, Status) VALUES
 (1,  15, N'TRK-1-2602201030',  N'Чернігів, склад видачі',  1, N'Доставлено'),
 (2,  16, N'TRK-2-2603101200',  N'Київ, склад видачі',      1, N'Доставлено'),
 (3,  15, N'TRK-3-2604251000',  N'Львів, склад видачі',     1, N'Доставлено'),
 (11, 16, N'TRK-11-2605281100', N'Черкаси, склад видачі',   1, N'Доставлено'),
 (4,  15, N'TRK-4-2606151400',  N'Порт Одеса',              1, N'В дорозі'),
 (5,  16, N'TRK-5-2607101000',  N'Польща, порт Гданськ',    1, N'В дорозі'),
 (6,  15, N'TRK-6-2608150900',  N'Дніпро, склад видачі',    1, N'Готово до видачі'),
 (12, 16, N'TRK-12-2608161200', N'Ужгород, склад видачі',   1, N'Готово до видачі'),
 (7,  15, N'TRK-7-2609181000',  N'США, порт Нью-Джерсі',    0, N'Перевізника призначено'),
 (8,  16, N'TRK-8-2610051100',  N'США, склад аукціону',     0, N'Перевізника призначено'),
 (9,  15, N'TRK-9-2610161500',  N'США, склад аукціону',     0, N'Перевізника призначено'),
 (10, 16, N'TRK-10-2610221000', N'США, склад аукціону',     0, N'Перевізника призначено');
GO

/* ---------- Перевірка: кількість рядків у кожній таблиці ---------- */
SELECT N'Users' AS [Таблиця], COUNT(*) AS [Рядків] FROM dbo.Users
UNION ALL SELECT N'Cars', COUNT(*) FROM dbo.Cars
UNION ALL SELECT N'Orders', COUNT(*) FROM dbo.Orders
UNION ALL SELECT N'Insurances', COUNT(*) FROM dbo.Insurances
UNION ALL SELECT N'Invoices', COUNT(*) FROM dbo.Invoices
UNION ALL SELECT N'Deliveries', COUNT(*) FROM dbo.Deliveries;
GO
