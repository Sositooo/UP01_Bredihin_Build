using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.DataVisualization.Charting;

namespace Build.Pages
{
    public partial class DiagrammPage : Page
    {
        public DiagrammPage()
        {
            InitializeComponent();

            // Область построения и один набор данных для диаграммы
            ChartMachines.ChartAreas.Add(new ChartArea("Main"));
            ChartMachines.Series.Add(new Series("Показатель") { IsValueShownAsLabel = true, LabelFormat = "N0" });

            // Что показывать
            MetricBox.ItemsSource = new[]
            {
                "Количество техники по категориям",
                "Средняя цена за сутки по категориям, ₽"
            };

            // Типы диаграмм: название для списка -> тип из библиотеки
            TypeBox.ItemsSource = new Dictionary<string, SeriesChartType>
            {
                { "Столбцы", SeriesChartType.Column },
                { "Полосы", SeriesChartType.Bar },
                { "Круговая", SeriesChartType.Pie },
                { "Кольцевая", SeriesChartType.Doughnut },
                { "Линия", SeriesChartType.Line },
                { "Область", SeriesChartType.Area }
            };
            TypeBox.DisplayMemberPath = "Key";
            TypeBox.SelectedValuePath = "Value";

            MetricBox.SelectedIndex = 0;
            TypeBox.SelectedIndex = 0;     // здесь сработает UpdateChart и диаграмма нарисуется
        }

        // Перерисовка диаграммы при выборе в любом из двух списков
        private void UpdateChart(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized || MetricBox == null || TypeBox == null || ChartMachines == null) return;
            if (MetricBox.SelectedIndex < 0 || TypeBox.SelectedValue == null) return;

            var type = (SeriesChartType)TypeBox.SelectedValue;
            bool round = type == SeriesChartType.Pie || type == SeriesChartType.Doughnut;

            Series series = ChartMachines.Series.First();
            series.ChartType = type;
            series.Points.Clear();

            // Цвет серии = основной цвет текущей темы (PrimaryBrush из словаря).
            // У круговой диаграммы цвета автоматические
            var main = ((System.Windows.Media.SolidColorBrush)FindResource("PrimaryBrush")).Color;
            series.Color = round ? System.Drawing.Color.Empty
                                 : System.Drawing.Color.FromArgb(main.R, main.G, main.B);
            ChartMachines.Legends[0].Enabled = round;

            var machines = Core.Db.Machines.ToList();
            foreach (var category in Core.Db.MachineCategories.ToList())
            {
                var list = machines.Where(m => m.CategoryId == category.Id).ToList();
                double value;
                if (MetricBox.SelectedIndex == 0) value = list.Count;
                else value = list.Count == 0 ? 0 : (double)list.Average(m => m.DailyPrice);
                series.Points.AddXY(category.Name, value);
            }

            ChartMachines.Titles.Clear();
            ChartMachines.Titles.Add(MetricBox.SelectedItem.ToString());
        }


        // ЭКСПОРТ В WORD (.docx и .pdf)
        private void ExportWord_Click(object sender, RoutedEventArgs e)
        {
            dynamic word = null;
            try
            {
                var machines = Core.Db.Machines.ToList();
                var categories = Core.Db.MachineCategories.ToList();

                word = Activator.CreateInstance(Type.GetTypeFromProgID("Word.Application"));
                word.DisplayAlerts = 0;
                dynamic doc = word.Documents.Add();
                dynamic sel = word.Selection;          // Selection - "курсор", в него печатаем текст

                // Заголовок документа
                sel.ParagraphFormat.Alignment = 1;     // 1 = по центру
                sel.Font.Name = "Times New Roman";
                sel.Font.Size = 20;
                sel.Font.Bold = 1;
                sel.TypeText("Отчёт по технике сервиса Build");
                sel.TypeParagraph();
                sel.Font.Size = 12;
                sel.Font.Bold = 0;
                sel.TypeText("Дата формирования: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
                sel.TypeParagraph();
                sel.TypeParagraph();
                sel.ParagraphFormat.Alignment = 0;     // 0 = по левому краю

                // Для каждой категории - название, таблица техники, самая дорогая и самая дешёвая
                foreach (var category in categories)
                {
                    var list = machines.Where(m => m.CategoryId == category.Id).OrderBy(m => m.Name).ToList();
                    if (list.Count == 0) continue;

                    sel.Font.Size = 14;
                    sel.Font.Bold = 1;
                    sel.TypeText(category.Name);
                    sel.TypeParagraph();
                    sel.Font.Size = 11;
                    sel.Font.Bold = 0;

                    // Таблица: строк = техника + строка заголовков, столбцов = 5
                    dynamic table = doc.Tables.Add(sel.Range, list.Count + 1, 5);
                    table.Borders.Enable = 1;
                    string[] heads = { "Название", "Модель", "Инв. №", "Цена за сутки", "Состояние" };
                    for (int c = 0; c < heads.Length; c++)
                    {
                        table.Cell(1, c + 1).Range.Text = heads[c];
                        table.Cell(1, c + 1).Range.Font.Bold = 1;
                    }
                    for (int i = 0; i < list.Count; i++)
                    {
                        table.Cell(i + 2, 1).Range.Text = list[i].Name;
                        table.Cell(i + 2, 2).Range.Text = list[i].Model;
                        table.Cell(i + 2, 3).Range.Text = list[i].InventoryNumber;
                        table.Cell(i + 2, 4).Range.Text = list[i].DailyPrice.ToString("N0") + " руб.";
                        table.Cell(i + 2, 5).Range.Text = list[i].Status;
                    }

                    sel.EndKey(6);                     // 6 = в конец документа (после таблицы)
                    sel.TypeParagraph();

                    var max = list.OrderByDescending(m => m.DailyPrice).First();
                    var min = list.OrderBy(m => m.DailyPrice).First();
                    sel.Font.Color = 192;              // тёмно-красный
                    sel.TypeText("Самая дорогая: " + max.Name + " " + max.Model + " — " + max.DailyPrice.ToString("N0") + " руб./сутки");
                    sel.TypeParagraph();
                    sel.Font.Color = 32768;            // зелёный
                    sel.TypeText("Самая дешёвая: " + min.Name + " " + min.Model + " — " + min.DailyPrice.ToString("N0") + " руб./сутки");
                    sel.TypeParagraph();
                    sel.Font.Color = 0;                // обычный чёрный
                    sel.TypeParagraph();
                }

                // Колонтитулы: сверху дата, снизу номер страницы
                dynamic section = doc.Sections.Item(1);
                section.Headers.Item(1).Range.Text = "Build · " + DateTime.Now.ToString("dd.MM.yyyy");
                section.Footers.Item(1).PageNumbers.Add(1);

                // Сохраняем на рабочий стол в .docx и .pdf
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                                           "Build_Report_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                doc.SaveAs2(path + ".docx");
                doc.SaveAs2(path + ".pdf", 17);        // 17 = формат PDF
                word.Visible = true;

                Core.Info("Отчёт сохранён на рабочем столе:\n" + path + ".docx\n" + path + ".pdf");
            }
            catch (Exception ex)
            {
                try { if (word != null) word.Quit(0); } catch { }
                Core.Error("Не удалось создать отчёт. Проверьте, что Microsoft Word установлен.\n" + ex.Message);
            }
        }

        // ЭКСПОРТ В EXCEL (.xlsx)
        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            dynamic excel = null;
            try
            {
                var machines = Core.Db.Machines.ToList().OrderBy(m => m.CategoryName).ThenBy(m => m.Name).ToList();

                excel = Activator.CreateInstance(Type.GetTypeFromProgID("Excel.Application"));
                excel.DisplayAlerts = false;
                dynamic book = excel.Workbooks.Add();
                dynamic sheet = book.Worksheets.Item(1);
                sheet.Name = "Техника";

                // Строка заголовков
                string[] heads = { "Название", "Модель", "Инв. №", "Категория", "Год выпуска",
                                   "Цена за сутки, ₽", "Залог, ₽", "Мин. срок, дн.", "Состояние" };
                for (int c = 0; c < heads.Length; c++)
                {
                    sheet.Cells[1, c + 1] = heads[c];
                    sheet.Cells[1, c + 1].Font.Bold = true;
                }

                // Данные: одна строка = одна единица техники
                for (int i = 0; i < machines.Count; i++)
                {
                    Machines m = machines[i];
                    sheet.Cells[i + 2, 1] = m.Name;
                    sheet.Cells[i + 2, 2] = m.Model;
                    sheet.Cells[i + 2, 3] = m.InventoryNumber;
                    sheet.Cells[i + 2, 4] = m.CategoryName;
                    sheet.Cells[i + 2, 5] = m.ManufactureYear.HasValue ? m.ManufactureYear.Value.ToString() : "";
                    sheet.Cells[i + 2, 6] = (double)m.DailyPrice;
                    sheet.Cells[i + 2, 7] = (double)m.DepositAmount;
                    sheet.Cells[i + 2, 8] = m.MinRentDays;
                    sheet.Cells[i + 2, 9] = m.Status;
                }
                sheet.Columns.AutoFit();               // ширина столбцов по содержимому

                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                                           "Build_Machines_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx");
                book.SaveAs(path);
                excel.Visible = true;

                Core.Info("Таблица сохранена на рабочем столе:\n" + path);
            }
            catch (Exception ex)
            {
                try { if (excel != null) excel.Quit(); } catch { }
                Core.Error("Не удалось создать таблицу. Проверьте, что Microsoft Excel установлен.\n" + ex.Message);
            }
        }
    }
}
