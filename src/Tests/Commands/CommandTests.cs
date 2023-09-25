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
using Microsoft.Extensions.DependencyInjection;
using Tests.Mocks;
using Tests.Mocks.Guild;
using Xunit;

namespace Tests
{
    public class CommandTests : BaseDataTests, IAsyncLifetime
    {
        protected MockDiscordClient Client { get; } = new();
        protected MockGuild Guild { get; } = new();
        protected CommandService Service { get; }
        protected TextCommandHandler Handler { get; }
        protected ServiceProvider Provider { get; }
        protected CommandResult CommandResult { get; private set; }
        protected TaskCompletionSource<CommandResult> CompletionSource { get; private set; }

        public CommandTests()
        {
            cache = new SettingsCache(Client);
            Client.guilds.Add(Guild);
            Provider = new ServiceCollection()
                .AddSingleton(Client)
                .BuildServiceProvider();
            Service = new CommandService();
            Handler = new TextCommandHandler(Client, null, Provider, Service);
            Service.CommandExecuted += CommandExecuted;
        }

        public async Task InitializeAsync()
            => await Handler.InitializeAsync(default);

        public Task DisposeAsync()
            => Task.CompletedTask;

        public async Task<CommandResult> TryExecuteCommand(string text, IUser user, MockTextChannel channel)
        {
            Assert.NotNull(channel);
            Assert.NotNull(user);
            var message = channel.SendMessageAsOther(text, user);
            var context = new MockCommandContext(Client, message);
            CompletionSource = new TaskCompletionSource<CommandResult>();
            await Handler.ExecuteCommand(message, context);
            return await CompletionSource.Task;
        }

        /// <summary>
        /// Executes after command is finished with full info <seealso cref="SlashCommandHandler"/>'s CommandExecuted
        /// </summary>
        private Task CommandExecuted(Optional<CommandInfo> arg1, ICommandContext context, IResult result)
        {
            if (result is CommandResult commandResult)
            {
                CommandResult = commandResult;
                CompletionSource.SetResult(commandResult);
            }
            else if (result.Error == CommandError.Exception) CompletionSource.SetException(((ExecuteResult)result).Exception);
            else if (!result.IsSuccess) CompletionSource.SetException(new Exception(result.ErrorReason));
            else CompletionSource.SetResult(new CommandResult(null, "Test"));

            return Task.CompletedTask;
        }
    }

    public class CommandTestException : Exception
    {
        public CommandTestException(IResult result) : base(result.ErrorReason) { }
    }

    public class BasicCommandTests : CommandTests
    {
        [Fact]
        public async Task BasicCommandCheck()
        {
            var channel = await Guild.CreateTextChannelAsync("BasicChannel") as MockTextChannel;
            var messages = await channel.GetMessagesAsync().FlattenAsync();
            Assert.Empty(messages);
            var users = await Guild.GetUsersAsync();
            var owner = users.First(user => user.Username == "Owner");
            var result = await TryExecuteCommand("!toggleserverstorage", owner, channel);
            messages = await channel.GetMessagesAsync().FlattenAsync();
            Assert.True(result.IsSuccess);
            Assert.Equal(2, messages.Count());
            var response = messages.First();
            var expected = "This is a legacy feature, if you want this done now contact blackcatmaxy@gmail.com with your guild invite and your username so I can get back to you";
            Assert.Equal(expected, response.Content);
        }

        [Fact]
        public async Task TypeReaderTest()
        {
            var channel = await Guild.CreateTextChannelAsync("TypeReaderChannel") as MockTextChannel;
            var users = await Guild.GetUsersAsync();
            var owner = users.First(user => user.Username == "Owner");
            var testee = users.First(user => user.Username == "Testee");
            var message = channel.SendMessageAsOther($"!warn {testee.Id} test", owner);
            MockCommandContext context = new(Client, message);
            var userRefReader = new UserRefTypeReader();
            var result = await userRefReader.ReadAsync(context, testee.Id.ToString(), Provider);
            Assert.True(result.IsSuccess);
            var match = result.BestMatch as UserRef;
            Assert.NotNull(match);

        }
    }
}
