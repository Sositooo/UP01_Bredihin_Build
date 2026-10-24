using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace Build.Pages
{
    public partial class AddMachinePage : Page
    {
        // Редактируемая техника. Для новой - сразу ставим разумные значения по умолчанию
        private Machines _current = new Machines { Status = "Исправна", MinRentDays = 1 };

        public AddMachinePage(Machines selectedMachine)
        {
            InitializeComponent();

            // В поля с цифрами можно вводить только цифры
            TbYear.PreviewTextInput += Core.OnlyDigits;
            TbPrice.PreviewTextInput += Core.OnlyDigits;
            TbDeposit.PreviewTextInput += Core.OnlyDigits;
            TbDays.PreviewTextInput += Core.OnlyDigits;

            CategoryBox.ItemsSource = Core.Db.MachineCategories.ToList();

            if (selectedMachine != null)
            {
                _current = selectedMachine;
                Title = "Редактирование техники";
            }
            else
            {
                Title = "Добавление техники";
            }

            // Привязка полей формы к _current
            DataContext = _current;

            // У новой техники числовые поля показываем пустыми, чтобы виден был плейсхолдер
            if (selectedMachine == null)
            {
                TbPrice.Text = "";
                TbDeposit.Text = "";
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var errors = new StringBuilder();
            int id = _current.Id;

            // Текстовые поля
            string name = (_current.Name ?? "").Trim();
            string model = (_current.Model ?? "").Trim();
            string inventory = (_current.InventoryNumber ?? "").Trim().ToUpper();

            if (name == "") errors.AppendLine("Укажите название!");
            if (model == "") errors.AppendLine("Укажите модель!");

            if (!Regex.IsMatch(inventory, @"^[A-Z]{2}-\d{3}$"))
                errors.AppendLine("Инвентарный номер должен быть в формате EX-001!");
            else if (Core.Db.Machines.Any(m => m.InventoryNumber == inventory && m.Id != id))
                errors.AppendLine("Техника с таким инвентарным номером уже есть!");

            if (CategoryBox.SelectedValue == null) errors.AppendLine("Выберите категорию!");
            if (StatusBox.SelectedValue == null) errors.AppendLine("Выберите состояние!");

            // Ссылка на фото необязательна, но если указана - должна начинаться с http:// или https://
            string photo = (_current.PhotoUrl ?? "").Trim();
            if (photo != "" && !(photo.StartsWith("http://") || photo.StartsWith("https://")))
                errors.AppendLine("Ссылка на фото должна начинаться с http:// или https://!");

            // Числовые поля: TryParse превращает текст в число и сообщает, получилось ли.
            // Дальше проверяем допустимый диапазон (минимум и максимум).
            int? year = null;
            if (TbYear.Text.Trim() != "")
            {
                int y;
                if (!int.TryParse(TbYear.Text, out y) || y < 1980 || y > DateTime.Now.Year)
                    errors.AppendLine("Год выпуска: от 1980 до " + DateTime.Now.Year + "!");
                else year = y;
            }

            decimal price;
            if (!decimal.TryParse(TbPrice.Text, out price) || price < 100 || price > 1000000)
                errors.AppendLine("Цена за сутки: от 100 до 1 000 000 ₽!");

            decimal deposit;
            if (!decimal.TryParse(TbDeposit.Text, out deposit) || deposit < 0 || deposit > 5000000)
                errors.AppendLine("Залог: от 0 до 5 000 000 ₽!");

            int days;
            if (!int.TryParse(TbDays.Text, out days) || days < 1 || days > 365)
                errors.AppendLine("Минимальный срок аренды: от 1 до 365 дней!");

            if (errors.Length > 0)
            {
                Core.Error(errors.ToString());
                return;
            }

            // Всё верно - переносим значения в объект
            _current.Name = name;
            _current.Model = model;
            _current.InventoryNumber = inventory;
            _current.ManufactureYear = year;
            _current.DailyPrice = price;
            _current.DepositAmount = deposit;
            _current.MinRentDays = days;
            _current.PhotoUrl = photo == "" ? null : photo;

            if (_current.Id == 0) Core.Db.Machines.Add(_current);
            if (Core.Save())
            {
                Core.Info("Данные успешно сохранены!");
                NavigationService.GoBack();
            }
        }

        private void BtnClean_Click(object sender, RoutedEventArgs e)
        {
            TbName.Text = "";
            TbModel.Text = "";
            TbInventory.Text = "";
            TbYear.Text = "";
            TbPrice.Text = "";
            TbDeposit.Text = "";
            TbDays.Text = "";
            TbDescription.Text = "";
            TbPhoto.Text = "";
            CategoryBox.SelectedIndex = -1;
            StatusBox.SelectedIndex = -1;
        }
    }
}
