using BotCatMaxy.TypeReaders;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BotCatMaxy.TypeReaders
{
    public class TimeSpanTypeReader : TypeReader
    {
        private static readonly string[] Formats = {
            //Decimals time formats
         ///@"%d\.FF'd'%h\.FF'h'%m\.FF'm'%s's'", //1.23d4.56h7.89m1s
            @"%d\.FF'd'",                        //7.89d
            @"%h\.FF'h'",                        //     4.56h
            @"%m\.FF'm'",                        //          4.56m
            //Normal time formats
            "%d'd'%h'h'%m'm'%s's'", //4d3h2m1s
            "%d'd'%h'h'%m'm'",      //4d3h2m
            "%d'd'%h'h'%s's'",      //4d3h  1s
            "%d'd'%h'h'",           //4d3h
            "%d'd'%m'm'%s's'",      //4d  2m1s
            "%d'd'%m'm'",           //4d  2m
            "%d'd'%s's'",           //4d    1s
            "%d'd'",                //4d
            "%h'h'%m'm'%s's'",      //  3h2m1s
            "%h'h'%m'm'",           //  3h2m
            "%h'h'%s's'",           //  3h  1s
            "%h'h'",                //  3h
            "%m'm'%s's'",           //    2m1s
            "%m'm'",                //    2m
            "%s's'",                //      1s
        };

        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
            => TimeSpan.TryParseExact(input.ToLowerInvariant(), Formats, CultureInfo.InvariantCulture, out var timeSpan)
                ? Task.FromResult(TypeReaderResult.FromSuccess(timeSpan))
                : Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Failed to parse TimeSpan"));

        public static TimeSpan? ParseTime(string s)
            => TimeSpan.TryParseExact(s.ToLowerInvariant(), Formats, CultureInfo.InvariantCulture, out var timeSpan)
                ? timeSpan
                : null;
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
