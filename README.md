# sxtg2

`sxtg2` is a MelonLoader mod for Sixtar Gate STARTRAIL that loads custom BMS charts and their media from a local `hwa` folder.

## Features

- Parses BMS charts and injects custom notes into gameplay
- Adds custom track data to the music select flow
- Replaces BGA, BGM, preview audio, and thumbnails for custom tracks
- Keeps BGA and BGM playback synchronized
- Adjusts score limits for custom charts
- Supports the custom pause and eyecatch thumbnail flow

## Repository layout

```text
sxtg2-mod/              # Main MelonLoader mod project
sxtg2.LogicTests/       # Logic test runner
sxtg2 커스텀 리드미/    # Project, system, and user documentation
sxtg2.sln               # Visual Studio solution
build.bat               # Debug build and local Mods copy helper
build-release.bat       # Release build and local Mods copy helper
```

## Requirements

- Windows
- Sixtar Gate STARTRAIL
- MelonLoader installed for the game
- Visual Studio/MSBuild for the main mod project
- .NET tooling for `sxtg2.LogicTests`

The main project targets .NET Framework 4.7.2 and references MelonLoader, Harmony, and Unity assemblies from the local game installation.

## Build

The build scripts currently use local paths near the top of each script:

- `GAME_PATH`
- `SOURCE_ROOT`

Update those values for your environment before running:

```bat
build.bat
```

For a release build:

```bat
build-release.bat
```

Both scripts build the solution and copy `sxtg2.dll` into the game's `Mods` folder when the configured paths are valid.

## Custom content layout

Create an `hwa` folder under the game install directory. Album folders can sit one level below it:

```text
{Game folder}\hwa\
  Album_A\
    trackinfo.txt
    chart.bms
    demo.ogg
    music.ogg
    thumb.png
    bg.mp4
```

Supported chart extensions are `.bms`, `.bme`, and `.bml`. BGM lookup supports `.ogg`, `.mp3`, and `.wav`, while BGA lookup uses `.mp4`.

## Tests

Run the logic test helper from the repository root:

```bat
run-logic-tests.bat
```

## Documentation

Start here for the Korean project docs:

- [Documentation index](sxtg2%20커스텀%20리드미/README.md)
- [Project overview](sxtg2%20커스텀%20리드미/00-overview/PROJECT_OVERVIEW.md)
- [Install and folder layout](sxtg2%20커스텀%20리드미/01-user-guide/INSTALL_AND_LAYOUT.md)
- [Current status](sxtg2%20커스텀%20리드미/CURRENT_STATUS.md)
