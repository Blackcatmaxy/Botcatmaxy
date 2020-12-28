using BotCatMaxy.Models;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading.Tasks;

namespace BotCatMaxy.TypeReaders
{
    public class UserRefTypeReader : TypeReader
    {
        public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            SocketGuildUser gUserResult = null;
            SocketUser userResult;

            //By Mention (1.0)
            if (MentionUtils.TryParseUser(input, out var id))
            {
                if (context.Guild != null)
                    gUserResult = await context.Guild.GetUserAsync(id, CacheMode.AllowDownload) as SocketGuildUser;
                if (gUserResult != null)
                    return TypeReaderResult.FromSuccess(new UserRef(gUserResult));
                else
                    userResult = await context.Client.GetUserAsync(id, CacheMode.AllowDownload) as SocketUser;
                if (userResult != null)
                    return TypeReaderResult.FromSuccess(new UserRef(userResult));
                else
                    return TypeReaderResult.FromSuccess(new UserRef(id));
            }

            //By Id (0.9)
            if (ulong.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out id))
            {
                if (context.Guild != null)
                    gUserResult = await context.Guild.GetUserAsync(id, CacheMode.AllowDownload) as SocketGuildUser;
                if (gUserResult != null)
                    return TypeReaderResult.FromSuccess(new UserRef(gUserResult));
                else
                    userResult = await context.Client.GetUserAsync(id, CacheMode.AllowDownload) as SocketUser;
                if (userResult != null)
                    return TypeReaderResult.FromSuccess(new UserRef(userResult));
                else
                    return TypeReaderResult.FromSuccess(new UserRef(id));
            }

            return TypeReaderResult.FromError(CommandError.ObjectNotFound, "User not found.");
        }
    }
}
