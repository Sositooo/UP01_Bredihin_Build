using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Build.Pages
{
    public partial class CategoryTabPage : Page
    {
        public CategoryTabPage()
        {
            InitializeComponent();
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
            SortBox.SelectedIndex = 0;
        }

        private void UpdateGrid()
        {
            if (!IsInitialized || SortBox == null || CategoryGrid == null) return;

            List<MachineCategories> list = Core.Db.MachineCategories.ToList();

            string text = SearchBox.Text.Trim().ToLower();
            if (text != "")
                list = list.Where(c => c.Name.ToLower().Contains(text)).ToList();

            if (SortBox.SelectedIndex == 0) list = list.OrderBy(c => c.Name).ToList();
            else list = list.OrderByDescending(c => c.Name).ToList();

            CategoryGrid.ItemsSource = list;
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AddCategoryPage(null));
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var category = CategoryGrid.SelectedItem as MachineCategories;
            if (category == null)
            {
                Core.Error("Выберите категорию в таблице!");
                return;
            }
            NavigationService.Navigate(new AddCategoryPage(category));
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var selected = CategoryGrid.SelectedItems.Cast<MachineCategories>().ToList();
            if (selected.Count == 0)
            {
                Core.Error("Выберите записи для удаления!");
                return;
            }
            if (!Core.Ask("Вы точно хотите удалить записи в количестве " + selected.Count +
                          " шт.? Вся техника этих категорий и связанные с ней данные тоже будут удалены."))
                return;

            Core.DeleteCategories(selected);
            if (Core.Save())
            {
                Core.Info("Данные успешно удалены!");
                UpdateGrid();
            }
        }
    }
}
