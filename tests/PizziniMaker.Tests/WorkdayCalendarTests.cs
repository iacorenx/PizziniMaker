using PizziniMaker.Core;

namespace PizziniMaker.Tests;

public sealed class WorkdayCalendarTests
{
    private static readonly DateOnly Wednesday = new(2026, 11, 18);
    private static readonly DateOnly TrackingStart = new(2026, 1, 1);

    [Fact]
    public void CurrentWeek_IsUpToDateWhenPreviousWorkdaysAreConfirmedAndTodayIsNot()
    {
        var confirmed = new HashSet<DateOnly> { new(2026, 11, 16), new(2026, 11, 17) };

        var status = WorkdayCalendar.EvaluateWeek(Wednesday, Wednesday, TrackingStart, confirmed.Contains);

        Assert.Equal(WeekStatusKind.UpToDate, status.Kind);
        Assert.Empty(status.MissingDays);
    }

    [Fact]
    public void CurrentWeek_IsIncompleteWhenAPreviousWorkdayIsMissing()
    {
        var confirmed = new HashSet<DateOnly> { new(2026, 11, 16) };
        var tuesday = new DateOnly(2026, 11, 17);

        var status = WorkdayCalendar.EvaluateWeek(Wednesday, Wednesday, TrackingStart, confirmed.Contains);

        Assert.Equal(WeekStatusKind.Incomplete, status.Kind);
        Assert.Equal([tuesday], status.MissingDays);
    }

    [Fact]
    public void CurrentWeekOnMonday_DoesNotRequireTodayOrFutureDays()
    {
        var monday = new DateOnly(2026, 11, 16);

        var status = WorkdayCalendar.EvaluateWeek(monday, monday, TrackingStart, _ => false);

        Assert.Equal(WeekStatusKind.UpToDate, status.Kind);
    }

    [Fact]
    public void PastWeek_IsCompleteOnlyWhenAllFiveWorkdaysAreConfirmed()
    {
        var displayedDate = new DateOnly(2026, 11, 10);
        var confirmedDays = WorkdayCalendar.EnumerateWorkdays(new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 13));
        var confirmed = new HashSet<DateOnly>(confirmedDays);

        var complete = WorkdayCalendar.EvaluateWeek(displayedDate, Wednesday, TrackingStart, confirmed.Contains);
        confirmed.Remove(new DateOnly(2026, 11, 13));
        var incomplete = WorkdayCalendar.EvaluateWeek(displayedDate, Wednesday, TrackingStart, confirmed.Contains);

        Assert.Equal(WeekStatusKind.Complete, complete.Kind);
        Assert.Equal(WeekStatusKind.Incomplete, incomplete.Kind);
    }

    [Fact]
    public void FutureWeek_IsNeutral()
    {
        var futureDate = new DateOnly(2026, 11, 25);

        var status = WorkdayCalendar.EvaluateWeek(futureDate, Wednesday, TrackingStart, _ => false);

        Assert.Equal(WeekStatusKind.Future, status.Kind);
    }

    [Fact]
    public void DaysBeforeTrackingStart_AreIgnored()
    {
        var trackingStart = new DateOnly(2026, 11, 18);
        var previousMonday = new DateOnly(2026, 11, 16);

        var status = WorkdayCalendar.EvaluateWeek(previousMonday, Wednesday, trackingStart, _ => false);

        Assert.Equal(WeekStatusKind.UpToDate, status.Kind);
    }

    [Fact]
    public void WeekBeforeTrackingStart_IsNotStarted()
    {
        var previousWeek = new DateOnly(2026, 11, 9);
        var trackingStart = new DateOnly(2026, 11, 18);

        var status = WorkdayCalendar.EvaluateWeek(previousWeek, Wednesday, trackingStart, _ => false);

        Assert.Equal(WeekStatusKind.NotStarted, status.Kind);
    }
}
