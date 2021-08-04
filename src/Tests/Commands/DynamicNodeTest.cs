using System;
using System.Linq;
using System.Threading.Tasks;
using BotCatMaxy.Data;
using BotCatMaxy.Models;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Tests.Commands.Attributes;
using Tests.Commands.BaseTests;
using Tests.Mocks;
using Tests.Mocks.Guild;
using Xunit;

namespace Tests.Commands
{
    public class DynamicNodeTest : BaseDynamicCommandTest
    {
        [InsertRole("TestRole")]
        private IRole _testRole;

        [InsertUser("TestUser", "TestRole")]
        private IGuildUser _testUser;

        [Theory]
        [InlineData("*")]
        [InlineData("Permissions.*")]
        [InlineData("Permissions.Nodes.*")]
        [InlineData("Permissions.Nodes.Remove")]
        public async Task CheckDynamicPermissionAsync(string node)
        {
            var instance = new DynamicPermissionAttribute("Permissions.Nodes.Remove");
            var channel = await Guild.CreateTextChannelAsync("BasicChannel") as MockTextChannel;
            Assert.NotNull(channel);
            var message = channel.SendMessageAsOther("Test1", _testUser);
            var context = new MockCommandContext(Client, message);
            var result = await instance.CheckPermissionsAsync(context, null, null);
            Assert.False(result.IsSuccess);

            var owner = await Guild.GetOwnerAsync();
            Tuple<CommandResult, MockCommandContext> cmdResult = await ExecuteCommandResult($"!AddPermission {_testRole.Id} {node}", owner, channel);
            var permissions = Guild.LoadFromFile<CommandPermissions>(false);
            Assert.True(permissions.RoleHasValue(_testRole.Id, node));
            message = channel.SendMessageAsOther("Test2", _testUser);
            context = new MockCommandContext(Client, message);
            result = await instance.CheckPermissionsAsync(context, null, null);
            Assert.True(result.IsSuccess);
        }
    }
}