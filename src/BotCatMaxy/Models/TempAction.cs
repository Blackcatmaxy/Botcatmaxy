using BotCatMaxy.Data;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace BotCatMaxy.Models
{
    /// <summary>
    /// A single temporary action against a user
    /// </summary>
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

    /// <summary>
    /// A collection of <seealso cref="TempAct"/>s to store and load from the database,
    /// entries removed at end of actions by <seealso cref="TempActions"/>
    /// </summary>
    public class TempActionList : DataObject
    {
        public List<TempAct> tempBans = new();
        public List<TempAct> tempMutes = new();
    }

    /// <summary>
    /// A single <seealso cref="TempAct"/> labeled by its <seealso cref="TempActType"/>
    /// </summary>
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
