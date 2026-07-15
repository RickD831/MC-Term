# mcterm

Windows 11 command-line and terminal UI control for Spotify, browsers, and other players that publish a Windows system media session (GSMTC).

> **Built in 10 minutes.** This project went from an empty folder to a fully working Windows media controller—with a command-line interface, live terminal player, transport controls, seeking, and a standalone executable—in about ten minutes, built collaboratively by a human and Codex.

## Use

### Install

Download and extract the Windows x64 ZIP, then run this from the extracted folder:

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

The installer copies `mcterm.exe` to `%LOCALAPPDATA%\Programs\mcterm` and adds that directory to your user `PATH`. It does not require administrator access. Open a new terminal after installation, then run `mcterm` from anywhere.

```powershell
mcterm                  # interactive player
mcterm now              # current track
mcterm now --json       # machine-readable state
mcterm sessions         # available media sessions
mcterm play|pause|toggle|next|previous|stop
mcterm seek +30
mcterm --session spotify
```

Run `mcterm help` for all commands and interactive keys. Session selection is a case-insensitive match against the Windows application ID; without it, mcterm controls the session Windows considers current.

### Uninstall

```powershell
powershell -ExecutionPolicy Bypass -File "$env:LOCALAPPDATA\Programs\mcterm\uninstall.ps1"
```

This removes the installed files and the user `PATH` entry. No other `PATH` entries are changed.

## Interactive player

Running `mcterm` without a command opens the live terminal player:

```text
╭─────────────────────────────────── M C T E R M ────────────────────────────────────╮
│                                                                                    │
│   ♪  On My Way                                                                     │
│      The Hip Abduction                                                             │
│      A Seafarer and the Infinite Dream                                             │
│                                                                                    │
│   ━━━━━━━━━━━━━━━━━━━━━╸────────────────────────────────────────────────────────   │
│   1:06   Playing            4:08                                                   │
│                                                                                    │
│   [z] Previous   [Space] Play/Pause   [x] Next                                     │
│   [←/→] Seek 10s                         [q] Quit                                  │
╰────────────────────────────────────────────────────────────────────────────────────╯
```

The timer and progress bar advance live. Use <kbd>Space</kbd> to play or pause, <kbd>Z</kbd>/<kbd>X</kbd> for the previous or next track, the arrow keys to seek 10 seconds, and <kbd>Q</kbd> to quit.

## Develop

Requires the .NET 10 SDK on Windows.

```powershell
dotnet restore McTerm.slnx --source https://api.nuget.org/v3/index.json
dotnet build McTerm.slnx
dotnet run --project src/McTerm -- now
dotnet publish src/McTerm -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
