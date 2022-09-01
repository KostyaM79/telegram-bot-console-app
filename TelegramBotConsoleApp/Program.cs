using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TelegramBotConsoleApp
{
    class Program
    {
        static TelegramBotClient bot;
        static string token;

        static async Task Main(string[] args)
        {
            token = System.IO.File.ReadAllText("token.txt");

            bot = new TelegramBotClient(token);
            var cts = new CancellationTokenSource();
            var receiverOptions = new ReceiverOptions()
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            };

            bot.StartReceiving(updateHandler: HandleUpdateAsync, pollingErrorHandler: HandlePollingErrorAsync, receiverOptions: receiverOptions, cancellationToken: cts.Token);
            var me = await bot.GetMeAsync();
            Console.WriteLine($"Start listening for @{me.Username}");

            Console.ReadKey();
            cts.Cancel();
        }

        private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                if (update.Type == UpdateType.Message)
                {
                    var message = update.Message;

                    Console.WriteLine($"Тип сообщения: {message.Type}\nID: {message.From.Id}\nПользователь: {message.From.FirstName}\n");

                    switch(message.Type)
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
            });
        }

        private static async Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            await Task.Run(() => { });
        }

        /// <summary>
        /// Обрабатывает текстовые сообщения
        /// </summary>
        /// <param name="message"></param>
        private static void TextMessageHandler(Message message)
        {
            if (message.Text[0] == '/') CommandHandler(message);
            else Console.WriteLine(message.Text);
        }

        /// <summary>
        /// Обрабатывает команды пользователя
        /// </summary>
        /// <param name="message"></param>
        private static void CommandHandler(Message message)
        {
            string[] parametrs = message.Text.Split(' ');
            string cmd = message.Text.Substring(0, message.Text.IndexOf(' '));

            switch (parametrs[0])
            {
                case "/start":
                    _ = SendStartMessageAsync(message);
                    break;

                case "/get_files":
                    SendFileList(message);
                    break;

                case "/load":
                    string fileName = message.Text.Remove(0, message.Text.IndexOf(' ') + 1);
                    UploadFileAsync(fileName, message);
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

        private static void SaveAudio(Message message)
        {
            SaveFile(message.Audio.FileName, message.Audio.FileId, message.From.Id);
        }

        private static void SaveDocument(Message message)
        {
            SaveFile(message.Document.FileName, message.Document.FileId, message.From.Id);
        }

        private static async void SaveVoiceAsync(Message message)
        {
            //SaveFile(CreateVoiceFileName(message.From.Id), message.Voice.FileId, message.From.Id);

            Telegram.Bot.Types.File file = await bot.GetFileAsync(message.Voice.FileId);
            string fileName = file.FilePath.Split('/')[1];
            SaveFile(fileName, file.FileId, message.From.Id);
        }

        private static async void SavePhotoAsync(Message message)
        {
            Telegram.Bot.Types.File file = await bot.GetFileAsync(message.Photo[message.Photo.Length - 1].FileId);
            string fileName = file.FilePath.Split('/')[1];
            SaveFile(fileName, file.FileId, message.From.Id);
        }

        private static async void UploadFileAsync(string fileName, Message message)
        {
            string filePath = $"files\\{message.From.Id}\\{fileName}";
            string fileNameExtention = fileName.Substring(fileName.LastIndexOf('.') + 1);

            if (System.IO.File.Exists(filePath))
            {
                switch (fileNameExtention)
                {
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

        private static async Task SendDocumentAsync(string filePath, string fileName, Message message)
        {
            FileStream fs = new FileStream(filePath, FileMode.Open);
            Message mess = await bot.SendDocumentAsync(message.From.Id, new Telegram.Bot.Types.InputFiles.InputOnlineFile(fs, fileName));
            fs.Close();
            fs.Dispose();
        }

        private static async Task SendVoiceAsync(string filePath, string fileName, Message message)
        {
            FileStream fs = new FileStream(filePath, FileMode.Open);
            Message mess = await bot.SendVoiceAsync(message.From.Id, new Telegram.Bot.Types.InputFiles.InputOnlineFile(fs, fileName));
            fs.Close();
            fs.Dispose();
        }

        private static async Task SendStartMessageAsync(Message message)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"Привет, {message.From.Username}!\nЯ телеграмм-бот, меня зовут Старый пёс.\nЯ могу схранять ваши файлы, выводить их список и отправлять их обратно по запросу.\n\n");
            sb.Append($"Чтобы я сохранил ваш файл, просто отправьте мне его.\nЧтобы получить список ваших файлов, введите команду /get_files\n");
            sb.Append($"Чтобы загрузить файл, введите команду /load, через пробел после команды введите имя файла.");
            await bot.SendTextMessageAsync(message.From.Id, sb.ToString());
        }

        private static async Task SendAudioAsync(string filePath, string fileName, Message message)
        {
            FileStream fs = new FileStream(filePath, FileMode.Open);
            Message mess = await bot.SendAudioAsync(message.From.Id, new Telegram.Bot.Types.InputFiles.InputOnlineFile(fs, fileName));
            fs.Close();
            fs.Dispose();
        }
    }
}
