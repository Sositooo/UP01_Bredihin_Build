using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Build.Pages
{
    public partial class AddUserPage : Page
    {
        // Редактируемый пользователь. Если форму открыли для добавления - создаём нового
        private Users _current = new Users();

        public AddUserPage(Users selectedUser)
        {
            InitializeComponent();
            TbLogin.PreviewTextInput += Core.OnlyLatin;
            TbFio.PreviewTextInput += Core.OnlyLetters;

            RoleBox.ItemsSource = Core.Db.Roles.ToList();

            if (selectedUser != null)
            {
                _current = selectedUser;
                Title = "Редактирование пользователя";
                PassHintText.Text = "Новый пароль (оставьте пустым, чтобы не менять)";
            }
            else
            {
                Title = "Добавление пользователя";
            }

            // Привязка: поля формы показывают свойства _current ({Binding Login} и т.д.)
            DataContext = _current;
        }

        private void Pass_Changed(object sender, RoutedEventArgs e)
        {
            Core.PassHint(sender);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Собираем все ошибки в одно сообщение
            var errors = new StringBuilder();

            string login = (_current.Login ?? "").Trim();
            string fio = (_current.FullName ?? "").Trim();
            int id = _current.Id;

            string loginError = string.IsNullOrEmpty(login) ? "Укажите логин!" : Core.CheckLogin(login);
            if (loginError != null) errors.AppendLine(loginError);
            else if (Core.Db.Users.Any(u => u.Login == login && u.Id != id))
                errors.AppendLine("Пользователь с таким логином уже существует!");

            string fioError = string.IsNullOrEmpty(fio) ? "Укажите ФИО!" : Core.CheckFio(fio);
            if (fioError != null) errors.AppendLine(fioError);

            if (RoleBox.SelectedValue == null) errors.AppendLine("Выберите роль!");

            // Пароль обязателен только для нового пользователя
            bool passEntered = PassBox.Password != "";
            if (_current.Id == 0 && !passEntered) errors.AppendLine("Укажите пароль!");
            else if (passEntered)
            {
                string passError = Core.CheckPassword(PassBox.Password);
                if (passError != null) errors.AppendLine(passError);
            }

            if (errors.Length > 0)
            {
                Core.Error(errors.ToString());
                return;
            }

            _current.Login = login;
            _current.FullName = fio;
            if (passEntered) _current.Password = Core.GetHash(PassBox.Password);

            if (_current.Id == 0) Core.Db.Users.Add(_current);   // новый - добавляем в таблицу
            if (Core.Save())
            {
                Core.Info("Данные успешно сохранены!");
                NavigationService.GoBack();
            }
        }

        private void BtnClean_Click(object sender, RoutedEventArgs e)
        {
            TbLogin.Text = "";
            TbFio.Text = "";
            RoleBox.SelectedIndex = -1;
            PassBox.Clear();
        }
    }
}
