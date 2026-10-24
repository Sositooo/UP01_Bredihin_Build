using System;
using System.Collections.Generic;
using System.Data;                      
using System.Data.Entity;               
using System.Linq;
using System.Security.Cryptography;     
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Build
{
   
    public static class Core
    {

        public static BuildEntities Db = new BuildEntities();

        // Кто сейчас вошёл в систему (null - никто)
        public static Users CurrentUser;

        // Название роли обычного пользователя. Все остальные роли = администратор
        public const string ClientRole = "Клиент";
        public static bool IsAdmin
        {
            get { return CurrentUser != null && CurrentUser.RoleName != ClientRole; }
        }


        // СООБЩЕНИЯ (вызываются из любой страницы одной строкой)
        public static void Info(string text)
        {
            MessageBox.Show(text, "Build", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static void Error(string text)
        {
            MessageBox.Show(text, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // Вопрос "Да/Нет": возвращает true, если пользователь нажал "Да"
        public static bool Ask(string text)
        {
            return MessageBox.Show(text, "Подтверждение", MessageBoxButton.YesNo,
                                   MessageBoxImage.Question) == MessageBoxResult.Yes;
        }


        // ПАРОЛИ: хэширование SHA1. В БД хранится только хэш (40 символов)
        public static string GetHash(string password)
        {
            using (var hash = SHA1.Create())
            {
                return string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(password))
                                         .Select(x => x.ToString("X2")));
            }
        }


        // АВТОРИЗАЦИЯ: ищем пользователя по логину и хэшу пароля
        public static Users Login(string login, string password)
        {
            string hash = GetHash(password);
            return Db.Users.FirstOrDefault(u => u.Login == login && u.Password == hash);
        }

       
        // ПРОВЕРКИ ФОРМАТА ДАННЫХ. Каждая возвращает текст ошибки или null, если всё хорошо
        public static string CheckLogin(string login)
        {
            if (!Regex.IsMatch(login, @"^[A-Za-z0-9_]{3,20}$"))
                return "Логин: от 3 до 20 символов, только английские буквы, цифры и _";
            return null;
        }

        // Правила пароля: 6+ символов, только английская раскладка, хотя бы одна цифра
        public static string CheckPassword(string password)
        {
            if (password.Length < 6)
                return "Пароль слишком короткий, должно быть минимум 6 символов!";
            bool english = password.All(c => (c >= '0' && c <= '9') ||
                                             (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'));
            if (!english)
                return "Используйте только английскую раскладку (буквы и цифры)!";
            if (!password.Any(char.IsDigit))
                return "Добавьте хотя бы одну цифру!";
            return null;
        }

        public static string CheckFio(string fio)
        {
            if (!Regex.IsMatch(fio, @"^[А-Яа-яЁё\- ]{5,100}$") || !fio.Trim().Contains(" "))
                return "ФИО: только русские буквы, пробелы и дефис, минимум имя и фамилия";
            return null;
        }

        // Ограничения на ввод прямо во время набора
        public static void OnlyDigits(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        public static void OnlyLatin(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(c => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') ||
                                         (c >= 'a' && c <= 'z') || c == '_');
        }

        public static void OnlyLetters(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(c => char.IsLetter(c) || c == ' ' || c == '-');
        }

        // Плейсхолдер для PasswordBox: прячем/показываем подсказку, лежащую в той же Grid
        public static void PassHint(object sender)
        {
            var box = (PasswordBox)sender;
            var hint = ((Panel)box.Parent).Children[1];
            hint.Visibility = box.Password.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // КАПЧА: случайная строка из 6 символов (без похожих O/0, l/1/I)
        static Random rnd = new Random();

        public static string NewCaptcha()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
            var result = new StringBuilder();
            for (int i = 0; i < 6; i++)
                result.Append(chars[rnd.Next(chars.Length)]);
            return result.ToString();
        }


        // СОХРАНЕНИЕ В БД
        // Возвращает true, если всё сохранилось. При ошибке показывает сообщение
        // и откатывает несохранённые изменения, чтобы они не мешали дальше.
        public static bool Save()
        {
            try
            {
                Db.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                Refresh();
                Exception inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                Error("Не удалось сохранить данные:\n" + inner.Message);
                return false;
            }
        }

        // Сбросить все несохранённые правки и заново прочитать данные из БД
        public static void Refresh()
        {
            foreach (var entry in Db.ChangeTracker.Entries().ToList())
            {
                if (entry.State == EntityState.Added) entry.State = EntityState.Detached;
                else entry.Reload();
            }
        }


        // КАСКАДНОЕ УДАЛЕНИЕ: вместе с записью удаляем всё, что на неё ссылается.
        // Цепочка: Пользователь -> Клиент -> Заявка -> Договор -> Платёж / Акт
        // Удалить из таблицы сразу несколько записей
        static void RemoveAll<T>(DbSet<T> table, IEnumerable<T> items) where T : class
        {
            foreach (var item in items.ToList())
                table.Remove(item);
        }

        public static void DeleteMachines(List<Machines> machines)
        {
            foreach (var m in machines)
            {
                int id = m.Id;
                RemoveAll(Db.RequestItems, Db.RequestItems.Where(x => x.MachineId == id).ToList());
                RemoveAll(Db.HandoverActs, Db.HandoverActs.Where(x => x.MachineId == id).ToList());
            }
            RemoveAll(Db.Machines, machines);
        }

        public static void DeleteCategories(List<MachineCategories> categories)
        {
            foreach (var c in categories)
            {
                int id = c.Id;
                DeleteMachines(Db.Machines.Where(x => x.CategoryId == id).ToList());
            }
            RemoveAll(Db.MachineCategories, categories);
        }

        public static void DeleteUsers(List<Users> users)
        {
            foreach (var u in users)
            {
                int id = u.Id;
                foreach (var c in Db.Clients.Where(x => x.UserId == id).ToList()) DeleteClient(c);
                foreach (var r in Db.RentalRequests.Where(x => x.ManagerId == id).ToList()) r.ManagerId = null;
                foreach (var p in Db.Payments.Where(x => x.AccountantId == id).ToList()) p.AccountantId = null;
                foreach (var k in Db.Contracts.Where(x => x.ManagerId == id).ToList()) DeleteContract(k);
                RemoveAll(Db.HandoverActs, Db.HandoverActs.Where(x => x.ManagerId == id).ToList());
            }
            RemoveAll(Db.Users, users);
        }

        static void DeleteClient(Clients client)
        {
            int id = client.Id;
            foreach (var r in Db.RentalRequests.Where(x => x.ClientId == id).ToList()) DeleteRequest(r);
            foreach (var k in Db.Contracts.Where(x => x.ClientId == id).ToList()) DeleteContract(k);
            Db.Clients.Remove(client);
        }

        static void DeleteRequest(RentalRequests request)
        {
            int id = request.Id;
            RemoveAll(Db.RequestItems, Db.RequestItems.Where(x => x.RequestId == id).ToList());
            foreach (var k in Db.Contracts.Where(x => x.RequestId == id).ToList()) DeleteContract(k);
            Db.RentalRequests.Remove(request);
        }

        static void DeleteContract(Contracts contract)
        {
            int id = contract.Id;
            RemoveAll(Db.Payments, Db.Payments.Where(x => x.ContractId == id).ToList());
            RemoveAll(Db.HandoverActs, Db.HandoverActs.Where(x => x.ContractId == id).ToList());
            Db.Contracts.Remove(contract);
        }
    }


    // partial class = продолжение того же класса в другом файле. Так мы добавляем
    // свойства, которых нет в таблице, не трогая автогенерируемый код.

    public partial class Users
    {
        public string RoleName
        {
            get { var role = Core.Db.Roles.Find(RoleId); return role == null ? "" : role.Name; }
        }
    }

    public partial class Machines
    {
        public string CategoryName
        {
            get { var cat = Core.Db.MachineCategories.Find(CategoryId); return cat == null ? "" : cat.Name; }
        }
    }
}
