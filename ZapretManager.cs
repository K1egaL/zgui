using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZGUI.Core
{
    public class ZapretManager
    {
        public string ZapretPath { get; private set; }
        public bool IsRunning { get; private set; }

        public event EventHandler<string> LogMessage;
        public event EventHandler<bool> StatusChanged;

        public ZapretManager()
        {
            DetectZapretPath();
        }

        private void DetectZapretPath()
        {
            var paths = new List<string>
            {
                @"C:\zapret",
                @"C:\Program Files\zapret",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "zapret"),
                Directory.GetCurrentDirectory()
            };

            foreach (var path in paths)
            {
                if (Directory.Exists(path) && File.Exists(Path.Combine(path, "service.bat")))
                {
                    ZapretPath = path;
                    Log($"Найден Zapret в: {path}");
                    return;
                }
            }

            throw new FileNotFoundException("Zapret не найден. Установите Zapret в одну из папок:\n" +
                                          "• C:\\zapret\n" +
                                          "• C:\\Program Files\\zapret\n" +
                                          "• %LocalAppData%\\zapret");
        }

        public async Task<bool> StartAsync(string mode)
        {
            if (IsRunning)
            {
                Log("Zapret уже запущен");
                return false;
            }

            try
            {
                var batPath = Path.Combine(ZapretPath, $"{mode}.bat");
                if (!File.Exists(batPath))
                    throw new FileNotFoundException($"Файл {mode}.bat не найден");

                await RunBatchAsync(batPath, "start");
                IsRunning = true;
                StatusChanged?.Invoke(this, true);
                Log($"Zapret запущен в режиме: {mode}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Ошибка запуска: {ex.Message}", true);
                return false;
            }
        }

        public async Task<bool> StopAsync()
        {
            if (!IsRunning)
            {
                Log("Zapret не запущен");
                return false;
            }

            try
            {
                await RunBatchAsync("service.bat", "stop");
                IsRunning = false;
                StatusChanged?.Invoke(this, false);
                Log("Zapret остановлен");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Ошибка остановки: {ex.Message}", true);
                return false;
            }
        }

        public async Task<bool> UpdateAsync()
        {
            try
            {
                Log("Обновление списков ipset...");
                await RunBatchAsync("update.bat");
                Log("Обновление выполнено");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Ошибка обновления: {ex.Message}", true);
                return false;
            }
        }

        public async Task<bool> TestConnectionAsync(string service, Action<string> resultCallback)
        {
            try
            {
                var urls = new Dictionary<string, string>
                {
                    { "youtube", "https://www.youtube.com" },
                    { "discord", "https://discord.com" }
                };

                if (!urls.ContainsKey(service.ToLower()))
                {
                    resultCallback?.Invoke($"Неизвестный сервис: {service}");
                    return false;
                }

                using var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(10),
                    DefaultRequestHeaders =
                    {
                        {"User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"}
                    }
                };

                var startTime = DateTime.Now;
                var response = await client.GetAsync(urls[service.ToLower()], HttpCompletionOption.ResponseHeadersRead);
                var endTime = DateTime.Now;
                var ping = (endTime - startTime).TotalMilliseconds;

                if (response.IsSuccessStatusCode)
                {
                    resultCallback?.Invoke($"{service} доступен. Пинг: {ping:F0}мс, Статус: {response.StatusCode}");
                    return true;
                }
                else
                {
                    resultCallback?.Invoke($"{service} недоступен. Статус: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                resultCallback?.Invoke($"Ошибка тестирования {service}: {ex.Message}");
                return false;
            }
        }

        private async Task RunBatchAsync(string batFile, string args = "")
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{Path.Combine(ZapretPath, batFile)} {args}\"",
                WorkingDirectory = ZapretPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Verb = "runas"
            };

            using var process = new Process { StartInfo = startInfo };

            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Log(e.Data);
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Log($"[ERROR] {e.Data}", true);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await Task.Run(() => process.WaitForExit(30000));

            if (process.ExitCode != 0)
                throw new Exception($"Код ошибки: {process.ExitCode}");
        }

        private void Log(string message, bool isError = false)
        {
            var prefix = isError ? "[ERROR] " : "[INFO] ";
            LogMessage?.Invoke(this, $"{prefix}{message}");
        }
    }
}