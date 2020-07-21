using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;

namespace BotCatMaxy.Models
{
    public class UserRef
    {
        public readonly SocketGuildUser gUser;
        public readonly SocketUser user;
        public readonly ulong ID;

        public UserRef(SocketGuildUser gUser)
        {
            this.gUser = gUser;
            user = gUser;
            ID = gUser.Id;
        }

        public UserRef(SocketUser user)
        {
            this.user = user;
            ID = user.Id;
        }

        public UserRef(ulong ID) => this.ID = ID;

        public UserRef(UserRef userRef, SocketGuild guild)
        {
            user = userRef.user;
            ID = userRef.ID;
            gUser = guild.GetUser(ID);
        }
    }
}
