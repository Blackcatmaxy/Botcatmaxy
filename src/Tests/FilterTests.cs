using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BotCatMaxy.Components.Filter;
using BotCatMaxy;
using Xunit;
using BotCatMaxy.Models;
using Tests.Mocks;
using BotCatMaxy.Startup;
using Tests.Mocks.Guild;
using Discord;
using BotCatMaxy.Data;
using System.Text.RegularExpressions;

namespace Tests
{
    public class FilterTests : BaseDataTests
    {
        private readonly BadWord[] badWords = { new BadWord("Calzone"), new BadWord("Something") { PartOfWord = false }, new BadWord("Substitution") };
        private readonly MockDiscordClient client = new();
        private readonly MockGuild guild = new();
        private readonly FilterHandler filter;
        private FilterSettings settings;
        private Task<ITextChannel> channelTask;

        public FilterTests()
        {
            filter = new(client);
            client.guilds.Add(guild);
            channelTask = guild.CreateTextChannelAsync("TestChannel");
            FilterSettings settings = new()
            {
                guild = guild,
                moderateNames = true,
                maxNewLines = 5
            };
            BadWordList badWordList = new BadWordList() { badWords = badWords.ToHashSet(), guild = guild };
            badWordList.SaveToFile();
            settings.SaveToFile();
        }

        [Fact]
        public async Task PunishTest()
        {
            var channel = (MockTextChannel)await channelTask;
            var users = await guild.GetUsersAsync();
            var testee = users.First(user => user.Username == "Testee");
            var message = channel.SendMessageAsOther("calzone", testee);
            var context = new MockCommandContext(client, message);
            await context.FilterPunish("Testing Punish", null, settings, "calzone");
            var infractons = testee.LoadInfractions(true);
            Assert.NotNull(infractons);
            Assert.NotEmpty(infractons);
        }

        [Theory]
        [InlineData("We like calzones", "calzone", "We like **[calzone]**s")]
        [InlineData("Somethings is here", null, null)]
        [InlineData("$ubst1tuti0n", "Substitution", null)]
        [InlineData("I'm a calzone", "calzone", "I'm a **[calzone]**")]
        [InlineData("This is a calz0ne", "calzone", null)]
        [InlineData("I'm a calz0ne", "calzone", null)]
        [InlineData("https://imgur.com/ac4lz0ne", null, null)]
        public async Task BadWordTheory(string input, string detected, string highlighted)
        {
            var result = input.CheckForBadWords(badWords);
            Assert.Equal(detected, result.word?.Word, ignoreCase: true);

            var channel = (MockTextChannel)await channelTask;
            var users = await guild.GetUsersAsync();
            var testee = users.First(user => user.Username == "Testee");
            var message = channel.SendMessageAsOther(input, testee);
            var context = new MockCommandContext(client, message);
            await filter.CheckMessage(message, context);
            if (detected != null)
            {
                var infractions = testee.LoadInfractions(false);
                Assert.NotNull(infractions);
                Assert.NotEmpty(infractions);
                Assert.Equal(FilterUtilities.HighlightFiltered(input, detected, result.pos), highlighted);
            }
        }

        [Theory]
        [InlineData("discord.gg/test", true)]
        [InlineData("http://discord.gg/test", true)]
        [InlineData("https://discord.gg/test", true)]
        [InlineData("https://discord.com/invite/test", true)]
        [InlineData("https://discord.com/test", false)]
        [InlineData("https://discord.com/security", false)]
        public void InviteRegexTheory(string input, bool expected)
        {
            Assert.True(Regex.IsMatch(input, FilterHandler.inviteRegex) == expected);
        }
    }
}
