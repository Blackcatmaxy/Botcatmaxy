using BotCatMaxy.TypeReaders;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace BotCatMaxy.TypeReaders
{
    public class TimeSpanTypeReader : TypeReader
    {
        private const string regex = @"\d+\.?\d?[ywdhms]";

        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            var result = ParseTime(input);
            return (result != null) ?
                Task.FromResult(TypeReaderResult.FromSuccess(result))
                : Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Failed to parse TimeSpan"));
        }

        public static TimeSpan? ParseTime(string input)
        {
            input = input.ToLowerInvariant();
            var results = Regex.Matches(input, regex, RegexOptions.CultureInvariant | RegexOptions.Compiled);
            if (results.Count == 0 || results.Sum(m => m.Length) != input.Length) return null;
            TimeSpan result = new TimeSpan();
            foreach (Match match in results)
            {
                string s = match.Value;
                if (!double.TryParse(s.Remove(match.Length - 1), out double amount)) return null;
                TimeSpan increment = s.Last() switch
                {
                    'y' => TimeSpan.FromDays(amount * 365.2425),
                    'w' => TimeSpan.FromDays(amount * 7),
                    'd' => TimeSpan.FromDays(amount),
                    'h' => TimeSpan.FromHours(amount),
                    'm' => TimeSpan.FromMinutes(amount),
                    's' => TimeSpan.FromSeconds(amount),
                    _ => throw new NotImplementedException(),
                };
                result = result.Add(increment);
            }
            return result;
        }
    }
}

namespace BotCatMaxy
{
    public static class TimeSpanUtilities
    {
        public static TimeSpan? ToTime(this string s)
            => TimeSpanTypeReader.ParseTime(s);
    }
}
