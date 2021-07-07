using System.Linq;
using System.Threading.Tasks;
using BotCatMaxy.Models;
using Discord;
using Discord.Commands;
using Tests.Mocks.Guild;
using Xunit;

namespace Tests.Commands
{
    public class DynamicPermissionTests : CommandTests
    {
        [Theory]
        [InlineData("*")]
        [InlineData("Permissions.*")]
        [InlineData("Permissions.Nodes.*")]
        [InlineData("Permissions.Nodes.Remove")]
        public async Task CheckDynamicPermissionAsync(string node)
        {
            //Set Up
            //var permissions = new CommandPermissions() {guild = guild};
            var channel = await Guild.CreateTextChannelAsync("BasicChannel") as MockTextChannel;
            var users = await Guild.GetUsersAsync();
            var owner = users.First(user => user.Username == "Owner");
            var role = await Guild.CreateRoleAsync("TestRole", GuildPermissions.None, isMentionable: false);
            await owner.AddRoleAsync(role);
            
            var cmdResult = await ExecuteCommandResult($"!AddPermission {role.Id} {node}", owner, channel);
            var context = cmdResult.Item2;
            
            var instance = new DynamicPermissionAttribute("Permissions.Nodes.Remove");
            var result = await instance.CheckPermissionsAsync(context, null, null);
            Assert.True(result.IsSuccess);
        }        
    }
}