using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Build.Pages
{
    public partial class MachineTabPage : Page
    {
        public MachineTabPage()
        {
            InitializeComponent();

            var categories = Core.Db.MachineCategories.ToList();
            categories.Insert(0, new MachineCategories { Id = 0, Name = "Все категории" });
            CategoryBox.ItemsSource = categories;
            CategoryBox.SelectedIndex = 0;

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
            CategoryBox.SelectedIndex = 0;
            StatusBox.SelectedIndex = 0;
            SortBox.SelectedIndex = 0;
        }

        private void UpdateGrid()
        {
            if (!IsInitialized || SortBox == null || MachineGrid == null) return;

            List<Machines> list = Core.Db.Machines.ToList();

            // Поиск
            string text = SearchBox.Text.Trim().ToLower();
            if (text != "")
                list = list.Where(m => m.Name.ToLower().Contains(text) || m.Model.ToLower().Contains(text) ||
                                       m.InventoryNumber.ToLower().Contains(text)).ToList();

            // Фильтр по категории
            var category = CategoryBox.SelectedItem as MachineCategories;
            if (category != null && category.Id != 0)
                list = list.Where(m => m.CategoryId == category.Id).ToList();

            // Фильтр по статусу (0 - "Все статусы")
            var status = StatusBox.SelectedItem as ComboBoxItem;
            if (StatusBox.SelectedIndex > 0 && status != null)
                list = list.Where(m => m.Status == status.Content.ToString()).ToList();

            // Сортировка
            if (SortBox.SelectedIndex == 0) list = list.OrderBy(m => m.Name).ToList();
            else if (SortBox.SelectedIndex == 1) list = list.OrderBy(m => m.DailyPrice).ToList();
            else list = list.OrderByDescending(m => m.DailyPrice).ToList();

            MachineGrid.ItemsSource = list;
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AddMachinePage(null));
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var machine = MachineGrid.SelectedItem as Machines;
            if (machine == null)
            {
                Core.Error("Выберите технику в таблице!");
                return;
            }
            NavigationService.Navigate(new AddMachinePage(machine));
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var selected = MachineGrid.SelectedItems.Cast<Machines>().ToList();
            if (selected.Count == 0)
            {
                Core.Error("Выберите записи для удаления!");
                return;
            }
            if (!Core.Ask("Вы точно хотите удалить записи в количестве " + selected.Count +
                          " шт.? Связанные с ними данные (позиции заявок, акты) тоже будут удалены."))
                return;

            Core.DeleteMachines(selected);
            if (Core.Save())
            {
                Core.Info("Данные успешно удалены!");
                UpdateGrid();
            }
        }
    }
}
