using System;
using System.Threading.Tasks;
using BotCatMaxy.Components.Interactivity;
using BotCatMaxy.Data;
using BotCatMaxy.Models;
using BotCatMaxy.Moderation;
using BotCatMaxy.Services.Logging;
using BotCatMaxy.Services.TempActions;
using Discord;
using Discord.Commands;

#nullable enable
namespace BotCatMaxy.Components.Moderation;

[Name("Moderation")]
public class TempBanCommands : InteractiveModule
{
    public TempBanCommands(IServiceProvider service) : base(service) { }

    [Command("tempban")]
    [Summary("Temporarily bans a user.")]
    [Alias("tban", "temp-ban")]
    [RequireContext(ContextType.Guild)]
    [RequireBotPermission(GuildPermission.BanMembers)]
    [RequireUserPermission(GuildPermission.KickMembers)]
    public async Task<RuntimeResult> TempBanUser([RequireHierarchy] UserRef userRef, TimeSpan time, [Remainder] string reason)
        => await TempBanAsync(userRef, time, reason);

    [Command("tempbanwarn")]
    [Summary("Temporarily bans a user, and warns them with a reason.")]
    [Alias("tbanwarn", "temp-banwarn", "tempbanandwarn", "tbw")]
    [RequireContext(ContextType.Guild)]
    [RequireBotPermission(GuildPermission.BanMembers)]
    [RequireUserPermission(GuildPermission.KickMembers)]
    public Task<RuntimeResult> TempBanWarnUser([RequireHierarchy] UserRef userRef, TimeSpan time, [Remainder] string reason)
        => TempBanWarnAsync(userRef, time, reason, 1);

    [Command("tempbanwarn")]
    [Alias("tbanwarn", "temp-banwarn", "tempbanwarn", "warntempban", "tbw")]
    [Summary("Temporarily bans a user, and warns them with a specific size along with a reason.")]
    [RequireContext(ContextType.Guild)]
    [RequireBotPermission(GuildPermission.BanMembers)]
    [RequireUserPermission(GuildPermission.KickMembers)]
    public Task<RuntimeResult> TempBanWarnUser([RequireHierarchy] UserRef userRef, TimeSpan time, float size, [Remainder] string reason)
        => TempBanWarnAsync(userRef, time, reason, size);

    public async Task<CommandResult> TempBanAsync(UserRef userRef, TimeSpan time, string reason)
    {
        if (time.TotalMinutes < 1)
            return CommandResult.FromError("Can't temp-ban for less than a minute.");

        ModerationSettings settings = Context.Guild.LoadFromFile<ModerationSettings>(false);

        if (!(Context.Message.Author as IGuildUser).HasAdmin())
        {
            if (settings?.maxTempAction != null && time > settings.maxTempAction)
            {
                return CommandResult.FromError("You are not allowed to punish for that long.");
            }
        }
        var actions = Context.Guild.LoadFromFile<TempActionList>(true);
        var tempBan = new TempBan(time, reason, userRef.ID);
        var checkResult = await ConfirmNoTempAct(actions.tempBans, tempBan, userRef);
        if (checkResult?.result != null)
            return checkResult.Value.result;
        if (checkResult?.action != null)
            actions.tempBans.Remove(checkResult.Value.action);

        actions.tempBans.Add(tempBan);
        actions.SaveToFile();
        if (userRef.User != null)
        {
            try
            {
                await userRef.User.Notify($"tempbanned for {time.LimitedHumanize()}", reason, Context.Guild, Context.Message.Author, appealLink: settings.appealLink);
            }
            catch (Exception e)
            {
                if (e is NullReferenceException) await new LogMessage(LogSeverity.Error, "TempAct", "Something went wrong notifying person", e).Log();
            }
        }
        await Context.Guild.AddBanAsync(userRef.ID, reason: reason);
        string execLink = Context.Message.GetJumpUrl();
        string? logLink = await DiscordLogging.LogTempAct(Context.Guild, Context.User, userRef, "bann", reason, execLink, time);
        logLink ??= execLink;
        userRef.ID.RecordAct(Context.Guild, tempBan, "tempban", logLink);
        return CommandResult.FromSuccess($"Temporarily banned {userRef.Mention()} for {time.LimitedHumanize(3)} because of {reason}.", logLink: logLink);
    }

    public async Task<RuntimeResult> TempBanWarnAsync(UserRef userRef, TimeSpan time, string reason, float warnSize)
    {
        if (warnSize is > 999f or < 0.01f)
            return CommandResult.FromError("The infraction size must be between `0.01` and `999`.");
        var banResult = await TempBanAsync(userRef, time, reason);
        if (banResult.IsSuccess)
        {
            var warnResult = await userRef.Warn(warnSize, reason, Context.Channel as ITextChannel, banResult.LogLink ?? Context.Message.GetJumpUrl());
            string result = $"Temporarily banned and warned {userRef.Mention()} for `{time.LimitedHumanize(3)}` because of `{reason}`";
            if (warnResult.success == false)
                result = $"{result}, but warn failed. {warnResult.description}";
            banResult = CommandResult.FromSuccess(result);
        }
        return banResult;
    }
}