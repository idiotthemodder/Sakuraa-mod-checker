# Sakuraa Mod Checker

A rebuilt-and-extended Linux port of the **Sakuraa Client** Gorilla Tag mod, a mod checker / camera client for BepInEx. Runs standalone on Linux (CachyOS/Arch) where the original Windows-only build doesn't work out of the box.

This project was rebuilt and extended with heavy help from Claude (Anthropic's AI), a lot of this code was written collaboratively in an AI-assisted session rather than by hand line-by-line. Flagging that up front for transparency.

## What it does

- **Mod / cheat detection** — known-mods, known-cheats, and unsure lists with exact-match and wildcard (`~fragment=LABEL`) support, live-reloadable without restarting the game
- **Spotify controls** — play/pause/next/previous from inside the game, patched to work without the original Windows-only Spotify integration
- **Lyrics HUD integration** — togglable in-menu lyrics view with position/size/color settings
- **Nametag customization** — extra nametag display options, colored by category, distance fade, friend hiding
- **Theming** — dark/light/extra theme pages with settings that actually persist across restarts
- **VR map loader** — load maps from inside the VR menu
- **Help page** — browse known mods/cheats/unsure by category and see details on each
- **Unknown property logger** — flags never-seen-before properties in lobbies so you can triage and file them as mod/cheat/unsure from inside the menu
- **Diagnostics page** — health/perf/helpers/errors at a glance
- **Config profiles**, **auto-save**, **changelog page**, and more

## Structure

```
SakuraaCameraClientStandalone/   the actual mod source (this is the part I wrote/extended)
sakuraa-cleaned.csproj           project file, builds a BepInEx plugin DLL
CleanPayload.*.dll / .dll.br     rebuilt payload from the original Sakuraa Client source
```

## Building

```
dotnet build
```

Output lands at `bin/Debug/netstandard2.1/Sakuraa Client.dll` — drop that into your `BepInEx/plugins` folder (a `QOL` subfolder works fine too).

## Known lists

The known mods/cheats/unsure lists live in `BepInEx/config/sakuraa_known_*.txt` at runtime, one entry per line as `propertyname=LABEL`, or `~fragment=LABEL` for a wildcard match. These sync separately via [sakuraa-lists](https://github.com/idiotthemodder/sakuraa-lists).

## Credit

Built on top of the original Sakuraa Client mod. This repo is the Linux standalone rebuild plus a pile of extra features layered on top.
All credits go to Sakuraadev.
## Issues
If you have any problems, please make an issue, or DM @idiot_the_modder on Discord.
