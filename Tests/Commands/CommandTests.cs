using BotCatMaxy.Cache;
using BotCatMaxy.Data;
using BotCatMaxy.Models;
using BotCatMaxy.Startup;
using BotCatMaxy.TypeReaders;
using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tests.Mocks;
using Tests.Mocks.Guild;
using Xunit;

namespace Tests
{
    public class CommandTests : BaseDataTests
    {
        protected readonly MockDiscordClient client = new();
        protected readonly MockGuild guild = new();
        protected readonly CommandService service;
        protected readonly CommandHandler handler;

        public CommandTests() : base()
        {
            cache = new SettingsCache(client);
            client.guilds.Add(guild);
            service = new CommandService();
            handler = new CommandHandler(client, service);
            service.CommandExecuted += CommandExecuted;
        }

        private Task CommandExecuted(Optional<CommandInfo> arg1, ICommandContext arg2, IResult result)
        {
            if (result.Error == CommandError.Exception) throw ((ExecuteResult)result).Exception;
            if (!result.IsSuccess) throw new Exception(result.ErrorReason);
            return Task.CompletedTask;
        }
    }

    public class BasicCommandTests : CommandTests
    {
        [Fact]
        public async Task BasicCommandCheck()
        {
            var channel = await guild.CreateTextChannelAsync("BasicChannel") as MockTextChannel;
            var users = await guild.GetUsersAsync();
            var owner = users.First(user => user.Username == "Owner");
            var message = channel.SendMessageAsOther("!toggleserverstorage", owner);
            MockCommandContext context = new(client, message);
            Assert.True(context.Channel is IGuildChannel);
            Assert.True(context.User is IGuildUser);
            await handler.ExecuteCommand(message, context);
            var messages = await channel.GetMessagesAsync().FlattenAsync();
            Assert.Equal(2, messages.Count());
            var response = messages.First();
            var expected = "This is a legacy feature, if you want this done now contact blackcatmaxy@gmail.com with your guild invite and your username so I can get back to you";
            Assert.Equal(expected, response.Content);
        }

        [Fact]
        public async Task TypeReaderTest()
        {
            var channel = await guild.CreateTextChannelAsync("TypeReaderChannel") as MockTextChannel;
            var users = await guild.GetUsersAsync();
            var owner = users.First(user => user.Username == "Owner");
            var testee = users.First(user => user.Username == "Testee");
            var message = channel.SendMessageAsOther($"!warn {testee.Id} test", owner);
            MockCommandContext context = new(client, message);
            var userRefReader = new UserRefTypeReader();
            var result = await userRefReader.ReadAsync(context, testee.Id.ToString(), handler.services);
            Assert.True(result.IsSuccess);
            var match = result.BestMatch as UserRef;
            Assert.NotNull(match);

        }


    }
}
