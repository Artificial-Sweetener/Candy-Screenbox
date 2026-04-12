# Candy-Screenbox

**Candy-Screenbox** is my fork of [Screenbox](https://github.com/huynhsontung/Screenbox).

Screenbox is already a good modern Windows media player. This fork keeps that foundation and adds the stuff I wanted for watching anime with chapters, multiple audio tracks, and subtitle tracks that need to behave the same way every time.

In short: this is Screenbox, but with automatic chapter skipping and better audio/subtitle defaults.

## Why This Exists

If you have a folder full of episodes where every file has an intro chapter, a credits chapter, multiple audio tracks, and a full English subtitle track, the normal flow gets repetitive fast.

Candy-Screenbox is for that problem.

- Pick the audio and subtitle tracks you actually want.
- Save them for a file or a whole folder.
- Tell the app which chapters to skip.
- Open the next file and let the app do the boring part.

## What Changed From Upstream

### Chapter Skip Rules

Candy-Screenbox can automatically skip chapters that match your rules.

Rules can apply to:

- the current file
- the current folder
- all media

Rules can match by:

- chapter title contains
- chapter title equals
- chapter title regex
- chapter number

This is useful for skipping intros, recaps, previews, credits, or whatever else your files already mark with chapter metadata.

You can configure chapter skipping from the player controls, the play queue context menu, or the settings page.

### Audio And Subtitle Track Preferences

Candy-Screenbox lets you save the current audio and subtitle choices from the audio/captions flyout.

Preferences can apply to:

- the current file
- the current folder

When a matching file opens later, Candy-Screenbox tries to pick the same kind of track again. It prefers stable metadata like language and labels, and falls back to track position when that is all the file gives us.

This is the "please stop making me pick Japanese audio and full English subtitles every episode" feature.

### Chapter Loading Fixes

This fork also fixes chapter loading across media transitions.

The app waits until the active file is actually ready before caching chapter data, so opening the next file in a folder does not keep stale chapters from the previous file.

## What Is Still Screenbox

Most of Candy-Screenbox is still Screenbox.

The app still uses the upstream UWP app structure and [LibVLCSharp](https://github.com/videolan/libvlcsharp). The media library, playback controls, network playback, picture-in-picture mode, frame capture, and the rest of the normal Screenbox experience are still here unless this fork changes something directly.

## Install

Candy-Screenbox is packaged as `Candy-Screenbox`.

The Microsoft Store and winget packages for Screenbox install upstream Screenbox, not this fork.

For now, this is a local MSIX build. Build `Screenbox.sln` for `x64`, sign the generated package with a certificate trusted by Windows, and install the MSIX.

## Build

Prerequisites:

- Windows 11
- Visual Studio 2022 with the UWP development workload
- Windows 10 SDK
- x64 build platform

Build from Visual Studio by opening `Screenbox.sln`, selecting `x64`, and building the solution.

For command-line builds, use Visual Studio MSBuild. `dotnet build` does not have the Windows XAML targets this UWP project needs.

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' .\Screenbox.sln /t:Build /p:Configuration=Debug /p:Platform=x64
```

## Upstream

Candy-Screenbox exists because Screenbox is already a nice base to build from.

- [Screenbox upstream repository](https://github.com/huynhsontung/Screenbox)
- [Contributing guide](CONTRIBUTING.md)
- [Project structure](docs/PROJECT_STRUCTURE.md)

## License

Candy-Screenbox follows the upstream Screenbox license. See [LICENSE](LICENSE) and [NOTICE.md](NOTICE.md).
