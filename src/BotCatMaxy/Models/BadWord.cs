using BotCatMaxy.Data;
using Discord;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

#nullable enable
namespace BotCatMaxy.Models;

/// <summary>
/// A single banned word
/// </summary>
[BsonIgnoreExtraElements]
public record BadWord(
    string Word,
    string? Euphemism = null,
    float Size = 0.5f,
    bool PartOfWord = true);

/// <summary>
/// A collection of <seealso cref="BadWord"/> to store and load from the database
/// </summary>
public class BadWordList : DataObject
{
    public HashSet<BadWord> badWords = new();
}

// TODO: Considered making this a part of BadWordList, but not sure how to make it cache while still updating on updates?
// Would be a big improvement as this class probably causes the most variability in memory usage.
/// <summary>
/// A class to load the groups of <see cref="BadWord"/>s for display
/// </summary>
public class BadWordSets
{
    public readonly ImmutableHashSet<BadWord> All;
    public readonly ImmutableHashSet<BadWord> OnlyAlone;
    public readonly ImmutableHashSet<BadWord> InsideWords;
    public readonly ImmutableHashSet<BadWord> Euphemisms;
    public readonly ImmutableHashSet<ImmutableHashSet<BadWord>> Grouped;

    public BadWordSets(IGuild guild) : this(guild.LoadFromFile<BadWordList>().badWords)
    {
    }

    public BadWordSets(HashSet<BadWord> badWords)
    {
        All = badWords.ToImmutableHashSet();
        var onlyAlone = new HashSet<BadWord>();
        var insideWords = new HashSet<BadWord>();
        var euphemisms = new HashSet<BadWord>();
        var grouped = new HashSet<ImmutableHashSet<BadWord>>();

        foreach (var badWord in All)
        {
            if (badWord.PartOfWord)
                insideWords.Add(badWord);
            else
                onlyAlone.Add(badWord);

            if (!string.IsNullOrWhiteSpace(badWord.Euphemism))
                euphemisms.Add(badWord);
        }

        OnlyAlone = onlyAlone.ToImmutableHashSet();
        InsideWords = insideWords.ToImmutableHashSet();
        Euphemisms = euphemisms.ToImmutableHashSet();

        foreach (var badword in Euphemisms)
        {
            //TODO: I'm tired of looking at this, could be better
            if (grouped.Any(group =>
                    group.Any(word => word.Euphemism!.Equals(badword.Euphemism, StringComparison.InvariantCultureIgnoreCase))))
                continue;

            var group = All.Where(word => badword.
                   Euphemism!.Equals(word.Euphemism, StringComparison.InvariantCultureIgnoreCase)).ToImmutableHashSet();
            grouped.Add(group);
        }

        Grouped = grouped.ToImmutableHashSet();
    }
}