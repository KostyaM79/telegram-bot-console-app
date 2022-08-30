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

                    switch(message.Type)
                    {
                        case MessageType.Text:
                            TextMessageHandler(message);
                            break;

                        case MessageType.Document:
                            SaveFile(message);
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
            switch (message.Text)
            {
                case "/start":
                    bot.SendTextMessageAsync(message.Chat.Id, "Начнём, пожалуй.");
                    break;

                case "/get_files":
                    SendFileList(message);
                    break;

                case "/exit":
                    bot.SendTextMessageAsync(message.Chat.Id, "Пока!");
                    break;
            }
        }

        /// <summary>
        /// Сохраняет полученные файлы
        /// </summary>
        /// <param name="message"></param>
        private static async void SaveFile(Message message)
        {
            string directoryPath = System.IO.Directory.CreateDirectory($"files\\{message.Chat.Id}").FullName;   //Создаём папку и получаем её путь
            string fileName = message.Document.FileName;                                                        //Получаем имя файла
            string fileId = message.Document.FileId;                                                            //Получаем ID файла
            long chatId = message.Chat.Id;                                                                      //Получаем ID пользователя

            var file = await bot.GetFileAsync(fileId);
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
    }
}
