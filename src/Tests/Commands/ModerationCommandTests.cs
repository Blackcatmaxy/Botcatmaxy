using BotCatMaxy.Data;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BotCatMaxy.Models;
using Tests.Mocks;
using BotCatMaxy.Moderation;
using Discord.Commands;
using Tests.Commands.Attributes;
using Tests.Commands.BaseTests;
using Tests.Mocks.Guild;
using Xunit;

namespace Tests.Commands
{
    public class ModerationCommandTests : BaseDynamicCommandTest
    {
        [InsertRole("PermModeratorRole", GuildPermission.KickMembers)]
        private IRole _permModeratorRole;
        [InsertRole("NodeModeratorRole", new []{"Moderation.Warn.*"})]
        private IRole _nodeModeratorRole;

        [InsertUser("Moderator", "NodeModeratorRole")]
        private IGuildUser _nodeMod;
        [InsertUser("Moderator", "PermModeratorRole")]
        private IGuildUser _permMod;
        [InsertUser("testee")]
        private IGuildUser _testee;

        [Fact]
        public async Task TestNodeRole()
        {
            var permissions = Guild.LoadFromFile<CommandPermissions>();
            permissions.enabled = true;
            permissions.SaveToFile();
            await TestWarnByUser(_nodeMod, true);
            await TestWarnByUser(_permMod, false);
        }

        [Fact]
        public Task TestPermRole()
            => TestWarnByUser(_permMod, true);

        private async Task TestWarnByUser(IGuildUser user, bool success)
        {
            var channel = await Guild.CreateTextChannelAsync(user.Username) as MockTextChannel;
            CommandResult result = null;
            try
            {
                result = await TryExecuteCommand($"!warn {_testee.Id} test", user, channel);
            }
            catch (Exception e)
            {
                Assert.Equal("Missing role with permission `Moderation.Warn.Give` to use this command.", e.Message);
            }
            var infractions = _testee.LoadInfractions(false);
            if (success)
            {
                Assert.True(result.IsSuccess);
                Assert.NotNull(infractions);
                Assert.Single(infractions);
            }
            else
            {
                Assert.Null(result);
            }
        }

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
