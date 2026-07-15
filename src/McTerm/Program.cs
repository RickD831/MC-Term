using System.Text.Json;
using System.Runtime.InteropServices;
using Windows.Media.Control;

Console.OutputEncoding = System.Text.Encoding.UTF8;
try
{
    var parsed = Parse(args);
    if (parsed.Command is "help" or "--help" or "-h") return Help();
    if (parsed.Command is "version" or "--version") { Console.WriteLine("mcterm 0.2.0"); return 0; }
    var media = await MediaService.CreateAsync();
    using var volume = new WindowsVolumeService();
    switch (parsed.Command)
    {
        case "tui": await RunTui(media, volume, parsed.Session); return 0;
        case "now":
            var snapshot = await media.SnapshotAsync(parsed.Session);
            if (snapshot is null) return Error("No matching media session is active.", 2);
            if (parsed.Json) Console.WriteLine(JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
            else { Console.WriteLine($"{snapshot.Title} — {snapshot.Artist}"); Console.WriteLine($"{snapshot.Status}  {snapshot.Position:mm\\:ss} / {snapshot.Duration:mm\\:ss}  [{snapshot.SessionId}]"); }
            return 0;
        case "sessions":
            var sessions = media.SessionIds();
            if (parsed.Json) Console.WriteLine(JsonSerializer.Serialize(sessions, new JsonSerializerOptions { WriteIndented = true }));
            else foreach (var id in sessions) Console.WriteLine(id);
            return 0;
        case "seek":
            if (!double.TryParse(parsed.Value, System.Globalization.CultureInfo.InvariantCulture, out var seconds)) return Error("Usage: mcterm seek <+|-seconds>", 1);
            return await media.SeekAsync(TimeSpan.FromSeconds(seconds), parsed.Session) ? 0 : Error("Seek was not accepted by the media session.", 3);
        case "volume":
            if (parsed.Value is null) { PrintVolume(volume.GetState(), parsed.Json); return 0; }
            if (!double.TryParse(parsed.Value, System.Globalization.CultureInfo.InvariantCulture, out var level)) return Error("Usage: mcterm volume [0-100|+amount|-amount]", 1);
            if (parsed.Value.StartsWith('+') || parsed.Value.StartsWith('-')) volume.Adjust((float)(level / 100));
            else volume.SetLevel((float)(level / 100));
            PrintVolume(volume.GetState(), parsed.Json); return 0;
        case "mute": volume.SetMute(true); PrintVolume(volume.GetState(), parsed.Json); return 0;
        case "unmute": volume.SetMute(false); PrintVolume(volume.GetState(), parsed.Json); return 0;
        case "play": case "pause": case "toggle": case "next": case "previous": case "prev": case "stop":
            return await media.ControlAsync(parsed.Command, parsed.Session) ? 0 : Error("Command was not accepted by the media session.", 3);
        default: return Error($"Unknown command '{parsed.Command}'. Run 'mcterm help'.", 1);
    }
}
catch (Exception ex) { return Error(ex.Message, 1); }

static Options Parse(string[] values)
{
    var command = "tui";
    string? session = null, value = null; var json = false;
    for (var i = 0; i < values.Length; i++)
    {
        if (values[i] is "--json" or "-j") json = true;
        else if (values[i] is "--session" or "-s") { if (++i >= values.Length) throw new ArgumentException("--session requires a value."); session = values[i]; }
        else if (values[i] is "--help" or "-h") command = "help";
        else if (values[i] == "--version") command = "version";
        else if (command == "tui" && value is null) command = values[i].ToLowerInvariant();
        else value ??= values[i];
    }
    return new(command, session, json, value);
}

static async Task RunTui(MediaService media, WindowsVolumeService volume, string? selector)
{
    Console.CursorVisible = false;
    try
    {
        while (true)
        {
            Draw(await media.SnapshotAsync(selector), volume.GetState());
            for (var tick = 0; tick < 10; tick++)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true).Key;
                    if (key is ConsoleKey.Q or ConsoleKey.Escape) return;
                    if (key == ConsoleKey.Spacebar) await media.ControlAsync("toggle", selector);
                    else if (key is ConsoleKey.X or ConsoleKey.MediaNext) await media.ControlAsync("next", selector);
                    else if (key is ConsoleKey.Z or ConsoleKey.MediaPrevious) await media.ControlAsync("previous", selector);
                    else if (key == ConsoleKey.RightArrow) await media.SeekAsync(TimeSpan.FromSeconds(10), selector);
                    else if (key == ConsoleKey.LeftArrow) await media.SeekAsync(TimeSpan.FromSeconds(-10), selector);
                    else if (key == ConsoleKey.UpArrow) volume.Adjust(0.05f);
                    else if (key == ConsoleKey.DownArrow) volume.Adjust(-0.05f);
                    else if (key == ConsoleKey.M) volume.ToggleMute();
                    break;
                }
                await Task.Delay(100);
            }
        }
    }
    finally { Console.ResetColor(); Console.CursorVisible = true; Console.Clear(); }
}

static void Draw(MediaSnapshot? item, VolumeState volume)
{
    var width = Math.Clamp(Console.WindowWidth - 2, 48, 86);
    Console.SetCursorPosition(0, 0); Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("╭" + Center(" M C T E R M ", width - 2, '─') + "╮"); Console.ResetColor();
    if (item is null)
    {
        Line("No active Windows media session", width); Line("Start playing something in Spotify or a browser.", width);
        for (var i = 0; i < 7; i++) Line("", width);
    }
    else
    {
        Line("", width); Console.ForegroundColor = ConsoleColor.White; Line("  ♪  " + item.Title, width);
        Console.ForegroundColor = ConsoleColor.DarkGray; Line("     " + item.Artist, width); Line("     " + item.Album, width); Console.ResetColor();
        Line("", width); Line(Progress(item.Position, item.Duration, width - 8), width);
        var volumeText = volume.Muted ? "Muted" : $"Vol {volume.Percent}%";
        Line($"  {Fmt(item.Position)}   {item.Status,-12}   {Fmt(item.Duration),8}   {volumeText}", width); Line("", width);
        Line("  [z] Previous   [Space] Play/Pause   [x] Next", width); Line("  [←/→] Seek 10s                         [q] Quit", width);
        Line("  [↑/↓] Volume 5%                       [m] Mute", width);
    }
    Console.ForegroundColor = ConsoleColor.Cyan; Console.WriteLine("╰" + new string('─', width - 2) + "╯"); Console.ResetColor();
}

static void Line(string text, int width) { var content = Truncate(text, width - 4).PadRight(width - 4); Console.WriteLine("│ " + content + " │"); }
static string Progress(TimeSpan p, TimeSpan d, int width) { var ratio = d.TotalSeconds <= 0 ? 0 : Math.Clamp(p.TotalSeconds / d.TotalSeconds, 0, 1); var n = (int)Math.Round(ratio * width); return "  " + new string('━', n) + (n < width ? "╸" + new string('─', Math.Max(0, width - n - 1)) : ""); }
static string Fmt(TimeSpan value) => value.TotalHours >= 1 ? value.ToString(@"h\:mm\:ss") : value.ToString(@"m\:ss");
static string Center(string value, int width, char fill) { var n = Math.Max(0, width - value.Length); return new string(fill, n / 2) + value + new string(fill, n - n / 2); }
static string Truncate(string value, int width) => value.Length <= width ? value : value[..Math.Max(0, width - 1)] + "…";
static int Error(string message, int code) { Console.Error.WriteLine("mcterm: " + message); return code; }
static void PrintVolume(VolumeState state, bool json)
{
    if (json) Console.WriteLine(JsonSerializer.Serialize(state));
    else Console.WriteLine(state.Muted ? $"Volume {state.Percent}% (muted)" : $"Volume {state.Percent}%");
}
static int Help() { Console.WriteLine("""
mcterm — control the active Windows media session

Usage:
  mcterm [tui] [--session <name>]
  mcterm now [--json] [--session <name>]
  mcterm sessions [--json]
  mcterm play|pause|toggle|next|previous|stop [--session <name>]
  mcterm seek <+|-seconds> [--session <name>]
  mcterm volume [0-100|+amount|-amount] [--json]
  mcterm mute|unmute [--json]

Keys: Space play/pause, z/x previous/next, left/right seek,
      up/down volume, m mute, q quit
"""); return 0; }

sealed record Options(string Command, string? Session, bool Json, string? Value);
sealed record MediaSnapshot(string SessionId, string Title, string Artist, string Album, string Status, TimeSpan Position, TimeSpan Duration, bool CanPlay, bool CanPause, bool CanNext, bool CanPrevious, bool CanSeek);
sealed record VolumeState(int Percent, bool Muted);

sealed class MediaService
{
    private readonly GlobalSystemMediaTransportControlsSessionManager manager;
    private MediaService(GlobalSystemMediaTransportControlsSessionManager manager) => this.manager = manager;
    public static async Task<MediaService> CreateAsync() => new(await GlobalSystemMediaTransportControlsSessionManager.RequestAsync());
    public IReadOnlyList<string> SessionIds() => manager.GetSessions().Select(s => s.SourceAppUserModelId).ToList();
    public async Task<MediaSnapshot?> SnapshotAsync(string? selector)
    {
        var s = Find(selector); if (s is null) return null;
        var m = await s.TryGetMediaPropertiesAsync(); var p = s.GetPlaybackInfo(); var t = s.GetTimelineProperties(); var c = p.Controls;
        var position = CurrentPosition(t, p) - t.StartTime;
        var duration = t.EndTime > t.StartTime ? t.EndTime - t.StartTime : TimeSpan.Zero;
        return new(s.SourceAppUserModelId, F(m?.Title, "Unknown title"), F(m?.Artist, "Unknown artist"), F(m?.AlbumTitle, "Unknown album"), p.PlaybackStatus.ToString(), position < TimeSpan.Zero ? TimeSpan.Zero : position, duration, c.IsPlayEnabled, c.IsPauseEnabled, c.IsNextEnabled, c.IsPreviousEnabled, c.IsPlaybackPositionEnabled);
    }
    public async Task<bool> ControlAsync(string action, string? selector)
    {
        var s = Find(selector); if (s is null) return false;
        return action switch { "play" => await s.TryPlayAsync(), "pause" => await s.TryPauseAsync(), "toggle" => await s.TryTogglePlayPauseAsync(), "next" => await s.TrySkipNextAsync(), "previous" or "prev" => await s.TrySkipPreviousAsync(), "stop" => await s.TryStopAsync(), _ => false };
    }
    public async Task<bool> SeekAsync(TimeSpan offset, string? selector)
    {
        var s = Find(selector); if (s is null) return false;
        var t = s.GetTimelineProperties();
        var target = CurrentPosition(t, s.GetPlaybackInfo()) + offset;
        if (target < t.StartTime) target = t.StartTime; if (t.EndTime > t.StartTime && target > t.EndTime) target = t.EndTime;
        return await s.TryChangePlaybackPositionAsync(target.Ticks);
    }
    private static TimeSpan CurrentPosition(GlobalSystemMediaTransportControlsSessionTimelineProperties timeline, GlobalSystemMediaTransportControlsSessionPlaybackInfo playback)
    {
        var position = timeline.Position;
        if (playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing && timeline.LastUpdatedTime != default)
        {
            var elapsed = DateTimeOffset.Now - timeline.LastUpdatedTime;
            if (elapsed > TimeSpan.Zero) position += TimeSpan.FromTicks((long)(elapsed.Ticks * (playback.PlaybackRate ?? 1.0)));
        }
        if (position < timeline.StartTime) return timeline.StartTime;
        if (timeline.EndTime > timeline.StartTime && position > timeline.EndTime) return timeline.EndTime;
        return position;
    }
    private GlobalSystemMediaTransportControlsSession? Find(string? selector) => string.IsNullOrWhiteSpace(selector) ? manager.GetCurrentSession() : manager.GetSessions().FirstOrDefault(s => s.SourceAppUserModelId.Contains(selector, StringComparison.OrdinalIgnoreCase));
    private static string F(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
}

sealed class WindowsVolumeService : IDisposable
{
    private readonly IAudioEndpointVolume endpoint;
    private readonly object endpointObject;
    private readonly object deviceObject;
    private readonly object enumeratorObject;

    public WindowsVolumeService()
    {
        var type = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"), true)!;
        enumeratorObject = Activator.CreateInstance(type)!;
        var enumerator = (IMMDeviceEnumerator)enumeratorObject;
        Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out var device));
        deviceObject = device;
        var iid = typeof(IAudioEndpointVolume).GUID;
        Marshal.ThrowExceptionForHR(device.Activate(ref iid, 23, IntPtr.Zero, out endpointObject));
        endpoint = (IAudioEndpointVolume)endpointObject;
    }

    public VolumeState GetState()
    {
        Marshal.ThrowExceptionForHR(endpoint.GetMasterVolumeLevelScalar(out var level));
        Marshal.ThrowExceptionForHR(endpoint.GetMute(out var muted));
        return new((int)Math.Round(level * 100), muted);
    }

    public void SetLevel(float level) => Marshal.ThrowExceptionForHR(endpoint.SetMasterVolumeLevelScalar(Math.Clamp(level, 0, 1), Guid.Empty));
    public void Adjust(float amount) => SetLevel(GetState().Percent / 100f + amount);
    public void SetMute(bool muted) => Marshal.ThrowExceptionForHR(endpoint.SetMute(muted, Guid.Empty));
    public void ToggleMute() { var state = GetState(); SetMute(!state.Muted); }

    public void Dispose()
    {
        Marshal.FinalReleaseComObject(endpointObject);
        Marshal.FinalReleaseComObject(deviceObject);
        Marshal.FinalReleaseComObject(enumeratorObject);
    }

    private enum EDataFlow { Render, Capture, All }
    private enum ERole { Console, Multimedia, Communications }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, [MarshalAs(UnmanagedType.Interface)] out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint classContext, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float levelDb, Guid context);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, Guid context);
        [PreserveSig] int GetMasterVolumeLevel(out float levelDb);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float levelDb, Guid context);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, Guid context);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float levelDb);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
    }
}
