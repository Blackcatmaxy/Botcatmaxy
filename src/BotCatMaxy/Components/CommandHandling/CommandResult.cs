using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BotCatMaxy.Startup;
using Discord;
using Discord.Interactions;

namespace BotCatMaxy.Components.CommandHandling;
/// <summary>
/// A wrapper of <seealso cref="RuntimeResult"/> for communicating the result of a command in the <seealso cref="TextCommandHandler"/>
/// </summary>
#nullable enable
public class CommandResult : Discord.Commands.IResult, Discord.Interactions.IResult
{
    public readonly Embed? Embed;
    public readonly string? LogLink;
    private string _reason;
    private readonly CommandError? _error;
    private bool _isSuccess;

    public CommandResult(CommandError? error, string reason, Embed? embed = null, string? logLink = null)
    {
        _error = error;
        _reason = reason;
        Embed = embed;
        LogLink = logLink;
    }

    public static CommandResult FromError(string reason, Embed? embed = null)
        => new(CommandError.Unsuccessful, reason, embed);

    public static CommandResult FromSuccess(string reason, Embed? embed = null, string? logLink = null)
        => new(null, reason, embed, logLink);

    public InteractionCommandError? Error => _error switch
    {
        CommandError.ParseFailed => InteractionCommandError.ParseFailed,
        CommandError.Exception => InteractionCommandError.Exception,
        CommandError.Unsuccessful => InteractionCommandError.Unsuccessful,
        CommandError.UnknownCommand => InteractionCommandError.UnknownCommand,
        _ => null
    };

    CommandError? Discord.Commands.IResult.Error => _error;
    public string ErrorReason => _reason;

    public bool IsSuccess => _error == null;

    // Library devs what have you made me do...
    public static implicit operator Discord.Commands.RuntimeResult(CommandResult result)
    {
        var hate = result as Discord.Commands.IResult;
        return new TextResult(hate.Error, hate.ErrorReason, result.Embed);
    }

    public static implicit operator Discord.Interactions.RuntimeResult(CommandResult result)
    {
        var hate = result as Discord.Interactions.IResult;
        return new InteractionResult(hate.Error, hate.ErrorReason, result.Embed);
    }
}

public class TextResult : Discord.Commands.RuntimeResult
{
    public Embed? Embed { get; }

    public TextResult(CommandError? error, string reason, Embed? embed = null) : base(error, reason)
    {
        Embed = embed;
    }
}

public class InteractionResult : Discord.Interactions.RuntimeResult
{
    public Embed? Embed { get; }

    public InteractionResult(InteractionCommandError? error, string reason, Embed? embed = null) : base(error, reason)
    {
        Embed = embed;
    }
}