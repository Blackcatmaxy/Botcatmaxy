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
    public class FilterTest : BaseDataTest
    {
        private readonly BadWord[] _badWords = { new BadWord("Calzone"), new BadWord("Something") { PartOfWord = false }, new BadWord("Substitution") };
        private readonly MockDiscordClient _client = new();
        private readonly MockGuild _guild = new();
        private readonly MockGuildUser _testee;
        private readonly FilterHandler _filter;
        private readonly FilterSettings _settings;
        private readonly Task<ITextChannel> _channelTask;

        public FilterTest()
        {
            _filter = new(_client);
            _client.guilds.Add(_guild);
            _channelTask = _guild.CreateTextChannelAsync("TestChannel");
            _testee = new MockGuildUser("testee", _guild);
            _guild.AddUser(_testee);
            _settings = new FilterSettings()
            {
                guild = _guild,
                moderateNames = true,
                maxNewLines = 5
            };
            var badWordList = new BadWordList { badWords = _badWords.ToList(), guild = _guild };
            badWordList.SaveToFile();
            _settings.SaveToFile();
        }

        [Fact]
        public async Task PunishTest()
        {
            var channel = (MockTextChannel)await _channelTask;
            var message = channel.SendMessageAsOther("calzone", _testee);
            var context = new MockCommandContext(_client, message);
            await context.FilterPunish("Testing Punish", null, _settings, "calzone");
            var infractons = _testee.LoadInfractions(true);
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
            var result = input.CheckForBadWords(_badWords);
            Assert.Equal(detected, result.word?.Word, ignoreCase: true);

            var channel = (MockTextChannel)await _channelTask;
            var message = channel.SendMessageAsOther(input, _testee);
            var context = new MockCommandContext(_client, message);
            await _filter.CheckMessage(message, context);
            if (detected != null)
            {
                var infractions = _testee.LoadInfractions(false);
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
