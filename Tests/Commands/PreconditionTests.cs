using BotCatMaxy.Models;
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

namespace Tests.Commands
{
    public class PreconditionTests : CommandTests
    {
        //TODO: Once roles are properly mocked need to add new cases for "Tester" user with position > 1 acting on "Testee"
        [Fact]
        public async Task CheckHierarchyAttribute()
        {
            var instance = new RequireHierarchyAttribute();

            //Set up channel and context
            var channel = await guild.CreateTextChannelAsync("HierarchyChannel") as MockTextChannel;
            var users = await guild.GetUsersAsync();
            var owner = users.First(user => user.Username == "Owner");
            var testee = users.First(user => user.Username == "Testee");
            var message = channel.SendMessageAsOther($"!warn {testee.Id} test", owner);
            MockCommandContext context = new(client, message);

            //Simple function so we don't need to this out for all the cases
            async Task<bool> CheckPermissions(IGuildUser user)
            {
                var tasks = new Task<PreconditionResult>[3];
                tasks[0] = instance.CheckPermissionsAsync(context, null, user, null);
                tasks[1] = instance.CheckPermissionsAsync(context, null, new UserRef(user), null);
                tasks[2] = instance.CheckPermissionsAsync(context, null, user.Id, null);
                var results = await Task.WhenAll(tasks);
                return results.All(result => result.IsSuccess); //Should always equal even if expecting false
            }

            //Check if Owner can do stuff to a user, should always be true
            Assert.True(await CheckPermissions(testee));

            //Check if a user can do stuff to an Owner, should always be false
            message = channel.SendMessageAsOther($"!warn {owner.Id} test", testee); //NOTE: The author of the message is what actually matters here
            context = new MockCommandContext(client, message);
            Assert.False(await CheckPermissions(owner));
            //Check if a user can do stuff to themselves, should always be false unless the behavior is changed in which case the test needs to be changed
            Assert.False(await CheckPermissions(testee));
        }
    }
}
