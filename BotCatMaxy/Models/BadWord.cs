using BotCatMaxy.Data;
using Discord;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BotCatMaxy.Models
{
    [BsonIgnoreExtraElements]
    public class BadWord
    {
        public string word;
        public string euphemism;
        public float size = 0.5f;
        public bool partOfWord = true;
        public object moreWords;
    }

    [BsonIgnoreExtraElements]
    public class BadWordList : DataObject
    {
        [BsonId]
        public string Id = "BadWordList";
        public List<BadWord> badWords = new List<BadWord>();
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
                if (badWord.partOfWord) insideWords.Add(badWord);
                else onlyAlone.Add(badWord);

                List<BadWord> group = grouped.Find(list => list.FirstOrDefault() != null && list.First().euphemism == badWord.euphemism);
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
