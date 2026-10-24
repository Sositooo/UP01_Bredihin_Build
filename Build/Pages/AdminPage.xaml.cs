using System.Windows;
using System.Windows.Controls;

namespace Build.Pages
{
    public partial class AdminPage : Page
    {
        public AdminPage()
        {
            InitializeComponent();
            WelcomeText.Text = "Добро пожаловать, " + Core.CurrentUser.FullName + "!";
        }

        // Каждая кнопка просто открывает свою страницу
        private void BtnUsers_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new UsersTabPage());
        }

        private void BtnCategories_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new CategoryTabPage());
        }

        private void BtnMachines_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new MachineTabPage());
        }

        private void BtnDiagram_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new DiagrammPage());
        }

        // У администратора есть и всё, что видит пользователь
        private void BtnCatalog_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new UserPage());
        }
    }
}
