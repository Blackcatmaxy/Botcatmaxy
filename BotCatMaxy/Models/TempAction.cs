using BotCatMaxy.Data;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace BotCatMaxy.Models
{
    public record TempAct
    {
        public TempAct(ulong userID, TimeSpan length, string reason)
        {
            User = userID;
            Reason = reason;
            DateBanned = DateTime.UtcNow;
            Length = length;
        }
        public TempAct(UserRef userRef, TimeSpan length, string reason)
        {
            User = userRef.ID;
            Reason = reason;
            DateBanned = DateTime.UtcNow;
            Length = length;
        }

        public DateTime End => DateBanned.Add(Length);

        public string Reason { get; init; }
        public ulong User { get; init; }
        public TimeSpan Length { get; init; }
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime DateBanned { get; init; }
    }

    public enum TempActType
    {
        TempMute,
        TempBan
    }

    public class TempActionList : DataObject
    {
        public List<TempAct> tempBans = new();
        public List<TempAct> tempMutes = new();
    }

    public record TypedTempAct : TempAct
    {
        public TempActType Type { get; }

        public TypedTempAct(TempAct tempAct, TempActType type) : base(tempAct.User, tempAct.Length, tempAct.Reason)
        {
            DateBanned = tempAct.DateBanned;
            Type = type;
        }
    }
}
