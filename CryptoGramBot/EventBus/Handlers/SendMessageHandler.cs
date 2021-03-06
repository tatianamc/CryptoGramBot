﻿using System;
using System.Threading.Tasks;
using CryptoGramBot.Services;
using Enexure.MicroBus;
using Microsoft.Extensions.Logging;
using Serilog;
using Telegram.Bot.Types.Enums;

namespace CryptoGramBot.EventBus.Handlers
{
    public class SendMessageCommand : ICommand
    {
        public SendMessageCommand(string message)
        {
            Message = message;
        }

        public string Message { get; }
    }

    public class SendMessageHandler : ICommandHandler<SendMessageCommand>
    {
        private readonly TelegramBot _bot;
        private readonly ILogger<SendMessageHandler> _log;

        public SendMessageHandler(TelegramBot bot, ILogger<SendMessageHandler> log)
        {
            _bot = bot;
            _log = log;
        }

        public async Task Handle(SendMessageCommand command)
        {
            var message = command.Message;

            if (message.Length <= 4094)
            {
                await _bot.SendHtmlMessage(_bot.ChatId, message);
            }
            else
            {
                var newMessage = string.Empty;
                var strings = message.Split("\n");

                foreach (var s in strings)
                {
                    var newStringLengh = s.Length;
                    var stringWithNewLine = s + "\n";

                    if (newMessage.Length + newStringLengh <= 4094)
                    {
                        newMessage = newMessage + stringWithNewLine;
                    }
                    else if (newMessage.Length + newStringLengh == 4094)
                    {
                        newMessage = newMessage + stringWithNewLine;
                        await _bot.SendHtmlMessage(_bot.ChatId, newMessage);
                        newMessage = string.Empty;
                    }
                    else if (newMessage.Length + newStringLengh > 4094)
                    {
                        await _bot.SendHtmlMessage(_bot.ChatId, newMessage);
                        newMessage = stringWithNewLine;
                    }
                }

                if (!string.IsNullOrEmpty(newMessage))
                {
                    await _bot.SendHtmlMessage(_bot.ChatId, newMessage);
                }
            }

            _log.LogInformation($"Send Message:\n" + message);
        }
    }
}