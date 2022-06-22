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
        => TempMuteWarnAsync(userRef, time, reason, size);

    [Command("tempmutewarn")]
    [Summary("Temporarily assigns a muted role to a user, and warns them with a reason.")]
    [Alias("tmutewarn", "temp-mutewarn", "warntmute", "tempmuteandwarn", "tmw")]
    [RequireContext(ContextType.Guild)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [RequireUserPermission(GuildPermission.KickMembers)]
    public Task<RuntimeResult> TempMuteWarnUser([RequireHierarchy] UserRef userRef, TimeSpan time, [Remainder] string reason)
        => TempMuteWarnAsync(userRef, time, reason, 1);

    [Command("tempmute", RunMode = RunMode.Async)]
    [Summary("Temporarily mutes a user in text channels.")]
    [Alias("tmute", "temp-mute")]
    [RequireContext(ContextType.Guild)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [RequireUserPermission(GuildPermission.KickMembers)]
    public async Task<RuntimeResult> TempMuteUser([RequireHierarchy] UserRef userRef, TimeSpan time, [Remainder] string reason)
        => await TempMuteAsync(userRef, time, reason);

    public async Task<CommandResult> TempMuteAsync(UserRef userRef, TimeSpan time, string reason)
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
        var tempMute = new TempMute(time, reason, userRef.ID);
        var checkResult = await ConfirmNoTempAct(actions.tempMutes, tempMute, userRef);
        if (checkResult?.result != null)
            return checkResult.Value.result;
        if (checkResult?.action != null)
            actions.tempMutes.Remove(checkResult.Value.action);

        actions.tempMutes.Add(tempMute);
        actions.SaveToFile();
        if (userRef.GuildUser != null)
            await userRef.GuildUser.AddRoleAsync(role);
        string execLink = Context.Message.GetJumpUrl();
        string? logLink = await DiscordLogging.LogTempAct(Context.Guild, Context.User, userRef, "mut", reason, execLink, time);
        logLink ??= execLink;
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
        userRef.ID.RecordAct(Context.Guild, tempMute, "tempmute", logLink);
        return CommandResult.FromSuccess($"Temporarily muted {userRef.Mention()} for {time.LimitedHumanize(3)} because of `{reason}`.", logLink: logLink);
    }

    public async Task<RuntimeResult> TempMuteWarnAsync(UserRef userRef, TimeSpan time, string reason, float warnSize)
    {
        if (warnSize is > 999f or < 0.01f)
            return CommandResult.FromError("The infraction size must be between `0.01` and `999`.");

        var muteResult = await TempMuteAsync(userRef, time, reason);
        if (muteResult.IsSuccess)
        {
            var warnResult = await userRef.Warn(warnSize, reason, Context.Channel as ITextChannel, muteResult.LogLink ?? Context.Message.GetJumpUrl());
            string result = $"Temporarily muted and warned {userRef.Mention()} for `{time.LimitedHumanize(3)}` because of `{reason}`";
            if (warnResult.success == false)
                result = $"{result}, but warn failed. {warnResult.description}";
            muteResult = CommandResult.FromSuccess(result);
        }
        return muteResult;
    }
}