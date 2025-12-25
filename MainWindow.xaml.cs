using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using ZGUI.Core;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ZGUI
{
    public partial class MainWindow : Window
    {
        private ZapretManager _zapretManager;
        private DispatcherTimer _statusTimer;
        private bool _isTesting;

        public MainWindow()
        {
            InitializeComponent();

            
            if (!IsRunningAsAdmin())
            {
                MessageBox.Show("Для работы ZGUI требуются права администратора. Перезапустите программу от имени администратора.",
                    "Требуются права администратора", MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
                return;
            }

            InitializeApplication();
        }

        private bool IsRunningAsAdmin()
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        private void InitializeApplication()
        {
            try
            {
                _zapretManager = new ZapretManager();
                _zapretManager.LogMessage += OnLogMessage;
                _zapretManager.StatusChanged += OnStatusChanged;

                InfoTextBox.Text = $"Zapret найден: {_zapretManager.ZapretPath}\n\n" +
                                 $"ZGUI готов к работе.\n" +
                                 $"Выберите режим и нажмите 'Запуск'.\n\n" +
                                 $"Доступные режимы:\n" +
                                 $"• general - для YouTube и Discord\n" +
                                 $"• general_alt - альтернативный режим\n" +
                                 $"• discord - только для Discord";

                AppendLog("ZGUI инициализирован");
                AppendLog($"Zapret найден: {_zapretManager.ZapretPath}");

                // Таймер для проверки статуса
                _statusTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };
                _statusTimer.Tick += StatusTimer_Tick;
                _statusTimer.Start();

                Title = $"ZGUI - Zapret Management [{_zapretManager.ZapretPath}]";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации: {ex.Message}\n\n" +
                              "Установите Zapret в одну из папок:\n" +
                              "• C:\\zapret\n" +
                              "• C:\\Program Files\\zapret\n" +
                              "• %LocalAppData%\\zapret\n\n" +
                              "Скачайте Zapret с: https://github.com/bol-van/zapret",
                    "Zapret не найден", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void OnLogMessage(object sender, string message)
        {
            Dispatcher.Invoke(() => AppendLog(message));
        }

        private void OnStatusChanged(object sender, bool isRunning)
        {
            Dispatcher.Invoke(() =>
            {
                StartButton.IsEnabled = !isRunning;
                StopButton.IsEnabled = isRunning;

                if (isRunning)
                {
                    StartButton.Background = System.Windows.Media.Brushes.LightGray;
                    StopButton.Background = System.Windows.Media.Brushes.Red;
                    AppendLog("Статус: Zapret запущен");
                }
                else
                {
                    StartButton.Background = System.Windows.Media.Brushes.Green;
                    StopButton.Background = System.Windows.Media.Brushes.LightGray;
                    AppendLog("Статус: Zapret остановлен");
                }
            });
        }

        private void AppendLog(string message)
        {
            LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
            LogTextBox.ScrollToEnd();

            
            TestLogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
            TestLogTextBox.ScrollToEnd();
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string mode = ModeComboBox.SelectedIndex switch
                {
                    0 => "general",
                    1 => "general_alt",
                    2 => "discord",
                    _ => "general"
                };

                AppendLog($"Запуск Zapret в режиме: {mode}");
                InfoTextBox.Text = $"Запуск Zapret в режиме: {mode}\nПожалуйста, подождите...";

                var success = await _zapretManager.StartAsync(mode);
                if (success)
                {
                    InfoTextBox.Text = $"Zapret успешно запущен в режиме: {mode}\n\n" +
                                     $"Сейчас активен режим обхода блокировок.\n" +
                                     $"Вы можете протестировать доступ к YouTube и Discord.";
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Ошибка запуска: {ex.Message}");
                InfoTextBox.Text = $"Ошибка запуска: {ex.Message}";
            }
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AppendLog("Остановка Zapret...");
                InfoTextBox.Text = "Остановка Zapret...\nПожалуйста, подождите...";

                var success = await _zapretManager.StopAsync();
                if (success)
                {
                    InfoTextBox.Text = "Zapret остановлен.\n\n" +
                                     "Для запуска выберите режим и нажмите 'Запуск'.";
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Ошибка остановки: {ex.Message}");
                InfoTextBox.Text = $"Ошибка остановки: {ex.Message}";
            }
        }

        private async void ApplyConfigButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AppendLog("Применение конфигурации...");

                
                var ipsetMode = IpsetComboBox.SelectedIndex switch
                {
                    0 => "none",
                    1 => "loaded",
                    2 => "any",
                    _ => "loaded"
                };

                var gameFilter = GameFilterCheckBox.IsChecked == true ? "1" : "0";

                AppendLog($"Применены настройки: ipset={ipsetMode}, game_filter={gameFilter}");

                
                var configPath = Path.Combine(_zapretManager.ZapretPath, "config");
                if (File.Exists(configPath))
                {
                    var lines = new[]
                    {
                        $"IPSET={ipsetMode}",
                        $"GAME_FILTER={gameFilter}",
                        "# Конфигурация применена через ZGUI"
                    };

                    await File.WriteAllLinesAsync(configPath, lines);
                    AppendLog("Конфигурация сохранена в config файл");

                    MessageBox.Show("Конфигурация успешно применена.\n" +
                                  "Для применения изменений может потребоваться перезапуск Zapret.",
                                  "Конфигурация применена", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Ошибка применения конфигурации: {ex.Message}");
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void TestYouTubeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isTesting) return;
            _isTesting = true;

            try
            {
                TestResultBorder.Visibility = Visibility.Visible;
                TestProgressBar.Visibility = Visibility.Visible;
                TestResultText.Text = "Тестирование YouTube...";

                bool success = await _zapretManager.TestConnectionAsync("youtube", (result) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        TestResultText.Text = result;
                        TestProgressBar.Visibility = Visibility.Collapsed;
                    });
                });

                TestResultBorder.Background = success
                    ? System.Windows.Media.Brushes.LightGreen
                    : System.Windows.Media.Brushes.LightCoral;
            }
            finally
            {
                _isTesting = false;
            }
        }

        private async void TestDiscordButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isTesting) return;
            _isTesting = true;

            try
            {
                TestResultBorder.Visibility = Visibility.Visible;
                TestProgressBar.Visibility = Visibility.Visible;
                TestResultText.Text = "Тестирование Discord...";

                bool success = await _zapretManager.TestConnectionAsync("discord", (result) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        TestResultText.Text = result;
                        TestProgressBar.Visibility = Visibility.Collapsed;
                    });
                });

                TestResultBorder.Background = success
                    ? System.Windows.Media.Brushes.LightGreen
                    : System.Windows.Media.Brushes.LightCoral;
            }
            finally
            {
                _isTesting = false;
            }
        }

        private async void DiagnosticButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AppendLog("Запуск диагностики...");
                await _zapretManager.UpdateAsync();
                AppendLog("Диагностика завершена");

                MessageBox.Show("Диагностика завершена. Проверьте логи для подробной информации.",
                              "Диагностика", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppendLog($"Ошибка диагностики: {ex.Message}");
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка диагностики",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AppendLog("Обновление списков ipset...");
                await _zapretManager.UpdateAsync();
                AppendLog("Обновление завершено");
            }
            catch (Exception ex)
            {
                AppendLog($"Ошибка обновления: {ex.Message}");
            }
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
            AppendLog("Логи очищены");
        }

        private void SaveLogButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Log files (*.log)|*.log|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"zapret_log_{DateTime.Now:yyyyMMdd_HHmmss}",
                DefaultExt = ".log"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(dialog.FileName, LogTextBox.Text);
                    AppendLog($"Лог сохранен: {dialog.FileName}");
                    MessageBox.Show($"Логи сохранены в файл:\n{dialog.FileName}",
                                  "Логи сохранены", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка сохранения: {ex.Message}",
                                  "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            // Простая проверка статуса - можно расширить при необходимости
            if (_zapretManager != null)
            {
                // Обновляем заголовок с текущим статусом
                Title = $"ZGUI - Zapret Management [{_zapretManager.ZapretPath}] - " +
                       $"{(_zapretManager.IsRunning ? "Запущен" : "Остановлен")}";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _statusTimer?.Stop();
            if (_zapretManager != null && _zapretManager.IsRunning)
            {
                var result = MessageBox.Show("Zapret все еще запущен. Остановить перед выходом?",
                                           "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _zapretManager.StopAsync().Wait(5000);
                }
            }

            base.OnClosed(e);
        }
    }
}