using System.Windows;
using System.Windows.Controls;

namespace Build.Pages
{
    public partial class AuthPage : Page
    {
        // Счётчик неверных попыток входа. После 3-й показываем капчу
        int failedAttempts = 0;

        public AuthPage()
        {
            InitializeComponent();
            TbLogin.PreviewTextInput += Core.OnlyLatin;   // в логин нельзя ввести русские буквы
        }

        private void Pass_Changed(object sender, RoutedEventArgs e)
        {
            Core.PassHint(sender);
        }

        // ВХОД 
        private void BtnEnter_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TbLogin.Text) || PassBox.Password == "")
            {
                Core.Error("Введите логин и пароль!");
                return;
            }

            Users user = Core.Login(TbLogin.Text.Trim(), PassBox.Password);
            if (user == null)
            {
                failedAttempts++;
                Core.Error("Пользователь с такими данными не найден!");
                if (failedAttempts >= 3) ShowCaptcha(true);
                return;
            }

            failedAttempts = 0;
            Core.CurrentUser = user;
            // Роль определяет, какую страницу открыть
            if (Core.IsAdmin) NavigationService.Navigate(new AdminPage());
            else NavigationService.Navigate(new UserPage());
        }

        private void BtnReg_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new RegPage());
        }

        private void BtnChange_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new ChangePassPage());
        }

        //  КАПЧА 
        // true - показать капчу вместо формы входа, false - вернуть форму входа
        private void ShowCaptcha(bool show)
        {
            CaptchaPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            LoginPanel.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            if (show)
            {
                CaptchaText.Text = Core.NewCaptcha();
                TbCaptcha.Clear();
            }
            else
            {
                TbLogin.Clear();
                PassBox.Clear();
            }
        }

        private void BtnCaptchaOk_Click(object sender, RoutedEventArgs e)
        {
            if (TbCaptcha.Text != CaptchaText.Text)
            {
                Core.Error("Неверно введена капча!");
                CaptchaText.Text = Core.NewCaptcha();   // новая капча
                TbCaptcha.Clear();
                return;
            }
            Core.Info("Капча введена верно, можете продолжить вход.");
            failedAttempts = 0;
            ShowCaptcha(false);
        }

        private void BtnCaptchaNew_Click(object sender, RoutedEventArgs e)
        {
            CaptchaText.Text = Core.NewCaptcha();
            TbCaptcha.Clear();
        }
    }
}
