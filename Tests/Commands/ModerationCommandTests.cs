using BotCatMaxy.Data;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tests.Mocks;
using Tests.Mocks.Guild;
using Xunit;

namespace Tests.Commands
{
    public class ModerationCommandTests : CommandTests
    {
        [Fact]
        public async Task WarnCommandTest()
        {
            var channel = await guild.CreateTextChannelAsync("WarnChannel") as MockTextChannel;
            var users = await guild.GetUsersAsync();
            var owner = users.First(user => user.Username == "Owner");
            var testee = users.First(user => user.Username == "Testee");
            var message = channel.SendMessageAsOther($"!warn {testee.Id} test", owner);
            MockCommandContext context = new(client, message);
            await handler.ExecuteCommand(message, context);
            var messages = await channel.GetMessagesAsync().FlattenAsync();
            Assert.Equal(2, messages.Count());
            var infractions = testee.LoadInfractions(false);
            Assert.NotNull(infractions);
            Assert.NotEmpty(infractions);
        }
    }
}
