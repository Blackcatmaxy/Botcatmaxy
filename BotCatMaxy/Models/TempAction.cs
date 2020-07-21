using BotCatMaxy.Data;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace BotCatMaxy.Models
{
    public class TempAct
    {
        public TempAct(ulong userID, TimeSpan length, string reason)
        {
            user = userID;
            this.reason = reason;
            dateBanned = DateTime.UtcNow;
            this.length = length;
        }
        public TempAct(UserRef userRef, TimeSpan length, string reason)
        {
            user = userRef.ID;
            this.reason = reason;
            dateBanned = DateTime.UtcNow;
            this.length = length;
        }

        public DateTime End => dateBanned.Add(length);

        public string reason;
        public ulong user;
        public TimeSpan length;
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime dateBanned;
    }

    public enum TempActType
    {
        TempMute,
        TempBan
    }

    public class TempActionList : DataObject
    {
        [BsonId]
        public string ID = "TempActionList";
        public List<TempAct> tempBans = new List<TempAct>();
        public List<TempAct> tempMutes = new List<TempAct>();
    }

    public class TypedTempAct : TempAct
    {
        public TempActType type;

        public TypedTempAct(TempAct tempAct, TempActType type) : base(tempAct.user, tempAct.length, tempAct.reason)
        {
            dateBanned = tempAct.dateBanned;
            this.type = type;
        }
    }
}
