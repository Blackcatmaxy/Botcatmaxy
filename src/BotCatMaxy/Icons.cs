using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BotCatMaxy
{
    /// <summary>
    /// Emojis to use on Discord to show the status of an action, and to give a general example of what the message is indicating.
    /// </summary>
    public static class Icons
    {
        /// <summary>
        /// An icon indicating one or multiple actions are successful.
        /// </summary>
        public static readonly string Checkmark = "<:bcmCheck:859558128251961385>";

        /// <summary>
        /// An icon indicating an input was malformed, or an action
        /// was unsuccessful.
        /// </summary>
        public static readonly string Error = "<:bcmError:859560960392822805>";

        // Moderative Icons
        /// <summary>
        /// An icon indicating an user has been given an infraction.
        /// </summary>
        public static readonly string Warn = "<:bcmWarn:859574107833630720>";

        /// <summary>
        /// An icon indicating an user was muted.
        /// </summary>
        public static readonly string Mute = "<:bcmMute:859578760378056714>";

        /// <summary>
        /// An icon indicating an user was banned.
        /// </summary>
        public static readonly string Ban = "<:bcmBan:859586248719990805>";
    }
}
