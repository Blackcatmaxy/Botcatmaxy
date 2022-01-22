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
public class TempMuteCommands : InteractiveModule
{
    public TempMuteCommands (IServiceProvider service) : base(service) { }

    [Command("tempmutewarn")]
    [Summary("Temporarily assigns a muted role to a user, and warns them with a specific size along with a reason.")]
    [Alias("tmutewarn", "temp-mutewarn", "warntmute", "tempmuteandwarn", "tmw")]
    [RequireContext(ContextType.Guild)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [RequireUserPermission(GuildPermission.KickMembers)]
    public Task<RuntimeResult> TempMuteWarnUser([RequireHierarchy] UserRef userRef, TimeSpan time, float size, [Remainder] string reason)
        => TempMuteWarn(userRef, time, reason, size);

    [Command("tempmutewarn")]
    [Summary("Temporarily assigns a muted role to a user, and warns them with a reason.")]
    [Alias("tmutewarn", "temp-mutewarn", "warntmute", "tempmuteandwarn", "tmw")]
    [RequireContext(ContextType.Guild)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [RequireUserPermission(GuildPermission.KickMembers)]
    public Task<RuntimeResult> TempMuteWarnUser([RequireHierarchy] UserRef userRef, TimeSpan time, [Remainder] string reason)
        => TempMuteWarn(userRef, time, reason, 1);

    [Command("tempmute", RunMode = RunMode.Async)]
    [Summary("Temporarily mutes a user in text channels.")]
    [Alias("tmute", "temp-mute")]
    [RequireContext(ContextType.Guild)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [RequireUserPermission(GuildPermission.KickMembers)]
    public async Task<RuntimeResult> TempMuteUser([RequireHierarchy] UserRef userRef, TimeSpan time, [Remainder] string reason)
        => await TempMute(userRef, time, reason);

    public async Task<CommandResult> TempMute(UserRef userRef, TimeSpan time, string reason)
    {
        if (time.TotalMinutes < 1)
            return CommandResult.FromError("Can't temp-mute for less than a minute.");
        var settings = Context.Guild.LoadFromFile<ModerationSettings>(false);
        if ((Context.Message.Author as IGuildUser).HasAdmin() == false)
        {
            if (settings?.maxTempAction != null && time > settings.maxTempAction)
                return CommandResult.FromError("You are not allowed to punish for that long.");
        }
        var role = Context.Guild.GetRole(settings?.mutedRole ?? 0);
        if (role == null)
            return CommandResult.FromError("Muted role is null or invalid.");
        var actions = Context.Guild.LoadFromFile<TempActionList>(true);
        var tempMute = new TempMute
        {
            Length = time,
            Reason = reason,
            UserId = userRef.ID
        };
        var checkResult = await ConfirmNoTempAct(actions.tempMutes, tempMute, userRef);
        if (checkResult != null)
            return checkResult;
        actions.tempMutes.Add(tempMute);
        actions.SaveToFile();
        if (userRef.GuildUser != null)
            await userRef.GuildUser.AddRoleAsync(role);
        string? logLink = await DiscordLogging.LogTempAct(Context.Guild, Context.User, userRef, "mut", reason, Context.Message.GetJumpUrl(), time);
        if (userRef.User != null)
        {
            try
            {
                await userRef.User?.Notify($"tempmuted for {time.LimitedHumanize()}", reason, Context.Guild, Context.Message.Author);
            }
            catch (Exception e)
            {
                if (e is NullReferenceException) await new LogMessage(LogSeverity.Error, "TempAct", "Something went wrong notifying person", e).Log();
            }
        }
        userRef.ID.RecordAct(Context.Guild, tempMute, "tempmute", logLink ?? Context.Message.GetJumpUrl());
        return CommandResult.FromSuccess($"Temporarily muted {userRef.Mention()} for {time.LimitedHumanize(3)} because of `{reason}`.", logLink: logLink);
    }

    public async Task<RuntimeResult> TempMuteWarn(UserRef userRef, TimeSpan time, string reason, float warnSize)
    {
        if (warnSize is > 999f or < 0.01f)
            return CommandResult.FromError("The infraction size must be between `0.01` and `999`.");

        var muteResult = await TempMute(userRef, time, reason);
        if (muteResult.IsSuccess)
        {
            var warnResult = await userRef.Warn(warnSize, reason, Context.Channel as ITextChannel, muteResult.LogLink ?? Context.Message.GetJumpUrl());
            string result = $"Temporarily muted {userRef.Mention()} for `{time.LimitedHumanize(3)}` because of `{reason}`";
            if (warnResult.success == false)
                result = $"{result}, but warn failed. {warnResult.description}";
            muteResult = CommandResult.FromSuccess(result);
        }
        return muteResult;
    }
}