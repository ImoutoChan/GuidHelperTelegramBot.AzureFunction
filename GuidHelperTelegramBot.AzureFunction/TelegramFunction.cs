using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GuidHelperTelegramBot.AzureFunction
{
    public static class TelegramFunction
    {
        private static readonly Regex Base64Regex = new Regex(
            @"[a-z0-9+\/]{16,}==",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex Base64ShortRegex = new Regex(
            @"[a-z0-9+\/]{16,}",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        [FunctionName("Telegram")]
        public static async Task Update(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest request,
            ILogger logger)
        {
            try
            {
                await ProcessUpdate(request, logger);
            }
            catch(Exception ex)
            {
                logger.LogError(ex, "Unable to process an update");
            }
        }

        private static async Task ProcessUpdate(HttpRequest request, ILogger logger)
        {
            var client = GetTelegramBotClient();

            logger.LogInformation("New update received");

            var requestBody = await request.ReadAsStringAsync();
            var update = JsonConvert.DeserializeObject<Update>(requestBody);

            if (update.Type != UpdateType.Message || string.IsNullOrEmpty(update.Message.Text))
                return;

            if (update.Message.Text == "/start")
            {
                await client.SendTextMessageAsync(
                    update.Message.Chat.Id,
                    "Hello! Send a binary formatted id to me and I return you a guid representation!\n" +
                    "For example: KQAAAIauU0aduPa9rNCU5Q== or KQAAAIauU0aduPa9rNCU5Q",
                    replyToMessageId: update.Message.MessageId);
                return;
            }

            if (TryParseBase64(update.Message.Text, out var base64))
            {
                var bytes = Convert.FromBase64String(base64);
                var guid = new Guid(bytes);

                await client.SendTextMessageAsync(
                    update.Message.Chat.Id,
                    guid.ToString(),
                    replyToMessageId: update.Message.MessageId);
                return;
            }

            if (Guid.TryParse(update.Message.Text, out var parsedGuid))
            {
                var bytes = parsedGuid.ToByteArray();
                var resultBase64 = Convert.ToBase64String(bytes);

                await client.SendTextMessageAsync(
                    update.Message.Chat.Id,
                    resultBase64,
                    replyToMessageId: update.Message.MessageId);

                await client.SendTextMessageAsync(
                    update.Message.Chat.Id,
                    $"BinData(3, '{resultBase64}')",
                    replyToMessageId: update.Message.MessageId);

                await client.SendTextMessageAsync(
                    update.Message.Chat.Id,
                    $"{{ _id: BinData(3, '{resultBase64}') }}",
                    replyToMessageId: update.Message.MessageId);

                if (update.Message.Text.Trim().ToLowerInvariant().EndsWith("-ae86-4653-9db8-f6bdacd094e5"))
                {
                    var resultInt = IdConverter.ConvertToInt(parsedGuid);
                    
                    await client.SendTextMessageAsync(
                        update.Message.Chat.Id,
                        resultInt.ToString(),
                        replyToMessageId: update.Message.MessageId);
                }

                return;
            }

            if (Int32.TryParse(update.Message.Text, out var parsedInt))
            {
                var resultGuid = IdConverter.ConvertToGuid(parsedInt);

                await client.SendTextMessageAsync(
                    update.Message.Chat.Id,
                    resultGuid.ToString(),
                    replyToMessageId: update.Message.MessageId);
                return;
            }
        }

        private static bool TryParseBase64(string messageText, [NotNullWhen(true)] out string? base64)
        {
            var match = Base64Regex.Match(messageText);
            if (match.Success)
            {
                base64 = match.Value;
                return true;
            }

            var matchShort = Base64ShortRegex.Match(messageText);
            if (matchShort.Success)
            {
                base64 = matchShort.Value + "==";
                return true;
            }

            base64 = null;
            return false;
        }

        private static TelegramBotClient GetTelegramBotClient()
        {
            var token = Environment.GetEnvironmentVariable("TelegramBotToken");
            if (token is null)
                throw new Exception("Unable to read telegram bot token");

            return new TelegramBotClient(token);
        }
    }

    public static class IdConverter
    {
        private static readonly byte[] BaseGuid =
            Guid.Parse("00000000-ae86-4653-9db8-f6bdacd094e5")
                .ToByteArray();

        public static Guid? ConvertToGuid(int? obsoleteId)
        {
            if (!obsoleteId.HasValue)
            {
                return null;
            }

            return ConvertToGuid(obsoleteId.Value);
        }

        public static Guid ConvertToGuid(int obsoleteId)
        {
            Span<byte> resultArray = stackalloc byte[16];

            BaseGuid.CopyTo(resultArray);
            BitConverter.GetBytes(obsoleteId).CopyTo(resultArray);

            return new Guid(resultArray.ToArray());
        }

        public static int ConvertToInt(Guid id)
        {
            var bytes = id.ToByteArray().AsSpan();
            return BitConverter.ToInt32(bytes.Slice(0, 4).ToArray(), 0);
        }
    }
}
