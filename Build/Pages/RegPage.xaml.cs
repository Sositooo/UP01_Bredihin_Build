using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Build.Pages
{
    public partial class RegPage : Page
    {
        public RegPage()
        {
            InitializeComponent();
            TbLogin.PreviewTextInput += Core.OnlyLatin;     // логин: только латиница, цифры
            TbFio.PreviewTextInput += Core.OnlyLetters;     // ФИО: только буквы, пробел, дефис

            // Роли из БД. 
            RoleBox.ItemsSource = Core.Db.Roles.ToList();
            RoleBox.SelectedIndex = 0;
        }

        private void Pass_Changed(object sender, RoutedEventArgs e)
        {
            Core.PassHint(sender);
        }

        private void BtnReg_Click(object sender, RoutedEventArgs e)
        {
            // 1. Все поля заполнены?
            if (string.IsNullOrWhiteSpace(TbLogin.Text) || string.IsNullOrWhiteSpace(TbFio.Text) ||
                PassBox.Password == "" || PassBox2.Password == "")
            {
                Core.Error("Заполните все поля!");
                return;
            }

            // 2. Формат логина, ФИО и пароля
            string login = TbLogin.Text.Trim();
            string fio = TbFio.Text.Trim();
            string error = Core.CheckLogin(login) ?? Core.CheckFio(fio) ?? Core.CheckPassword(PassBox.Password);
            if (error != null)
            {
                Core.Error(error);
                return;
            }

            // 3. Пароли совпадают?
            if (PassBox.Password != PassBox2.Password)
            {
                Core.Error("Пароли не совпадают!");
                return;
            }

            // 4. Такого логина ещё нет?
            if (Core.Db.Users.Any(u => u.Login == login))
            {
                Core.Error("Пользователь с таким логином уже существует!");
                return;
            }

            // 5. Всё хорошо - создаём пользователя (в БД пароль хранится в виде хэша)
            var role = (Roles)RoleBox.SelectedItem;
            var user = new Users
            {
                Login = login,
                FullName = fio,
                Password = Core.GetHash(PassBox.Password),
                RoleId = role.Id
            };
            Core.Db.Users.Add(user);
            if (!Core.Save()) return;

            Core.Info("Пользователь успешно зарегистрирован! Теперь можно войти.");
            NavigationService.GoBack();
        }
    }
}
