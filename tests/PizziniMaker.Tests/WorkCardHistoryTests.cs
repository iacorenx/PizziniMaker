using PizziniMaker.Core;

namespace PizziniMaker.Tests;

public sealed class WorkCardHistoryTests
{
    [Fact]
    public void Search_ByCommentPrefix_IsCaseInsensitive()
    {
        var history = CreateHistory(("8092", "Baccane", 10), ("1234", "Officina", 12));

        var result = history.Search("bac");

        var entry = Assert.Single(result);
        Assert.Equal("8092", entry.Number);
        Assert.Equal("Baccane", entry.Comment);
        Assert.Equal("8092 — Baccane", entry.DisplayText);
    }

    [Fact]
    public void Search_ByNumber_FindsEntries()
    {
        var history = CreateHistory(("8092", "Baccane", 10), ("8100", "Magazzino", 11), ("1234", "Officina", 12));

        var result = history.Search("80");

        Assert.Equal(["8092"], result.Select(entry => entry.Number));
    }

    [Fact]
    public void Search_SameRelevance_OrdersByMostRecentUse()
    {
        var history = CreateHistory(("1111", "Cantiere nord", 3), ("2222", "Cantiere sud", 15), ("3333", "Cantiere est", 9));

        var result = history.Search("cant");

        Assert.Equal(["2222", "3333", "1111"], result.Select(entry => entry.Number));
    }

    [Fact]
    public void Search_PrefixBeatsContains()
    {
        var history = CreateHistory(("1111", "Vecchia Baccane", 20), ("2222", "Baccane", 1), ("3333", "Abaccanesco", 25));

        var result = history.Search("bacc");

        Assert.Equal(["2222", "1111", "3333"], result.Select(entry => entry.Number));
    }

    [Fact]
    public void RecordDay_KeepsOneEntryPerNumberWithLatestComment()
    {
        var history = new WorkCardHistory();
        history.RecordDay(Day(new DateOnly(2026, 11, 2), ("8092", "Baccane")));
        history.RecordDay(Day(new DateOnly(2026, 11, 9), ("8092", "Baccane cantiere B")));
        history.RecordDay(Day(new DateOnly(2026, 11, 5), ("8092", "Commento più vecchio")));
        history.RecordDay(Day(new DateOnly(2026, 11, 10), ("8092", "")));

        var entry = Assert.Single(history.Entries);
        Assert.Equal("Baccane cantiere B", entry.Comment);
        Assert.Equal(new DateOnly(2026, 11, 10), entry.LastUsed);
        Assert.Equal(4, entry.UseCount);
    }

    [Fact]
    public void RecordDay_IgnoresInvalidNumbersAndOtherActivities()
    {
        var history = new WorkCardHistory();
        var day = Day(new DateOnly(2026, 11, 2), ("80", "Incompleta"), ("8092", "Valida"));
        day.Entries.Add(new ActivityEntry { Type = ActivityType.Qu, Minutes = 60 });

        history.RecordDay(day);

        Assert.Equal(["8092"], history.Entries.Select(entry => entry.Number));
    }

    [Fact]
    public void Merge_AddsMissingEntriesAndKeepsNewestComment()
    {
        var current = CreateHistory(("8092", "Baccane", 10), ("1234", "", 1));
        var imported = CreateHistory(("8092", "Vecchio commento", 2), ("1234", "Officina", 1), ("5555", "Deposito", 5));

        current.Merge(imported);

        Assert.Equal(3, current.Entries.Count);
        Assert.Equal("Baccane", current.Find("8092")!.Comment);
        Assert.Equal("Officina", current.Find("1234")!.Comment);
        Assert.Equal("Deposito", current.Find("5555")!.Comment);
    }

    [Fact]
    public void Rebuild_UsesOnlyConfirmedDays()
    {
        var confirmed = Day(new DateOnly(2026, 11, 2), ("8092", "Baccane"));
        var draft = Day(new DateOnly(2026, 11, 3), ("9999", "Bozza"));
        draft.IsConfirmed = false;

        var history = WorkCardHistory.Rebuild([confirmed, draft]);

        Assert.Equal(["8092"], history.Entries.Select(entry => entry.Number));
    }

    [Fact]
    public void Upsert_ValidatesNumberAndTrimsComment()
    {
        var history = new WorkCardHistory();

        history.Upsert("8092", "  Baccane  ");
        history.Upsert("8092", "Baccane nuovo");

        Assert.Equal("Baccane nuovo", Assert.Single(history.Entries).Comment);
        Assert.Throws<ArgumentException>(() => history.Upsert("80a2", "x"));
    }

    private static WorkCardHistory CreateHistory(params (string Number, string Comment, int Day)[] entries) => new()
    {
        Entries = entries.Select(entry => new WorkCardHistoryEntry
        {
            Number = entry.Number,
            Comment = entry.Comment,
            LastUsed = new DateOnly(2026, 11, entry.Day),
            UseCount = 1
        }).ToList()
    };

    private static WorkDay Day(DateOnly date, params (string Number, string Comment)[] cards) => new()
    {
        Date = date,
        IsConfirmed = true,
        Entries = cards.Select(card => new ActivityEntry
        {
            WorkCardNumber = card.Number,
            WorkCardComment = card.Comment,
            Minutes = 60
        }).ToList()
    };
}
