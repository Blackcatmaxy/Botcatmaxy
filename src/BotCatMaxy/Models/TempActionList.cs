using BotCatMaxy.Data;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using BotCatMaxy.Services.TempActions;

namespace BotCatMaxy.Models
{
    public class TempActionList : DataObject
    {
        public List<TempBan> tempBans = new();
        public List<TempMute> tempMutes = new();
    }
}
