using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using BotCatMaxy.Models;
using Tests.Mocks;
using Tests.Mocks.Guild;
using Discord;

namespace Tests
{
    public class MocksTest
    {
        [Fact]
        public async Task GuildTest()
        {
            var guild = new MockGuild();
            var channel = await guild.CreateTextChannelAsync("Channel");
            //Messages
            Assert.Equal(channel.Id, (await guild.GetTextChannelAsync(channel.Id))?.Id);
            var message = await channel.SendMessageAsync("Test");
            Assert.Equal("Test", message.Content);
            message = await channel.GetMessageAsync(message.Id) as UserMockMessage;
            Assert.Equal("Test", message?.Content);
            await channel.DeleteMessageAsync(message.Id);
            message = await channel.GetMessageAsync(message.Id) as UserMockMessage;
            Assert.Null(message);

            //Users
            await guild.DownloadUsersAsync();
            var userID = guild.AddUser(new MockGuildUser("Someone", guild));
            var users = guild.ApproximateMemberCount;

            var user = await guild.GetUserAsync(userID);
            Assert.Equal(userID, user.Id);
            await user.KickAsync("Test");
            Assert.Null(await guild.GetBanAsync(user));
            Assert.NotEqual(users, guild.ApproximateMemberCount);

            guild.AddUser(user as MockGuildUser);
            await guild.AddBanAsync(user, reason: "Test");
            Assert.NotNull(await guild.GetBanAsync(user));
        }

        [Fact]
        public async Task MultipleMessageTest()
        {
            var channel = new MockMessageChannel();
            var message1 = await channel.SendMessageAsync("Test1");
            var message2 = await channel.SendMessageAsync("Test2");
            var messages = await channel.GetMessagesAsync(2).ToArrayAsync();
            var strings = messages[0].Select(message => message.Content);
            CheckMessages(strings);
            messages = await channel.GetMessagesAsync(message1.Id, Discord.Direction.After).ToArrayAsync();
            CheckMessages(strings);
            messages = await channel.GetMessagesAsync(message1, Discord.Direction.After).ToArrayAsync();
            CheckMessages(strings);
            messages = await channel.GetMessagesAsync(message2.Id, Discord.Direction.After).ToArrayAsync();
            CheckMessages(strings);

        }

        internal void CheckMessages(IEnumerable<string> messages, bool containsFirst = true)
        {
            if (containsFirst)
            {
                Assert.Equal(2, messages.Count());
                Assert.Contains("Test1", messages);
            }
            else
            {
                Assert.Single(messages);
                Assert.DoesNotContain("Test1", messages);
            }
            Assert.Contains("Test2", messages);
        }
    }
}
