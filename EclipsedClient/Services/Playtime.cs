using System.Diagnostics;
using System.IO;

namespace EclipsedClient.Services;

internal sealed record PlaySlice(DateTimeOffset Start, DateTimeOffset End, long? PlaceId);
internal sealed record PlayHistory
{
    public List<PlaySlice> Sessions { get; init; } = [];
}

internal sealed record PlaytimeSummary(
    TimeSpan Total,
    int SessionsThisWeek,
    IReadOnlyList<TimeSpan> Hours,
    TimeSpan Today,
    TimeSpan ThisWeek,
    int Streak,
    int? PeakHour,
    IReadOnlyList<KeyValuePair<DateOnly, TimeSpan>> Daily,
    IReadOnlyList<KeyValuePair<DateOnly, TimeSpan>> Weekly,
    IReadOnlyList<KeyValuePair<long, TimeSpan>> TopPlaces);

internal sealed class Playtime
{
    private static readonly int SessionId = Process.GetCurrentProcess().SessionId;
    private readonly PlayHistory _history = LocalData.Load<PlayHistory>("stats.json");
    private readonly RobloxLogs _logs = new();
    private (int Id, DateTimeOffset Started)? _player;
    private DateTimeOffset? _lastPoll;
    private bool _dirty;

    internal DateTimeOffset? PlayingSince { get; private set; }
    internal ServerConnection? Connection { get; private set; }

    internal void Poll(DateTimeOffset now)
    {
        var active = FindPlayer();
        bool sameProcess = active is not null && active == _player;
        if (_lastPoll is DateTimeOffset previous && _player is not null &&
            now > previous && now - previous <= TimeSpan.FromSeconds(15))
        {
            DateTimeOffset end = sameProcess ? now : previous + (now - previous) / 2;
            AddSlice(previous, end, Connection?.PlaceId);
        }

        if (!sameProcess)
        {
            _logs.Reset();
            PlayingSince = active?.Started;
        }

        _player = active;
        _lastPoll = now;
        Connection = active is { } player ? _logs.Read(player.Started) : null;
        Flush();
    }

    private void AddSlice(DateTimeOffset start, DateTimeOffset end, long? placeId)
    {
        List<PlaySlice> sessions = _history.Sessions;
        if (sessions.LastOrDefault() is { } last && last.End == start && last.PlaceId == placeId)
            sessions[^1] = last with { End = end };
        else
            sessions.Add(new(start, end, placeId));
        _dirty = true;
    }

    internal void Flush()
    {
        if (!_dirty)
            return;
        LocalData.Save("stats.json", _history);
        _dirty = false;
    }

    private static (int Id, DateTimeOffset Started)? FindPlayer()
    {
        (int Id, DateTimeOffset Started)? newest = null;
        foreach (Process process in Process.GetProcessesByName("RobloxPlayerBeta"))
        {
            using (process)
            {
                try
                {
                    if (process.SessionId != SessionId)
                        continue;
                    DateTimeOffset started = process.StartTime.ToUniversalTime();
                    if (newest is null || started > newest.Value.Started)
                        newest = (process.Id, started);
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
            }
        }

        return newest;
    }

    internal PlaytimeSummary Summarize(DateTimeOffset now)
    {
        DateOnly today = DateOnly.FromDateTime(now.LocalDateTime);
        DateOnly monday = today.AddDays(-((int)today.DayOfWeek + 6) % 7);
        var days = new Dictionary<DateOnly, TimeSpan>();
        var weeks = new Dictionary<DateOnly, TimeSpan>();
        var places = new Dictionary<long, TimeSpan>();
        var hours = new TimeSpan[24];

        foreach (PlaySlice slice in _history.Sessions)
        {
            if (slice.End <= slice.Start)
                continue;
            if (slice.PlaceId is long place)
                places[place] = places.GetValueOrDefault(place) + (slice.End - slice.Start);

            for (DateTimeOffset cursor = slice.Start; cursor < slice.End;)
            {
                DateTime local = cursor.LocalDateTime;
                DateOnly date = DateOnly.FromDateTime(local);
                DateOnly week = date.AddDays(-((int)date.DayOfWeek + 6) % 7);
                // Advance on the UTC timeline so repeated and skipped local hours are handled.
                DateTimeOffset next = cursor.AddTicks(TimeSpan.TicksPerHour - local.Ticks % TimeSpan.TicksPerHour);
                if (next > slice.End)
                    next = slice.End;

                TimeSpan duration = next - cursor;
                days[date] = days.GetValueOrDefault(date) + duration;
                weeks[week] = weeks.GetValueOrDefault(week) + duration;
                hours[local.Hour] += duration;
                cursor = next;
            }
        }

        int streak = 0;
        DateOnly streakDay = days.ContainsKey(today) ? today : today.AddDays(-1);
        while (days.ContainsKey(streakDay))
        {
            streak++;
            streakDay = streakDay.AddDays(-1);
        }

        DateTimeOffset weekStart = monday.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
        DateTimeOffset? lastEnd = null;
        int sessionsThisWeek = 0;
        foreach (PlaySlice slice in _history.Sessions.Where(slice => slice.End > slice.Start).OrderBy(slice => slice.Start))
        {
            if (slice.End > weekStart && (lastEnd is null || lastEnd <= weekStart || slice.Start > lastEnd))
                sessionsThisWeek++;
            if (lastEnd is null || slice.End > lastEnd)
                lastEnd = slice.End;
        }

        TimeSpan longestHour = hours.Max();
        return new(
            TimeSpan.FromTicks(hours.Sum(hour => hour.Ticks)),
            sessionsThisWeek,
            hours,
            days.GetValueOrDefault(today),
            weeks.GetValueOrDefault(monday),
            streak,
            longestHour > TimeSpan.Zero ? Array.IndexOf(hours, longestHour) : null,
            Enumerable.Range(-6, 7).Select(offset => today.AddDays(offset))
                .Select(date => KeyValuePair.Create(date, days.GetValueOrDefault(date))).ToArray(),
            Enumerable.Range(-7, 8).Select(offset => monday.AddDays(offset * 7))
                .Select(date => KeyValuePair.Create(date, weeks.GetValueOrDefault(date))).ToArray(),
            places.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).Take(10).ToArray());
    }
}
