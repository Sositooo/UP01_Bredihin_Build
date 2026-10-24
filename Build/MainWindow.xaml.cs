using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Threading;
using Build.Pages;

namespace Build
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // первая страница приложения - авторизация
            MainFrame.Navigate(new AuthPage());
        }

        // Часы в нижней панели: таймер раз в секунду обновляет текст
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            DateTimeNow.Text = DateTime.Now.ToString();
            var timer = new DispatcherTimer();
            timer.Interval = new TimeSpan(0, 0, 1);
            timer.Tick += (o, t) => { DateTimeNow.Text = DateTime.Now.ToString(); };
            timer.Start();
        }

        // Срабатывает после каждого перехода между страницами
        private void MainFrame_Navigated(object sender, NavigationEventArgs e)
        {
            var page = e.Content as Page;
            if (page == null) return;

            
            PageTitle.Text = page.Title;
            Title = "Build — " + page.Title;

            // Страницы с Tag="root" - начальные для своей роли, назад с них идти некуда
            bool root = page.Tag != null && page.Tag.ToString() == "root";
            BackButton.Visibility = (MainFrame.CanGoBack && !root) ? Visibility.Visible : Visibility.Collapsed;

            // Кнопка "Выйти" и подпись внизу - только когда пользователь вошёл
            bool logged = Core.CurrentUser != null;
            LogoutButton.Visibility = logged ? Visibility.Visible : Visibility.Collapsed;
            UserText.Text = logged
                ? "👤 " + Core.CurrentUser.FullName + " · " + Core.CurrentUser.RoleName
                : "";
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainFrame.CanGoBack) MainFrame.GoBack();
        }

        // Смена темы: переключаем между двумя словарями стилей
        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            App.SetTheme(App.Theme == "Blue" ? "Green" : "Blue");
        }

        // Выход из учётной записи с подтверждением
        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Core.Ask("Вы действительно хотите выйти?")) return;
            Core.CurrentUser = null;
            MainFrame.Navigate(new AuthPage());
        }

        // Закрытие окна с подтверждением
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (MessageBox.Show("Вы действительно хотите выйти из приложения?", "Build",
                                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                e.Cancel = true;
        }
    }
}
