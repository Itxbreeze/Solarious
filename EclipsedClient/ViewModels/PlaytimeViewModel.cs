using EclipsedClient.Services;

namespace EclipsedClient.ViewModels;

internal sealed record ChartBar(string Label, double Height, string Value);
internal sealed record StatCard(string Label, string Value);

internal sealed class PlaytimeViewModel : ViewModel
{
    private ChartBar[] _daily = [], _weekly = [];
    private bool _weeklySelected;
    public StatCard[] Stats { get; private set; } = [];
    public ChartBar[] Hours { get; private set; } = [];
    public string Peak { get; private set; } = "";
    public ChartBar[] Bars => ShowWeekly ? _weekly : _daily;
    public bool ShowWeekly
    {
        get => _weeklySelected;
        set { _weeklySelected = value; Notify(); Notify(nameof(Bars)); }
    }

    internal void Update(PlaytimeSummary summary, Func<long?, string?> titleFor)
    {
        Stats = [
            new("TOTAL PLAYTIME", Duration(summary.Total)),
            new("DAILY STREAK", $"{summary.Streak} {(summary.Streak == 1 ? "day" : "days")}"),
            new("MOST PLAYED", summary.TopPlaces.Count == 0 ? "—" : titleFor(summary.TopPlaces[0].Key) ?? "—"),
            new("SESSIONS THIS WEEK", summary.SessionsThisWeek.ToString())
        ];
        Peak = summary.PeakHour is int peak ? $"Peak hour · {peak:00}:00 local" : "No recorded activity";
        _daily = Scale(summary.Daily.Select(day => (day.Key.ToString("ddd"), day.Value)), 124);
        _weekly = Scale(summary.Weekly.Select(week => (week.Key.ToString("MMM d"), week.Value)), 124);
        Hours = Scale(summary.Hours.Select((hour, index) => ($"{index:00}", hour)), 76);
        Notify(nameof(Stats));
        Notify(nameof(Peak));
        Notify(nameof(Bars));
        Notify(nameof(Hours));
    }

    private static string Duration(TimeSpan value) => value.TotalMinutes is > 0 and < 1
        ? "< 1m" : $"{(long)value.TotalHours}h {value.Minutes:00}m";

    private static ChartBar[] Scale(IEnumerable<(string Label, TimeSpan Time)> values, double height)
    {
        var bars = values.ToArray();
        double max = Math.Max(1, bars.Max(item => item.Time.TotalSeconds));
        return bars.Select(item => new ChartBar(item.Label,
            Math.Max(3, height * item.Time.TotalSeconds / max), Duration(item.Time))).ToArray();
    }
}
