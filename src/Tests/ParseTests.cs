using BotCatMaxy;
using System;
using Xunit;

namespace Tests
{
    public class ParseTests
    {
        [Theory]
        [InlineData("hey", null)]
        [InlineData("30m", 30d)]
        [InlineData("1h", 60d)]
        [InlineData("30m1h", 90d)]
        [InlineData("1h30m", 90d)]
        public void TimeSpanTheory(string input, double? minutes)
        {
            double? result = null;
            var time = input.ToTime();
            if (time.HasValue) result = time.Value.TotalMinutes;
            Assert.Equal(minutes, result);
        }
    }
}
