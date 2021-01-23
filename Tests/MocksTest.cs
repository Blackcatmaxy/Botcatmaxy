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
    }
}
