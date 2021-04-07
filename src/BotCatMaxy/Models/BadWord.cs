using BotCatMaxy.Data;
using Discord;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BotCatMaxy.Models
{
    /// <summary>
    /// A single banned word
    /// </summary>
    [BsonIgnoreExtraElements]
    public record BadWord(
        string Word = null,
        string Euphemism = null,
        float Size = 0.5f,
        bool PartOfWord = true);

    /// <summary>
    /// A collection of <seealso cref="BadWord"/> to store and load from the database
    /// </summary>
    public class BadWordList : DataObject
    {
        public List<BadWord> badWords = new();
    }

    /// <summary>
    /// A class to load the groups of <seealso cref="BadWord"/>s for display
    /// </summary>
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
