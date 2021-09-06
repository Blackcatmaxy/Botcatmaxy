using System;
using Discord;
using Discord.WebSocket;

namespace BotCatMaxy.Components.Interactivity
{
    /// <summary>
    /// A class containing functions around matching messages,
    /// mostly to be (re)usable as filters in Interactivity functions.
    /// </summary>
    public class InteractivePredicate
    {
        public InteractivePredicate(IMessage message, bool requireAuthor = true)
        {
            _message = message;
            _requireAuthor = requireAuthor;
        }

        private readonly IMessage _message;
        private readonly bool _requireAuthor;

        /// <summary>
        /// Evaluate a message's channel.
        /// </summary>
        /// <returns>If message originated from same channel as stored message.</returns>
        public bool EvaluateChannel(SocketMessage message)
        {
            //Won't evaluate right hand side if matchAuthor is !false since || is lazy
            return message.Channel.Id == _message.Channel.Id && (!_requireAuthor || message.Author.Id == _message.Author.Id);
        }
    }
}