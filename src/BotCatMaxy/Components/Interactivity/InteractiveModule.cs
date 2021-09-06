using System;
using Discord.Commands;
using Fergun.Interactive;

namespace BotCatMaxy.Components.Interactivity
{
    /// <summary>
    /// Wrapper around <seealso cref="ModuleBase{T}"/> with T as <see cref="ICommandContext"/>
    /// and including an <seealso cref="InteractivityService"/> property
    /// </summary>
    public class InteractiveModule : ModuleBase<ICommandContext>
    {
        /// <summary>
        /// This constructor is required for DI to work in both test environment and release without
        /// mocking of <seealso cref="Discord.WebSocket.BaseSocketClient"/>
        /// </summary>
        public InteractiveModule(IServiceProvider service) : base()
        {
            Interactivity = (InteractiveService)service.GetService(typeof(InteractiveService));
        }

        protected InteractiveService Interactivity { get; }
    }
}