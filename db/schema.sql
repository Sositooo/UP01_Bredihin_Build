-- =====================================================================
--  СЕРВИС АРЕНДЫ СТРОИТЕЛЬНОЙ И СПЕЦИАЛЬНОЙ ТЕХНИКИ — БД на 10 таблиц
--  Открыть в SSMS -> New Query -> вставить -> Execute (F5)
--
--  Как БД связана с вашими диаграммами:
--    IDEF0 / DFD, хранилища данных:
--        "Клиенты"  -> Clients        "Заявки"   -> RentalRequests + RequestItems
--        "Техника"  -> Machines       "Договоры" -> Contracts
--    DFD, внешние сущности (Клиент, Менеджер по аренде, Бухгалтер)
--        -> Roles + Users
--    "Данные для оплаты" / "Подтверждение оплаты"   -> Payments
--    "Акт выдачи" / "Акт возврата" / "Контроль состояния техники"
--        -> HandoverActs
--    "График занятости техники" -> не отдельная таблица, а выборка:
--        техника + периоды из RentalRequests (статус 'Одобрена')
--    Диаграмма переходов состояний:
--        "Корзина с техникой"  -> RentalRequests.Status = 'Черновик'
--        "Отказ в аренде"      -> RentalRequests.Status = 'Отказ' + RejectReason
--        "Окно оплаты"         -> Contracts.Status = 'Ожидает оплаты'
--        "Договор оформлен"    -> Contracts.Status = 'Оформлен'
--
--  Логика выбора справочников: Roles и MachineCategories - настоящие
--  справочники (их может пополнять администратор). Статусы и способ оплаты -
--  фиксированные списки, жёстко связанные с логикой программы (диаграмма
--  состояний), поэтому они сделаны столбцами с ограничением CHECK.
--
--  Таблица техники названа Machines, а не Equipment: слово Equipment
--  в английском не имеет множественного числа, и мастер Entity Framework
--  может запутаться в именах класса и набора (EntityType/EntitySet).
-- =====================================================================

IF DB_ID('ConstructionRentalDB') IS NOT NULL
BEGIN
    ALTER DATABASE ConstructionRentalDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE ConstructionRentalDB;
END
GO
CREATE DATABASE ConstructionRentalDB;
GO
USE ConstructionRentalDB;
GO

-- 1) Роли пользователей (Клиент / Менеджер по аренде / Бухгалтер)
CREATE TABLE Roles (
    Id   INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(50) NOT NULL UNIQUE
);
GO

-- 2) Категории техники (справочник)
CREATE TABLE MachineCategories (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Name        NVARCHAR(100) NOT NULL UNIQUE,
    Description NVARCHAR(300) NULL
);
GO

-- 3) Учётные записи для входа в приложение (авторизация/регистрация)
CREATE TABLE Users (
    Id       INT IDENTITY(1,1) PRIMARY KEY,
    Login    NVARCHAR(50)  NOT NULL UNIQUE,
    Password NVARCHAR(50)  NOT NULL,
    FullName NVARCHAR(100) NOT NULL,
    RoleId   INT NOT NULL FOREIGN KEY REFERENCES Roles(Id)
);
GO

-- 4) Клиенты (хранилище "Клиенты" из DFD).
--    UserId может быть NULL: клиента мог завести менеджер без учётной записи
--    (например, пришёл лично). Для юр. лица заполняется CompanyName.
CREATE TABLE Clients (
    Id           INT IDENTITY(1,1) PRIMARY KEY,
    UserId       INT NULL FOREIGN KEY REFERENCES Users(Id),
    FullName     NVARCHAR(100) NOT NULL,
    ClientType   NVARCHAR(30)  NOT NULL
                 CHECK (ClientType IN (N'Физическое лицо', N'Юридическое лицо')),
    CompanyName  NVARCHAR(150) NULL,
    Phone        NVARCHAR(20)  NOT NULL,
    Email        NVARCHAR(100) NULL,
    DocumentData NVARCHAR(100) NOT NULL,   -- паспорт (физ. лицо) или ИНН (юр. лицо)
    Address      NVARCHAR(200) NULL
);
GO

-- 5) Техника (хранилище "Техника" из DFD).
--    DailyPrice, MinRentDays и DepositAmount - это "Цены на аренду и правила"
--    из IDEF0 (управляющее воздействие). Status - исправность самой единицы
--    техники ("Контроль состояния техники"); занятость по датам определяется
--    не статусом, а пересечением периодов в заявках (см. график занятости).
CREATE TABLE Machines (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    Name            NVARCHAR(150) NOT NULL,
    Model           NVARCHAR(100) NOT NULL,
    InventoryNumber NVARCHAR(30)  NOT NULL UNIQUE,
    CategoryId      INT NOT NULL FOREIGN KEY REFERENCES MachineCategories(Id),
    ManufactureYear INT NULL,
    DailyPrice      DECIMAL(12,2) NOT NULL CHECK (DailyPrice > 0),
    DepositAmount   DECIMAL(12,2) NOT NULL DEFAULT 0 CHECK (DepositAmount >= 0),
    MinRentDays     INT NOT NULL DEFAULT 1 CHECK (MinRentDays >= 1),
    Status          NVARCHAR(30)  NOT NULL DEFAULT N'Исправна'
                    CHECK (Status IN (N'Исправна', N'На обслуживании', N'Списана')),
    Description     NVARCHAR(300) NULL
);
GO

-- 6) Заявки на аренду (хранилище "Заявки" из DFD).
--    Один период аренды на всю заявку; техника лежит в RequestItems ("корзина").
--      Черновик  - корзина с техникой, заявка ещё не отправлена
--      Подана    - отправлена, менеджер проверяет данные и наличие техники
--      Одобрена  - данные верны, техника есть, оформляется договор
--      Отказ     - "Отказ в аренде" (причина в RejectReason)
CREATE TABLE RentalRequests (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    ClientId        INT NOT NULL FOREIGN KEY REFERENCES Clients(Id),
    ManagerId       INT NULL FOREIGN KEY REFERENCES Users(Id),   -- назначается при обработке
    CreatedAt       DATETIME NOT NULL DEFAULT GETDATE(),
    RentStart       DATE NOT NULL,
    RentEnd         DATE NOT NULL,
    DeliveryAddress NVARCHAR(200) NULL,                          -- адрес стройплощадки
    Status          NVARCHAR(30) NOT NULL DEFAULT N'Черновик'
                    CHECK (Status IN (N'Черновик', N'Подана', N'Одобрена', N'Отказ')),
    RejectReason    NVARCHAR(300) NULL,
    CONSTRAINT CK_RentalRequests_Dates CHECK (RentEnd >= RentStart)
);
GO

-- 7) Позиции заявки - "Корзина с техникой" (в заявку можно добавить несколько
--    единиц техники - стрелка "добавить ещё" на диаграмме состояний).
--    DailyPrice - цена на момент заявки (чтобы смена прайса не меняла старые заявки).
CREATE TABLE RequestItems (
    Id         INT IDENTITY(1,1) PRIMARY KEY,
    RequestId  INT NOT NULL FOREIGN KEY REFERENCES RentalRequests(Id),
    MachineId  INT NOT NULL FOREIGN KEY REFERENCES Machines(Id),
    DailyPrice DECIMAL(12,2) NOT NULL,
    CONSTRAINT UQ_RequestItems UNIQUE (RequestId, MachineId)   -- одну технику дважды не добавить
);
GO

-- 8) Договоры аренды (хранилище "Договоры" из DFD).
--    Договор создаётся по одобренной заявке (связь 1:1 через UNIQUE RequestId).
--      Ожидает оплаты - "Окно оплаты"
--      Оформлен       - "Договор оформлен" (оплата прошла)
--      Выдан          - техника выдана клиенту
--      Закрыт         - вся техника возвращена
--      Отменён        - "Оплата не прошла" -> отказ в аренде
CREATE TABLE Contracts (
    Id             INT IDENTITY(1,1) PRIMARY KEY,
    ContractNumber NVARCHAR(30) NOT NULL UNIQUE,
    RequestId      INT NOT NULL UNIQUE FOREIGN KEY REFERENCES RentalRequests(Id),
    ClientId       INT NOT NULL FOREIGN KEY REFERENCES Clients(Id),
    ManagerId      INT NOT NULL FOREIGN KEY REFERENCES Users(Id),
    SignedAt       DATETIME NOT NULL DEFAULT GETDATE(),
    TotalAmount    DECIMAL(12,2) NOT NULL CHECK (TotalAmount >= 0),
    Status         NVARCHAR(30) NOT NULL DEFAULT N'Ожидает оплаты'
                   CHECK (Status IN (N'Ожидает оплаты', N'Оформлен', N'Выдан', N'Закрыт', N'Отменён'))
);
GO

-- 9) Платежи ("Данные для оплаты" / "Подтверждение оплаты" из DFD).
--    Связь с договором 1:1 (UNIQUE ContractId): договор оплачивается ОДНИМ
--    платежом на всю сумму, частичные платежи не предусмотрены.
--    IsConfirmed = 1, когда бухгалтер подтвердил поступление денег.
CREATE TABLE Payments (
    Id            INT IDENTITY(1,1) PRIMARY KEY,
    ContractId    INT NOT NULL UNIQUE FOREIGN KEY REFERENCES Contracts(Id),
    Amount        DECIMAL(12,2) NOT NULL CHECK (Amount > 0),
    PaymentMethod NVARCHAR(30)  NOT NULL
                  CHECK (PaymentMethod IN (N'Карта', N'Наличные', N'Безналичный расчёт')),
    PaidAt        DATETIME NOT NULL DEFAULT GETDATE(),
    IsConfirmed   BIT NOT NULL DEFAULT 0,
    AccountantId  INT NULL FOREIGN KEY REFERENCES Users(Id)
);
GO

-- 10) Акты выдачи и возврата техники (A21 "Выдача техники", A22 "Возврат
--     техники", A23 "Контроль состояния техники" из IDEF0).
--     Одна строка = один акт по одной единице техники.
--     ExtraCharge - доплата за просрочку или повреждение (попадает в отчёт).
CREATE TABLE HandoverActs (
    Id             INT IDENTITY(1,1) PRIMARY KEY,
    ContractId     INT NOT NULL FOREIGN KEY REFERENCES Contracts(Id),
    MachineId      INT NOT NULL FOREIGN KEY REFERENCES Machines(Id),
    ActType        NVARCHAR(20) NOT NULL CHECK (ActType IN (N'Выдача', N'Возврат')),
    ActDate        DATETIME NOT NULL DEFAULT GETDATE(),
    ManagerId      INT NOT NULL FOREIGN KEY REFERENCES Users(Id),
    ConditionState NVARCHAR(30) NOT NULL
                   CHECK (ConditionState IN (N'Исправна', N'Требует ремонта', N'Повреждена')),
    Notes          NVARCHAR(300) NULL,
    ExtraCharge    DECIMAL(12,2) NOT NULL DEFAULT 0 CHECK (ExtraCharge >= 0)
);
GO

-- =====================================================================
-- ТЕСТОВЫЕ ДАННЫЕ
-- (Id везде присваиваются по порядку вставки: 1, 2, 3, ...,
--  на это порядковое соответствие опираются внешние ключи ниже)
--
-- Учётные записи для проверки входа (в БД лежат хэши, вводить нужно пароли ниже):
--     manager1    / manager123   (Менеджер по аренде)
--     accountant1 / account123   (Бухгалтер)
--     client1     / client123    (Клиент - физ. лицо)
--     client2     / client123    (Клиент - юр. лицо)
-- =====================================================================

INSERT INTO Roles (Name) VALUES
    (N'Клиент'),               -- 1
    (N'Менеджер по аренде'),   -- 2
    (N'Бухгалтер');            -- 3
GO

INSERT INTO MachineCategories (Name, Description) VALUES
    (N'Экскаваторы',       N'Гусеничные и колёсные экскаваторы, мини-экскаваторы'),  -- 1
    (N'Бульдозеры',        N'Гусеничные бульдозеры для планировки и земляных работ'),-- 2
    (N'Автокраны',         N'Автомобильные краны различной грузоподъёмности'),       -- 3
    (N'Самосвалы',         N'Грузовые самосвалы для перевозки сыпучих материалов'),  -- 4
    (N'Погрузчики',        N'Фронтальные и мини-погрузчики'),                        -- 5
    (N'Бетонная техника',  N'Автобетоносмесители и автобетононасосы');               -- 6
GO

-- Пароли хранятся в виде SHA1-хэша (40 символов, как делает метод Core.GetHash в приложении).
INSERT INTO Users (Login, Password, FullName, RoleId) VALUES
    (N'manager1',    N'F35BC30C0AB883785EB8909FE8DB729E6E591A9E', N'Орлов Дмитрий Андреевич',     2),  -- пароль manager123
    (N'accountant1', N'32C273BB19E676D36490C4F7604A2F4DC5A03EB9', N'Воронова Елена Игоревна',     3),  -- пароль account123
    (N'client1',     N'7172E7D67C68576D4F308337E1AA6D533BE7EBC8', N'Иванов Сергей Петрович',      1),  -- пароль client123
    (N'client2',     N'7172E7D67C68576D4F308337E1AA6D533BE7EBC8', N'Петров Алексей Николаевич',   1);  -- пароль client123
GO

INSERT INTO Clients (UserId, FullName, ClientType, CompanyName, Phone, Email, DocumentData, Address) VALUES
    (3,    N'Иванов Сергей Петрович',  N'Физическое лицо', NULL,
           N'89001112233', N'ivanov@example.com',  N'Паспорт 4510 123456', N'г. Москва, ул. Лесная, 7'),         -- 1
    (4,    N'Петров Алексей Николаевич', N'Юридическое лицо', N'ООО "СтройМонтаж"',
           N'84951234567', N'office@stroymontazh.ru', N'ИНН 7701234567',   N'г. Москва, ул. Строителей, 12'),     -- 2
    (NULL, N'Смирнов Олег Викторович', N'Физическое лицо', NULL,
           N'89005556677', NULL,                    N'Паспорт 4511 654321', N'г. Химки, ул. Заводская, 3');       -- 3
GO

INSERT INTO Machines (Name, Model, InventoryNumber, CategoryId, ManufactureYear,
                      DailyPrice, DepositAmount, MinRentDays, Status, Description) VALUES
    (N'Экскаватор гусеничный',       N'CAT 320',          N'EX-001', 1, 2019, 18000.00, 30000.00, 1, N'Исправна',        N'Ковш 1.2 м3, масса 22 т'),                -- 1
    (N'Мини-экскаватор',             N'Bobcat E35',       N'EX-002', 1, 2020,  9000.00, 15000.00, 1, N'Исправна',        N'Масса 3.5 т, для работ в стеснённых условиях'), -- 2
    (N'Бульдозер',                   N'Komatsu D65',      N'BD-001', 2, 2018, 22000.00, 40000.00, 1, N'Исправна',        N'Мощность 205 л.с.'),                      -- 3
    (N'Бульдозер',                   N'Shantui SD16',     N'BD-002', 2, 2021, 15000.00, 25000.00, 1, N'Исправна',        N'Мощность 160 л.с.'),                      -- 4
    (N'Автокран 25 т',               N'КС-45717',         N'CR-001', 3, 2017, 25000.00, 50000.00, 2, N'Исправна',        N'Грузоподъёмность 25 т, вылет стрелы 21 м'),-- 5
    (N'Автокран 50 т',               N'КС-55713',         N'CR-002', 3, 2019, 38000.00, 80000.00, 2, N'Исправна',        N'Грузоподъёмность 50 т, вылет стрелы 31 м'),-- 6
    (N'Самосвал 20 т',               N'КАМАЗ 6520',       N'DT-001', 4, 2020,  8000.00, 10000.00, 1, N'Исправна',        N'Объём кузова 12 м3'),                     -- 7
    (N'Самосвал 25 т',               N'Shacman SX3258',   N'DT-002', 4, 2021,  8500.00, 10000.00, 1, N'Исправна',        N'Объём кузова 16 м3'),                     -- 8
    (N'Фронтальный погрузчик',       N'Amkodor 342',      N'LD-001', 5, 2019, 12000.00, 20000.00, 1, N'Исправна',        N'Грузоподъёмность 4.2 т'),                 -- 9
    (N'Мини-погрузчик',              N'Bobcat S450',      N'LD-002', 5, 2022,  7000.00, 12000.00, 1, N'Исправна',        N'Грузоподъёмность 0.7 т'),                 -- 10
    (N'Автобетоносмеситель 9 м3',    N'Тонар 5835',       N'CM-001', 6, 2018, 11000.00, 15000.00, 1, N'Исправна',        N'Объём барабана 9 м3'),                    -- 11
    (N'Автобетононасос 32 м',        N'Putzmeister M32',  N'CM-002', 6, 2016, 30000.00, 60000.00, 1, N'На обслуживании', N'Плановое ТО, временно недоступен'),       -- 12
    (N'Экскаватор колёсный',         N'JCB JS160W',       N'EX-003', 1, 2008, 10000.00, 20000.00, 1, N'Списана',         N'Выведен из эксплуатации');                -- 13
GO

-- Блок с датами выполняется одним пакетом (без GO внутри), потому что
-- переменная @Today живёт только в пределах одного пакета.
DECLARE @Today DATE = CAST(GETDATE() AS DATE);

-- Заявки:
--   1 - завершённая аренда мини-экскаватора (физ. лицо)
--   2 - действующая аренда крана и самосвала (юр. лицо)
--   3 - "корзина": черновик, ещё не отправлен
--   4 - отказ: кран уже занят по договору №2 на эти даты
INSERT INTO RentalRequests (ClientId, ManagerId, CreatedAt, RentStart, RentEnd, DeliveryAddress, Status, RejectReason) VALUES
    (1, 1,    DATEADD(DAY, -20, GETDATE()), DATEADD(DAY, -15, @Today), DATEADD(DAY, -12, @Today),
              N'г. Москва, ул. Лесная, 7',       N'Одобрена', NULL),                                              -- 1
    (2, 1,    DATEADD(DAY, -5,  GETDATE()), DATEADD(DAY, -2,  @Today), DATEADD(DAY,  5,  @Today),
              N'г. Москва, ул. Строителей, 12',  N'Одобрена', NULL),                                              -- 2
    (1, NULL, GETDATE(),                    DATEADD(DAY, 10,  @Today), DATEADD(DAY, 14,  @Today),
              N'г. Москва, ул. Лесная, 7',       N'Черновик', NULL),                                              -- 3
    (3, 1,    DATEADD(DAY, -1,  GETDATE()), DATEADD(DAY, -1,  @Today), DATEADD(DAY,  3,  @Today),
              N'г. Химки, ул. Заводская, 3',     N'Отказ',    N'Техника занята на выбранные даты');               -- 4

-- Корзины (DailyPrice - цена на момент заявки)
INSERT INTO RequestItems (RequestId, MachineId, DailyPrice) VALUES
    (1, 2,  9000.00),     -- мини-экскаватор
    (2, 5, 25000.00),     -- автокран 25 т
    (2, 7,  8000.00),     -- самосвал 20 т
    (3, 1, 18000.00),     -- экскаватор CAT 320 (в черновике)
    (4, 5, 25000.00);     -- автокран 25 т (заявка отклонена)

-- Договоры (по одобренным заявкам 1 и 2).
-- Сумма = дни аренды (включая оба края) * цена за сутки:
--   договор 1: 4 дня * 9000                    = 36 000
--   договор 2: 8 дней * (25 000 + 8 000)       = 264 000
INSERT INTO Contracts (ContractNumber, RequestId, ClientId, ManagerId, SignedAt, TotalAmount, Status) VALUES
    (N'АР-0001', 1, 1, 1, DATEADD(DAY, -19, GETDATE()),  36000.00, N'Закрыт'),   -- 1
    (N'АР-0002', 2, 2, 1, DATEADD(DAY, -4,  GETDATE()), 264000.00, N'Выдан');    -- 2

INSERT INTO Payments (ContractId, Amount, PaymentMethod, PaidAt, IsConfirmed, AccountantId) VALUES
    (1,  36000.00, N'Карта',              DATEADD(DAY, -19, GETDATE()), 1, 2),
    (2, 264000.00, N'Безналичный расчёт', DATEADD(DAY, -3,  GETDATE()), 1, 2);

-- Акты: по договору 1 техника выдана и возвращена, по договору 2 - только выдана
INSERT INTO HandoverActs (ContractId, MachineId, ActType, ActDate, ManagerId, ConditionState, Notes, ExtraCharge) VALUES
    (1, 2, N'Выдача',  DATEADD(HOUR,  9, CAST(DATEADD(DAY, -15, @Today) AS DATETIME)), 1, N'Исправна', N'Выдан без замечаний',  0),
    (1, 2, N'Возврат', DATEADD(HOUR, 18, CAST(DATEADD(DAY, -12, @Today) AS DATETIME)), 1, N'Исправна', N'Возвращён в срок',     0),
    (2, 5, N'Выдача',  DATEADD(HOUR, 10, CAST(DATEADD(DAY, -2,  @Today) AS DATETIME)), 1, N'Исправна', N'Выдан без замечаний',  0),
    (2, 7, N'Выдача',  DATEADD(HOUR, 10, CAST(DATEADD(DAY, -2,  @Today) AS DATETIME)), 1, N'Исправна', N'Выдан без замечаний',  0);
GO

-- Быстрая проверка: сколько строк в каждой таблице
SELECT 'Roles' AS TableName, COUNT(*) AS Rows FROM Roles
UNION ALL SELECT 'MachineCategories', COUNT(*) FROM MachineCategories
UNION ALL SELECT 'Users',             COUNT(*) FROM Users
UNION ALL SELECT 'Clients',           COUNT(*) FROM Clients
UNION ALL SELECT 'Machines',          COUNT(*) FROM Machines
UNION ALL SELECT 'RentalRequests',    COUNT(*) FROM RentalRequests
UNION ALL SELECT 'RequestItems',      COUNT(*) FROM RequestItems
UNION ALL SELECT 'Contracts',         COUNT(*) FROM Contracts
UNION ALL SELECT 'Payments',          COUNT(*) FROM Payments
UNION ALL SELECT 'HandoverActs',      COUNT(*) FROM HandoverActs;
GO

PRINT 'Готово: база ConstructionRentalDB создана (10 таблиц) и заполнена.';
