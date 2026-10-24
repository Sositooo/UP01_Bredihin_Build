using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Build.Pages
{
    public partial class UsersTabPage : Page
    {
        public UsersTabPage()
        {
            InitializeComponent();

            // Фильтр по роли: все роли из БД + пункт "Все роли" (Id = 0)
            var roles = Core.Db.Roles.ToList();
            roles.Insert(0, new Roles { Id = 0, Name = "Все роли" });
            RoleFilter.ItemsSource = roles;
            RoleFilter.SelectedIndex = 0;

            // Когда страница снова становится видимой (вернулись с формы) - обновляем данные
            IsVisibleChanged += Page_IsVisibleChanged;
            UpdateGrid();
        }

        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                Core.Refresh();
                UpdateGrid();
            }
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            UpdateGrid();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            RoleFilter.SelectedIndex = 0;
            SortBox.SelectedIndex = 0;
        }

        private void UpdateGrid()
        {
            if (!IsInitialized || SortBox == null || UsersGrid == null) return;

            List<Users> list = Core.Db.Users.ToList();

            string text = SearchBox.Text.Trim().ToLower();
            if (text != "")
                list = list.Where(u => u.FullName.ToLower().Contains(text) || u.Login.ToLower().Contains(text)).ToList();

            var role = RoleFilter.SelectedItem as Roles;
            if (role != null && role.Id != 0)
                list = list.Where(u => u.RoleId == role.Id).ToList();

            if (SortBox.SelectedIndex == 0) list = list.OrderBy(u => u.FullName).ToList();
            else list = list.OrderByDescending(u => u.FullName).ToList();

            UsersGrid.ItemsSource = list;
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AddUserPage(null));   // null = создаём нового
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var user = UsersGrid.SelectedItem as Users;
            if (user == null)
            {
                Core.Error("Выберите пользователя в таблице!");
                return;
            }
            NavigationService.Navigate(new AddUserPage(user));   // передаём выбранного
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var selected = UsersGrid.SelectedItems.Cast<Users>().ToList();
            if (selected.Count == 0)
            {
                Core.Error("Выберите записи для удаления!");
                return;
            }
            if (selected.Any(u => u.Id == Core.CurrentUser.Id))
            {
                Core.Error("Нельзя удалить свою учётную запись!");
                return;
            }
            if (!Core.Ask("Вы точно хотите удалить записи в количестве " + selected.Count +
                          " шт.? Связанные с ними данные тоже будут удалены."))
                return;

            Core.DeleteUsers(selected);
            if (Core.Save())
            {
                Core.Info("Данные успешно удалены!");
                UpdateGrid();
            }
        }
    }
}
