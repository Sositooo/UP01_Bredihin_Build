using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Build.Pages
{
    public partial class UserPage : Page
    {
        public UserPage()
        {
            InitializeComponent();

            // Для обычного пользователя это начальная страница (кнопки "Назад" нет).
            // Администратор попадает сюда из своего меню и может вернуться назад.
            if (!Core.IsAdmin) Tag = "root";

            // Список категорий для фильтра + пункт "Все категории" (Id = 0)
            var categories = Core.Db.MachineCategories.ToList();
            categories.Insert(0, new MachineCategories { Id = 0, Name = "Все категории" });
            CategoryBox.ItemsSource = categories;
            CategoryBox.SelectedIndex = 0;

            UpdateList();
        }

        // Один обработчик на все элементы фильтра: поиск, категория, сортировка, галочка
        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            UpdateList();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            CategoryBox.SelectedIndex = 0;
            SortBox.SelectedIndex = 0;
            OnlyOkCheck.IsChecked = false;
        }

        private void UpdateList()
        {
            // Пока страница строится, элементы ещё не созданы
            if (!IsInitialized || SortBox == null || CountText == null) return;

            List<Machines> list = Core.Db.Machines.ToList();

            // Поиск по названию или модели
            string text = SearchBox.Text.Trim().ToLower();
            if (text != "")
                list = list.Where(m => m.Name.ToLower().Contains(text) || m.Model.ToLower().Contains(text)).ToList();

            // Фильтр по категории
            var category = CategoryBox.SelectedItem as MachineCategories;
            if (category != null && category.Id != 0)
                list = list.Where(m => m.CategoryId == category.Id).ToList();

            // Только исправные
            if (OnlyOkCheck.IsChecked == true)
                list = list.Where(m => m.Status == "Исправна").ToList();

            // Сортировка
            if (SortBox.SelectedIndex == 0) list = list.OrderBy(m => m.DailyPrice).ToList();
            else if (SortBox.SelectedIndex == 1) list = list.OrderByDescending(m => m.DailyPrice).ToList();
            else list = list.OrderBy(m => m.Name).ToList();

            MachinesList.ItemsSource = list;
            CountText.Text = "Найдено: " + list.Count;
            EmptyText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
