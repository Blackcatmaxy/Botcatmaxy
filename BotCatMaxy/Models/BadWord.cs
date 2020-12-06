using BotCatMaxy.Data;
using Discord;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BotCatMaxy.Models
{
    [BsonIgnoreExtraElements]
    public record BadWord
    {
        public string Word { get; init; }
        public string Euphemism { get; init; }
        public float Size { get; init; } = 0.5f;
        public bool PartOfWord { get; init; } = true;
    }

    public class BadWordList : DataObject
    {
        public List<BadWord> badWords = new();
    }

    public class BadWords
    {
        public List<BadWord> all;
        public List<BadWord> onlyAlone;
        public List<BadWord> insideWords;
        public List<List<BadWord>> grouped;

        public BadWords(IGuild guild)
        {
            if (guild == null) throw new ArgumentNullException();
            all = guild.LoadFromFile<BadWordList>()?.badWords;
            if (all == null) return;
            onlyAlone = new List<BadWord>();
            insideWords = new List<BadWord>();
            grouped = new List<List<BadWord>>();

            foreach (BadWord badWord in all)
            {
                if (badWord.PartOfWord) insideWords.Add(badWord);
                else onlyAlone.Add(badWord);

                List<BadWord> group = grouped.Find(list => list.FirstOrDefault() != null && list.First().Euphemism == badWord.Euphemism);
                if (group != null)
                {
                    group.Add(badWord);
                }
                else
                {
                    grouped.Add(new List<BadWord> { badWord });
                }
            }
        }
    }
}
