using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Newtonsoft.Json.Linq;

namespace TelegramBotConsoleApp
{
    class Program
    {
        static TelegramBotClient bot;
        static string token;
        const string helpText = "/start - Начало работы с ботом\n/help - " +            //Список команд
            "Список команд\n/get_files - Список файлов\n/load - " +
            "Загрузить файл (после команды через пробел укажите имя файла)\n" +
            "/update_weather - Обновляет сведения о погоде.";
        static HttpClient client = new HttpClient();                                    //Создаём HTTP-клиент для взаимодействия с сервисом погоды Yandex

        static void Main(string[] args)
        {
            token = System.IO.File.ReadAllText("token.txt");                            //Получаем токен из файла

            client.DefaultRequestHeaders.Add("X-Yandex-API-Key",                        //Получаем ключ Yandex.Weather
                System.IO.File.ReadAllText("yandexWeatherKey.txt"));


            bot = new TelegramBotClient(token);
            var cts = new CancellationTokenSource();
            var receiverOptions = new ReceiverOptions()
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            };

            bot.StartReceiving(
                updateHandler: HandleUpdateAsync,
                pollingErrorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: cts.Token
                );

            Console.WriteLine($"Телеграм-бот запущен.");

            Console.ReadKey();
            cts.Cancel();
        }

        /// <summary>
        /// Обрабатывает поступающие сообщения
        /// </summary>
        /// <param name="botClient"></param>
        /// <param name="update"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.Message)
            {
                var message = update.Message;

                Console.WriteLine($"Тип сообщения: {message.Type}\nID: {message.From.Id}\nПользователь: {message.From.FirstName}\n");

                switch (message.Type)
                {
                    case MessageType.Text:
                        TextMessageHandler(message);
                        break;

                    case MessageType.Audio:
                        SaveAudio(message);
                        break;

                    case MessageType.Voice:
                        SaveVoiceAsync(message);
                        break;

                    case MessageType.Document:
                        SaveDocument(message);
                        break;

                    case MessageType.Photo:
                        SavePhotoAsync(message);
                        break;
                }
            }

        }

        /// <summary>
        /// Обрабатывает ошибки
        /// </summary>
        /// <param name="botClient"></param>
        /// <param name="exception"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine(exception.Message);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Обрабатывает текстовые сообщения
        /// </summary>
        /// <param name="message"></param>
        private static void TextMessageHandler(Message message)
        {
            if (message.Text[0] == '/') HandlerCommand(message);
            else Console.WriteLine(message.Text);
        }

        /// <summary>
        /// Обрабатывает команды пользователя
        /// </summary>
        /// <param name="message"></param>
        private static void HandlerCommand(Message message)
        {
            int firstSpaseIndex = message.Text.IndexOf(' ');
            string cmd = firstSpaseIndex > 0 ? message.Text.Substring(0, message.Text.IndexOf(' ')) : message.Text;
            string fileName;

            switch (cmd)
            {
                case "/start":
                    _ = SendStartTextAsync(message);
                    _ = GetWeatherInfoAsync(message.From.Id);
                    break;

                case "/get_files":
                    SendFileList(message);
                    break;

                case "/load":
                    fileName = message.Text.Remove(0, message.Text.IndexOf(' ') + 1);
                    UploadFileAsync(fileName, message);
                    break;

                case "/help":
                    _ = SendText(message.From.Id, helpText);
                    break;

                case "/update_weather":
                    _ = GetWeatherInfoAsync(message.From.Id);
                    break;
            }
        }

        /// <summary>
        /// Сохраняет полученные файлы
        /// </summary>
        /// <param name="message"></param>
        private static async void SaveFile(string fileName, string fileId, long chatId)
        {
            string directoryPath = System.IO.Directory.CreateDirectory($"files\\{chatId}").FullName;   //Создаём папку и получаем её путь

            Telegram.Bot.Types.File file = await bot.GetFileAsync(fileId);
            System.IO.FileStream fs = new System.IO.FileStream($"{directoryPath}\\{fileName}", System.IO.FileMode.Create);
            await bot.DownloadFileAsync(file.FilePath, fs);
            fs.Close();
            fs.Dispose();

            await bot.SendTextMessageAsync(chatId, "OK. Ваш файл успешно сохранён.\nЧтобы получить список ваших файлов введите команду - /get_files");
        }

        /// <summary>
        /// Отправляет пользователю список файлов
        /// </summary>
        /// <param name="message"></param>
        private static void SendFileList(Message message)
        {
            string path = $"files\\{message.Chat.Id}";
            StringBuilder sb = new StringBuilder();

            if (!Directory.Exists(path)) bot.SendTextMessageAsync(message.Chat.Id, "У вас пока нет сохранённых файлов.");
            else
            {
                FileInfo[] files = Directory.CreateDirectory(path).GetFiles();
                for (int i = 0; i < files.Length; i++)
                {
                    sb.Append($"{i + 1}. {files[i].Name}");
                    if (i < files.Length - 1) sb.Append('\n');
                }
            }

            bot.SendTextMessageAsync(message.Chat.Id, $"Список файлов:\n\n{sb.ToString()}");
        }

        /// <summary>
        /// Сохраняет аудио-файл на диск
        /// </summary>
        /// <param name="message"></param>
        private static void SaveAudio(Message message)
        {
            SaveFile(message.Audio.FileName, message.Audio.FileId, message.From.Id);
        }

        /// <summary>
        /// Сохраняет файл типа Document на диск
        /// </summary>
        /// <param name="message"></param>
        private static void SaveDocument(Message message)
        {
            SaveFile(message.Document.FileName, message.Document.FileId, message.From.Id);
        }

        /// <summary>
        /// Сохраняет голосовое сообщение на диск
        /// </summary>
        /// <param name="message"></param>
        private static async void SaveVoiceAsync(Message message)
        {
            //SaveFile(CreateVoiceFileName(message.From.Id), message.Voice.FileId, message.From.Id);

            Telegram.Bot.Types.File file = await bot.GetFileAsync(message.Voice.FileId);
            string fileName = file.FilePath.Split('/')[1];
            SaveFile(fileName, file.FileId, message.From.Id);
        }

        /// <summary>
        /// Сохраняет фотографию на диск
        /// </summary>
        /// <param name="message"></param>
        private static async void SavePhotoAsync(Message message)
        {
            Telegram.Bot.Types.File file = await bot.GetFileAsync(message.Photo[message.Photo.Length - 1].FileId);
            string fileName = file.FilePath.Split('/')[1];
            SaveFile(fileName, file.FileId, message.From.Id);
        }

        /// <summary>
        /// Определяет тип файла по его расширению и отправляет его пользователю
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="message"></param>
        private static async void UploadFileAsync(string fileName, Message message)
        {
            string filePath = $"files\\{message.From.Id}\\{fileName}";                      //Формируем путь к файлу
            string fileNameExtention = fileName.Substring(fileName.LastIndexOf('.') + 1);   //Получаем расширение имени файла

            //Отправляем файл пользователю
            if (System.IO.File.Exists(filePath))
            {
                switch (fileNameExtention)
                {
                    case "docx":
                    case "pdf":
                    case "jpg":
                        await SendDocumentAsync(filePath, fileName, message);
                        break;

                    case "oga":
                        await SendVoiceAsync(filePath, fileName, message);
                        break;

                    case "mp3":
                        await SendAudioAsync(filePath, fileName, message);
                        break;
                }
            }
        }

        /// <summary>
        /// Отправляет пользователю файлы типа Document
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="fileName"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private static async Task SendDocumentAsync(string filePath, string fileName, Message message)
        {
            FileStream fs = new FileStream(filePath, FileMode.Open);
            Message mess = await bot.SendDocumentAsync(message.From.Id, new Telegram.Bot.Types.InputFiles.InputOnlineFile(fs, fileName));
            fs.Close();
            fs.Dispose();
        }

        /// <summary>
        /// Отправляет пользователю гоолосовое сообщение
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="fileName"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private static async Task SendVoiceAsync(string filePath, string fileName, Message message)
        {
            FileStream fs = new FileStream(filePath, FileMode.Open);
            Message mess = await bot.SendVoiceAsync(message.From.Id, new Telegram.Bot.Types.InputFiles.InputOnlineFile(fs, fileName));
            fs.Close();
            fs.Dispose();
        }

        /// <summary>
        /// Отправляет пользователю первоначальное сообщение (по команде /start)
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private static async Task SendStartTextAsync(Message message)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"Привет, {message.From.Username}!\nЯ телеграмм-бот, меня зовут Старый пёс.\nЯ показываю температуру воздуха.\n");
            sb.Append("Также я могу схранять ваши файлы, выводить их список и отправлять их обратно по запросу.\n\n");
            sb.Append($"Вот список доступных команд:\n\n");
            sb.Append(helpText);
            await SendText(message.From.Id, sb.ToString());
        }

        /// <summary>
        /// Отправляет пользователю аудио-файл
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="fileName"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private static async Task SendAudioAsync(string filePath, string fileName, Message message)
        {
            FileStream fs = new FileStream(filePath, FileMode.Open);
            Message mess = await bot.SendAudioAsync(message.From.Id, new Telegram.Bot.Types.InputFiles.InputOnlineFile(fs, fileName));
            fs.Close();
            fs.Dispose();
        }

        /// <summary>
        /// Отправляет пользователю текстовой сообщение
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="text"></param>
        /// <returns></returns>
        private static async Task SendText(long userId, string text)
        {
            await bot.SendTextMessageAsync(userId, text);
        }

        /// <summary>
        /// Получает сведения о погоде и отправляет их пользователю
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        private static async Task GetWeatherInfoAsync(long userId)
        {
            //Получаем сведения о погоде
            HttpResponseMessage mess = await client.GetAsync($"https://api.weather.yandex.ru/v2/forecast?lat=60.0663&lon=30.4154&extra=true");
            string responceText = mess.Content.ReadAsStringAsync().Result;

            JObject json = JObject.Parse(responceText);     //Создаём JSON-объект

            //Формируем строку, содержащую локацию и температуру воздуха на данный момент
            StringBuilder sb = new StringBuilder();
            sb.Append(json["geo_object"]["province"]["name"]+"\n");
            sb.Append(json["geo_object"]["locality"]["name"]+"\n");
            sb.Append($"Температура воздуха сейчас: {json["fact"]["temp"]} градусов."+"\n");

            _ = SendText(userId, sb.ToString());            //Отправляем текст пользователю
        }
    }
}
