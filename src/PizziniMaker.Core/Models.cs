namespace PizziniMaker.Core;

public enum ActivityType
{
    Scheda,
    Qu,
    FeriePermesso,
    Malattia,
    Infortunio,
    Custom,
    Viaggio,
    Mandato,
    Preventivo
}

public sealed class CustomActivityDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Icon { get; set; } = "📌";
}

public sealed class ActivityEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public ActivityType Type { get; set; } = ActivityType.Scheda;

    public int Minutes { get; set; } = 30;

    public string WorkCardNumber { get; set; } = string.Empty;

    public string WorkCardComment { get; set; } = string.Empty;

    public bool IsCad { get; set; } = true;

    public bool IsPaperRecorded { get; set; }

    public string CustomActivityName { get; set; } = string.Empty;

    public string CustomActivityIcon { get; set; } = string.Empty;
}

public sealed class WorkDay
{
    public DateOnly Date { get; set; }

    public List<ActivityEntry> Entries { get; set; } = [];

    public bool IsConfirmed { get; set; }

    public bool AllowNonWorkingDay { get; set; }

    public bool IsTrasferta { get; set; }

    public DateTimeOffset LastModifiedAt { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }

    /// <summary>Contatore delle modifiche salvate, base per la futura sincronizzazione con la segreteria.</summary>
    public int Revision { get; set; }

    /// <summary>Quando il foglietto del giorno è stato stampato e consegnato (null = non consegnato).</summary>
    public DateTimeOffset? DeliveredAt { get; set; }

    /// <summary>Impronta del foglietto consegnato: se il contenuto cambia, il giorno va ristampato.</summary>
    public string? DeliveredFingerprint { get; set; }

    public int TotalMinutes => Entries.Sum(entry => entry.Minutes);

    public static WorkDay CreateNew(DateOnly date) => new()
    {
        Date = date,
        Entries = [new ActivityEntry()]
    };
}

public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 4;

    public const string LegacyTrasfertaActivityName = "TRASFERTA";

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Identificativo stabile del dipendente (schema 4), per la futura sincronizzazione.</summary>
    public Guid EmployeeId { get; set; } = Guid.NewGuid();

    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>Tariffa di rimborso chilometrico in centesimi di euro per km (0 = non impostata).</summary>
    public int KmRateCents { get; set; }

    public bool CheckUpdatesAtStartup { get; set; } = true;

    public DateOnly? LastUpdateCheck { get; set; }

    public DateOnly TrackingStartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public bool AlwaysOnTop { get; set; }

    public bool LaunchAtStartup { get; set; }

    public List<CustomActivityDefinition> CustomActivities { get; set; } = CreateDefaultCustomActivities();

    public static List<CustomActivityDefinition> CreateDefaultCustomActivities() =>
    [
        new() { Name = "FORMAZIONE", Icon = "🎓" }
    ];
}
