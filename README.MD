# Secure Delete Interface

A WinUI 3 wrapper around Sysinternals SDelete. It provides a graphical
interface for the same command-line tool: pick files or drives, set the
flags, and see the output as it appears.

![1790415053640](Media\SDEL001.png) ![1790415053640](Media\SDEL002.png)

## What it does

- Runs `sdelete.exe` or `sdelete64.exe` in a pseudo-terminal (ConPTY), so
  output appears line by line as SDelete produces it, not in one chunk at
  the end.
- Provides two modes: secure deletion of files and folders, and cleaning
  free space on selected drives.
- Lets you pick the target sdelete executable, the number of passes, and
  the location of the Sysinternals Suite if it is not in `PATH`.
- Remembers your preferences between sessions.
- Shows progress, a timer, and the current status in a small footer bar.
- Supports Russian and English, switching automatically based on the
  system language.
- Registers context menu entries for files, folders, folder backgrounds,
  and drives.

## Requirements

- Windows 10 version 1809 or later (ConPTY).
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0).
- [Windows App SDK Runtime](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads).
- [Sysinternals](https://learn.microsoft.com/sysinternals/) [SDelete](https://learn.microsoft.com/sysinternals/downloads/sdelete) (`sdelete.exe` or `sdelete64.exe`), either in `PATH`
  or in a folder you specify in the settings

## Building

```
make debug
make x64
make arm
make publish // (both x64 and arm)
```

The project targets `net10.0-windows10.0.26100.0`. Both x64 and ARM64
builds are supported.

An Inno Setup script is included under `installer/`. To produce both
installers:

```
make iss64
make issarm
make installer (both iss64 and iss64)
```

## Usage

Launch the application, pick a tab, add paths or drives, adjust the options,
and press **Run**. The command that will be executed is always shown in the
output area before you start, so you can review it.

While SDelete is running, the **Run** button turns into **Stop** and the
footer shows a timer and a progress indicator. Stopping SDelete is
immediate; note that free space cleaning may leave a temporary file on the
disk if interrupted, which you can remove manually.

Settings are stored in `%LOCALAPPDATA%\Nkardaz\SDeleteGUI\settings.json`.

## Context menu

The installer registers the following entries for the current user:

- **Secure delete** on files and folders.
- **Clean free space** on folder backgrounds and drives.

These entries are per-user (`HKCU\Software\Classes`) and are removed on
uninstall.

## Limitations

- Multiple selection in the context menu is not supported. Windows launches
  one process per selected file, which makes consolidation unreliable without
  a native shell extension. The menu is therefore restricted to a single
  target at a time.
- The application is not signed. SmartScreen may show a warning on first
  launch.
- Only Sysinternals SDelete is officially supported as the underlying tool.
