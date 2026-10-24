using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Build.Pages
{
    public partial class AddCategoryPage : Page
    {
        private MachineCategories _current = new MachineCategories();

        public AddCategoryPage(MachineCategories selectedCategory)
        {
            InitializeComponent();

            if (selectedCategory != null)
            {
                _current = selectedCategory;
                Title = "Редактирование категории";
            }
            else
            {
                Title = "Добавление категории";
            }
            DataContext = _current;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var errors = new StringBuilder();
            string name = (_current.Name ?? "").Trim();
            int id = _current.Id;

            if (name == "") errors.AppendLine("Укажите название категории!");
            else if (Core.Db.MachineCategories.Any(c => c.Name == name && c.Id != id))
                errors.AppendLine("Категория с таким названием уже существует!");

            if (errors.Length > 0)
            {
                Core.Error(errors.ToString());
                return;
            }

            _current.Name = name;
            if (_current.Id == 0) Core.Db.MachineCategories.Add(_current);
            if (Core.Save())
            {
                Core.Info("Данные успешно сохранены!");
                NavigationService.GoBack();
            }
        }

        private void BtnClean_Click(object sender, RoutedEventArgs e)
        {
            TbName.Text = "";
            TbDescription.Text = "";
        }
    }
}
