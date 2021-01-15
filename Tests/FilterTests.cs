using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BotCatMaxy.Components.Filter;
using BotCatMaxy;
using Xunit;
using BotCatMaxy.Models;

namespace Tests
{
    public class FilterTests
    {
        public BadWord[] badWords = { new BadWord("Calzone"), new BadWord("Something") { PartOfWord = false }, new BadWord("Substitution") };

        [Theory]
        [InlineData("We like calzones", "calzone")]
        [InlineData("Somethings is here", null)]
        [InlineData("$ubst1tuti0n", "Substitution")]
        public void BadWordTheory(string input, string expected)
        {
            var result = input.CheckForBadWords(badWords);
            Assert.Equal(expected, result?.Word, ignoreCase: true);
        }
    }
}
