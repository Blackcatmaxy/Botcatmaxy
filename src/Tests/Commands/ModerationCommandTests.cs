using BotCatMaxy.Data;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tests.Mocks;
using BotCatMaxy.Moderation;
using Tests.Commands.Attributes;
using Tests.Commands.BaseTests;
using Tests.Mocks.Guild;
using Xunit;

namespace Tests.Commands
{
    public class ModerationCommandTests : BaseDynamicCommandTest
    {
        [InsertRole("ModeratorRole", new []{"Moderation.Warn.*"})]
        private IRole _moderatorRole;
        [InsertUser("Moderator", "ModeratorRole")]
        private IGuildUser _moderator;
        [InsertUser("testee")]
        private IGuildUser _testee;

        [Fact]
        public async Task WarnCommandAndRemoveTest()
        {
            var channel = await Guild.CreateTextChannelAsync("WarnChannel") as MockTextChannel;
            var owner = await Guild.GetOwnerAsync();

            var result = await TryExecuteCommand($"!removewarn {_testee.Id} 1", owner, channel);
            Assert.False(result.IsSuccess); //No infractions currently
            result = await TryExecuteCommand($"!warn {_testee.Id} test", owner, channel);
            Assert.True(result.IsSuccess);
            var infractions = _testee.LoadInfractions(false);
            Assert.NotNull(infractions);
            Assert.Single(infractions);
            result = await TryExecuteCommand($"!warn {_testee.Id} test", owner, channel);
            Assert.True(result.IsSuccess);
            infractions = _testee.LoadInfractions(false);
            Assert.Equal(2, infractions.Count);

            result = await TryExecuteCommand($"!removewarn {_testee.Id} 1", owner, channel);
            Assert.True(result.IsSuccess);
            result = await TryExecuteCommand($"!removewarn {_testee.Id} 2", owner, channel);
            Assert.False(result.IsSuccess); //Should be out of bounds and fail now since we removed 1 out of 2 of the infractions
            infractions = _testee.LoadInfractions(false);
            Assert.Single(infractions);
        }
    }
}
