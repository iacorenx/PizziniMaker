using PizziniMaker.Core;

namespace PizziniMaker.Tests;

public sealed class DeliveryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PizziniMakerTests", Guid.NewGuid().ToString("N"));
    private readonly PizziniStore _store;
    private static readonly DateOnly Date = new(2026, 11, 16);

    public DeliveryTests() => _store = new PizziniStore(_root);

    private WorkDay CreateConfirmedDay(DateOnly date)
    {
        var day = WorkDay.CreateNew(date);
        day.Entries[0].WorkCardNumber = "8000";
        day.Entries[0].Minutes = 480;
        _store.SaveDay(day);
        return _store.ConfirmDay(date, "Dipendente");
    }

    [Fact]
    public void DayWithoutDeliveryFields_IsNotDeliveredAndNeedsPrinting()
    {
        Directory.CreateDirectory(_store.DataDirectory);
        File.WriteAllText(_store.GetDayFilePath(Date), """
            {"date":"2026-11-16","entries":[{"type":0,"minutes":480,"workCardNumber":"8000","isCad":true}],
             "isConfirmed":true,"allowNonWorkingDay":false,"revision":2}
            """);

        var day = _store.LoadExistingDay(Date)!;

        Assert.Null(day.DeliveredAt);
        Assert.Equal(DeliveryStatus.NotDelivered, PrintDelivery.GetStatus(day));
        Assert.True(PrintDelivery.NeedsPrinting(day));
    }

    [Fact]
    public void MarkDelivered_SetsStatusWithoutChangingRevision()
    {
        var confirmed = CreateConfirmedDay(Date);

        var marked = _store.MarkDelivered([Date]);
        var day = _store.LoadExistingDay(Date)!;

        Assert.Equal([Date], marked);
        Assert.NotNull(day.DeliveredAt);
        Assert.Equal(DeliveryStatus.Delivered, PrintDelivery.GetStatus(day));
        Assert.False(PrintDelivery.NeedsPrinting(day));
        Assert.Equal(confirmed.Revision, day.Revision);
        Assert.Equal(confirmed.LastModifiedAt, day.LastModifiedAt);
        Assert.Empty(_store.GetDaysToPrint());
    }

    [Fact]
    public void MarkDelivered_IgnoresDraftsAndMissingDays()
    {
        var draft = WorkDay.CreateNew(Date);
        draft.Entries[0].WorkCardNumber = "8000";
        draft.Entries[0].Minutes = 480;
        _store.SaveDay(draft);

        var marked = _store.MarkDelivered([Date, Date.AddDays(1)]);

        Assert.Empty(marked);
        Assert.Null(_store.LoadExistingDay(Date)!.DeliveredAt);
        Assert.False(PrintDelivery.NeedsPrinting(_store.LoadExistingDay(Date)));
    }

    [Fact]
    public void ChangeAfterDelivery_RequiresReprinting()
    {
        CreateConfirmedDay(Date);
        _store.MarkDelivered([Date]);

        _store.ReopenDay(Date);
        var reopened = _store.LoadExistingDay(Date)!;
        Assert.Equal(DeliveryStatus.ChangedAfterDelivery, PrintDelivery.GetStatus(reopened));

        reopened.Entries[0].WorkCardNumber = "8001";
        _store.SaveDay(reopened);
        var confirmed = _store.ConfirmDay(Date, "Dipendente");

        Assert.NotNull(confirmed.DeliveredAt);
        Assert.Equal(DeliveryStatus.ChangedAfterDelivery, PrintDelivery.GetStatus(confirmed));
        Assert.True(PrintDelivery.NeedsPrinting(confirmed));
        Assert.Equal([Date], _store.GetDaysToPrint());
    }

    [Fact]
    public void ReopenAndConfirmWithoutChanges_StaysDelivered()
    {
        CreateConfirmedDay(Date);
        _store.MarkDelivered([Date]);

        _store.ReopenDay(Date);
        var confirmed = _store.ConfirmDay(Date, "Dipendente");

        Assert.Equal(DeliveryStatus.Delivered, PrintDelivery.GetStatus(confirmed));
    }

    [Fact]
    public void PaperCheckOrTrasfertaAfterDelivery_BehaveAsExpected()
    {
        var confirmed = CreateConfirmedDay(Date);
        _store.MarkDelivered([Date]);

        _store.UpdatePaperStatus(Date, confirmed.Entries[0].Id, true, "Altro nome");
        Assert.Equal(DeliveryStatus.Delivered, PrintDelivery.GetStatus(_store.LoadExistingDay(Date)));

        var day = _store.LoadExistingDay(Date)!;
        day.IsTrasferta = true;
        Assert.Equal(DeliveryStatus.ChangedAfterDelivery, PrintDelivery.GetStatus(day));
    }

    [Fact]
    public void ClearDelivered_RestoresNotDelivered()
    {
        CreateConfirmedDay(Date);
        _store.MarkDelivered([Date]);

        var cleared = _store.ClearDelivered([Date, Date.AddDays(1)]);
        var day = _store.LoadExistingDay(Date)!;

        Assert.Equal([Date], cleared);
        Assert.Null(day.DeliveredAt);
        Assert.Null(day.DeliveredFingerprint);
        Assert.True(PrintDelivery.NeedsPrinting(day));
    }

    [Fact]
    public void Fingerprint_IgnoresEmployeeNameButNotComment()
    {
        var day = WorkDay.CreateNew(Date);
        day.IsConfirmed = true;
        day.Entries[0].WorkCardNumber = "8000";
        day.Entries[0].Minutes = 480;
        var before = PrintSlipBuilder.GetFingerprint(day);

        day.Entries[0].WorkCardComment = "Baccane";

        Assert.NotEqual(before, PrintSlipBuilder.GetFingerprint(day));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
