using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Build
{
    public partial class App : Application
    {
        // Какая тема сейчас включена: "Blue" (по Style Guide) или "Green" (собственная)
        public static string Theme = "Blue";

        // Файл, в котором запоминаем выбранную тему (переживает выход и перезапуск)
        static string ThemeFile
        {
            get
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Build");
                Directory.CreateDirectory(folder);
                return Path.Combine(folder, "theme.txt");
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // читаем сохранённую тему и применяем её ДО создания главного окна
            string saved = "Blue";
            try { if (File.Exists(ThemeFile)) saved = File.ReadAllText(ThemeFile).Trim(); } catch { }
            SetTheme(saved == "Green" ? "Green" : "Blue");
            base.OnStartup(e);
        }

        // Смена темы: подменяем словарь ресурсов приложения и запоминаем выбор
        public static void SetTheme(string theme)
        {
            var uri = new Uri(theme == "Green" ? "DictionaryGreen.xaml" : "Dictionary.xaml", UriKind.Relative);
            var dict = new ResourceDictionary { Source = uri };
            Current.Resources.MergedDictionaries.Clear();
            Current.Resources.MergedDictionaries.Add(dict);
            Theme = theme;
            try { File.WriteAllText(ThemeFile, theme); } catch { }
        }

        // Любая непойманная ошибка (например, нет связи с БД) - понятное сообщение вместо вылета
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Exception ex = e.Exception.InnerException ?? e.Exception;
            MessageBox.Show("Произошла ошибка:\n" + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Handled = true;
        }
    }
}
