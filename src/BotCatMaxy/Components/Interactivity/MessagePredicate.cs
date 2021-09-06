using Discord;

namespace BotCatMaxy.Components.Interactivity
{
    public class MessagePredicate
    {
        public IMessage Message { get; init; }

        public bool Evaluate(IMessage other)
        {
            return Message.Id == other.Id;
        }
    }
}