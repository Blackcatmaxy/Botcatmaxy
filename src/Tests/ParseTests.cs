using BotCatMaxy;
using System;
using Xunit;

namespace Tests
{
    public class ParseTests
    {
        [Theory]
        [InlineData("hey", null)]
        [InlineData("30m", 30)]
        [InlineData("1h", 60)]
        [InlineData("30m1h", 90)]
        [InlineData("1h30m", 90)]
        public void TimeSpanTheory(string input, double? minutes)
        {
            double? result = null;
            var time = input.ToTime();
            if (time.HasValue) result = time.Value.TotalMinutes;
            Assert.Equal(minutes, result);
        }
    }
}
