using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Build.Pages
{
    public partial class ChangePassPage : Page
    {
        public ChangePassPage()
        {
            InitializeComponent();
            TbLogin.PreviewTextInput += Core.OnlyLatin;
        }

        private void Pass_Changed(object sender, RoutedEventArgs e)
        {
            Core.PassHint(sender);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // 1. Все поля заполнены?
            if (string.IsNullOrWhiteSpace(TbLogin.Text) || OldPass.Password == "" ||
                NewPass.Password == "" || NewPass2.Password == "")
            {
                Core.Error("Все поля обязательны к заполнению!");
                return;
            }

            // 2. Логин и текущий пароль верные? (сравниваем хэши)
            Users user = Core.Login(TbLogin.Text.Trim(), OldPass.Password);
            if (user == null)
            {
                Core.Error("Текущий пароль или логин неверный!");
                return;
            }

            // 3. Новый пароль подходит по формату?
            string error = Core.CheckPassword(NewPass.Password);
            if (error != null)
            {
                Core.Error(error);
                return;
            }

            // 4. Новый пароль введён одинаково дважды и отличается от старого?
            if (NewPass.Password != NewPass2.Password)
            {
                Core.Error("Пароли не совпадают!");
                return;
            }
            if (NewPass.Password == OldPass.Password)
            {
                Core.Error("Новый пароль должен отличаться от текущего!");
                return;
            }

            // 5. Сохраняем хэш нового пароля
            user.Password = Core.GetHash(NewPass.Password);
            if (!Core.Save()) return;

            Core.Info("Пароль успешно изменён!");
            NavigationService.GoBack();
        }
    }
}
