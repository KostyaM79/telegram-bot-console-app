using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
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

                    if (message.Text[0] == '/')
                    {
                        CommandHandler(message);
                    }
                }
            });
        }

        private static async Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {

        }

        private static void CommandHandler(Message message)
        {
            switch (message.Text)
            {
                case "/start":
                    bot.SendTextMessageAsync(message.Chat.Id, "Начнём, пожалуй.");
                    break;

                case "/getFiles":
                    bot.SendTextMessageAsync(message.Chat.Id, "Скро я смогу возвращать список файлов!");
                    break;


                case "/exit":
                    bot.SendTextMessageAsync(message.Chat.Id, "Пока!");
                    break;
            }
        }
    }
}
