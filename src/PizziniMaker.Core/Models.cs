namespace PizziniMaker.Core;

public enum ActivityType
{
    Scheda,
    Qu,
    FeriePermesso,
    Malattia,
    Infortunio
}

public sealed class ActivityEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public ActivityType Type { get; set; } = ActivityType.Scheda;

    public int Minutes { get; set; } = 30;

    public string WorkCardNumber { get; set; } = string.Empty;

    public bool IsCad { get; set; } = true;

    public bool IsPaperRecorded { get; set; }
}

public sealed class WorkDay
{
    public DateOnly Date { get; set; }

    public List<ActivityEntry> Entries { get; set; } = [];

    public bool IsConfirmed { get; set; }

    public DateTimeOffset LastModifiedAt { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }

    public int TotalMinutes => Entries.Sum(entry => entry.Minutes);

    public static WorkDay CreateNew(DateOnly date) => new()
    {
        Date = date,
        Entries = [new ActivityEntry()]
    };
}

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;

    public string EmployeeName { get; set; } = string.Empty;

    public DateOnly TrackingStartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public bool AlwaysOnTop { get; set; }

    public bool LaunchAtStartup { get; set; }
}
