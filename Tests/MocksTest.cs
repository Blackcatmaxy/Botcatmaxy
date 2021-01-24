using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using BotCatMaxy.Models;
using Tests.Mocks;

namespace Tests
{
    public class MocksTest
    {
        [Fact]
        public async Task MessageTest()
        {
            var channel = new MockMessageChannel();
            var message = await channel.SendMessageAsync("Test");
            Assert.Equal("Test", message.Content);
            message = await channel.GetMessageAsync(message.Id) as UserMockMessage;
            Assert.Equal("Test", message?.Content);
            await channel.DeleteMessageAsync(message.Id);
            message = await channel.GetMessageAsync(message.Id) as UserMockMessage;
            Assert.Null(message);
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
