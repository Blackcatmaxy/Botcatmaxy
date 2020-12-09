using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BotCatMaxy
{
    public static class NotifyUtilities
    {
        public static async Task<bool> TryNotify(this IUser user, string message)
        {
            try
            {
                var sentMessage = await user?.SendMessageAsync(message);
                if (sentMessage == null) return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> TryNotify(this IUser user, Embed embed)
        {
            try
            {
                var sentMessage = await user?.SendMessageAsync(embed: embed);
                if (sentMessage == null) return false;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
