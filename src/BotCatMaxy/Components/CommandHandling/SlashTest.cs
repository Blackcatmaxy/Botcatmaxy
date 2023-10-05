using System;
using System.Threading.Tasks;
using BotCatMaxy.Components.Interactivity;
using Discord;
using Discord.Interactions;

namespace BotCatMaxy.Components.CommandHandling;

public class SlashTest : InteractionModuleBase
{
    [SlashCommand("ping", "pong")]
    public async Task Ping(IUser user)
    {
        await DeferAsync();
    }
}