using BotCatMaxy.Startup;
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
    public class CommandTests
    {
        MockDiscordClient client = new();
        MockGuild guild = new();
        CommandService service;
        CommandHandler handler;
        Task<ITextChannel> channelTask;

        public CommandTests()
        {
            client.guilds.Add(guild);
            channelTask = guild.CreateTextChannelAsync("TestChannel");
            service = new CommandService();
            handler = new CommandHandler(client, service);
            service.CommandExecuted += CommandExecuted;
        }

        [Fact]
        public async Task BasicCommandCheck()
        {
            var channel = await channelTask as MockMessageChannel;
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

        private Task CommandExecuted(Optional<CommandInfo> arg1, ICommandContext arg2, IResult result)
        {
            if (result.Error == CommandError.Exception) throw ((ExecuteResult)result).Exception;
            if (!result.IsSuccess) throw new Exception(result.ErrorReason);
            return Task.CompletedTask;
        }
    }
}
