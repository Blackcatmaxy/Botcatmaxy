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
        [InlineData("Debug.*")]
        [InlineData("Debug.Test.*")]
        [InlineData("Debug.Test.Something")]
        public async Task CheckDynamicPermissionAsync(string node)
        {
            //Set Up
            //var permissions = new CommandPermissions() {guild = guild};
            var channel = await guild.CreateTextChannelAsync("BasicChannel") as MockTextChannel;
            var users = await guild.GetUsersAsync();
            var owner = users.First(user => user.Username == "Owner");
            var role = await guild.CreateRoleAsync("TestRole", GuildPermissions.None, isMentionable: false);
            await owner.AddRoleAsync(role);
            
            var cmdResult = await ExecuteCommandResult($"!AddPermission {role.Id} {node}", owner, channel);
            var context = cmdResult.Item2;
            
            var instance = new DynamicPermissionAttribute("Debug.Test.Something");
            var result = await instance.CheckPermissionsAsync(context, null, null);
            Assert.True(result.IsSuccess);
        }        
    }
}