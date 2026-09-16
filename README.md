<p align="center">
    <img alt="mothentik" src="src/Clamor.App/Assets/clamor-text-cropped.jpg" height="150" >
</p>

---

[![GitHub Workflow Status](https://img.shields.io/github/actions/workflow/status/wakeops/clamor/build-test.yml?branch=main&style=for-the-badge)](https://github.com/wakeops/clamor/actions/workflows/build-test.yml)
[![Latest Release](https://img.shields.io/github/v/release/wakeops/clamor?style=for-the-badge)](https://github.com/wakeops/clamor/releases/latest)
[![MIT License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](LICENSE)

# Clamor

A WPF desktop soundboard for Windows, inspired by Soundboard/Voicemod. Clamor plays audio clips
on demand and routes them to a virtual audio cable (VB-Audio CABLE) so other apps — Discord,
Zoom, games — hear them as if coming from a microphone, optionally mixed with your real mic.

See `soundboard-architecture.md` (if present in this checkout) for the full design doc this
implementation follows.

## Project layout

```
Clamor.slnx
├── src/
│   ├── Clamor.Core/    Models + services (profiles, settings, clip library) — no NAudio/WPF
│   ├── Clamor.Audio/   NAudio engine: dual-mixer AudioEngine, ClipPlayer, DeviceManager, MicPassthrough
│   ├── Clamor.Hotkeys/ Global hotkeys via Win32 RegisterHotKey, no UI framework dependency
│   └── Clamor.App/     WPF UI (MVVM), dark theme, tray icon, first-run VB-Cable detection
├── tests/
│   └── Clamor.Tests/   xUnit tests for Core and Audio's pure logic
└── installer/          Inno Setup script
```

## Building

Requires the .NET 10 SDK (or Visual Studio 17.10+) and Windows — WPF only runs on Windows, and
`Clamor.Audio`/`Clamor.Hotkeys` use Win32 APIs that are no-ops or unavailable elsewhere.
`Clamor.Core` and `Clamor.Tests` will build cross-platform, but the app itself needs Windows to
run. The solution file is the newer XML-based `.slnx` format, which needs a `dotnet` CLI/Visual
Studio recent enough to understand it (SDK 9.0.200+; the CI workflows pin `10.0.x`).

```
dotnet restore Clamor.slnx
dotnet build Clamor.slnx --configuration Release
dotnet test Clamor.slnx --configuration Release
```

Run the app from Visual Studio (set `Clamor.App` as the startup project) or:

```
dotnet run --project src/Clamor.App
```

On first launch, Clamor checks for [VB-Audio Virtual Cable](https://vb-audio.com/Cable/) and
offers to open the download page if it isn't installed — it's a signed driver Clamor doesn't
bundle, so you install it separately (and reboot once, per VB-Audio's own requirement).

## Configuration

Settings and profiles are stored as JSON under `%AppData%\Clamor\`:

- `settings.json` — selected devices, mic passthrough default, minimize-to-tray behavior
- `profiles\*.json` — one file per profile: clip paths, hotkeys, per-clip volume

## CI/CD

- **`.github/workflows/build-test.yml`** — restores, builds, and runs tests on every PR and push to `main`.
- **`.github/workflows/release.yml`** — on a `v*.*.*` tag push, re-runs the same build+test gate,
  publishes a self-contained `win-x64` build, packages it with Inno Setup
  (`installer/Clamor.iss`), and attaches the installer to a GitHub Release.

## Known gaps (not yet implemented)

- **Drag-to-reorder** the sound grid — the data model (`SoundClip.SortOrder`,
  `ClipLibraryService.MoveClip`) already supports it, but there's no drag gesture wired up in
  `SoundGridView` yet.
- **Waveform preview/trim, fades, push-to-talk hotkeys** — listed as nice-to-have (v1.1+) in the
  design doc, not implemented.
