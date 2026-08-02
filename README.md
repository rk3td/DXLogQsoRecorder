# DXLog QSO Recorder

**Version 1.2.3**  
Portable audio recorder for DXLog.net  
Author: Sergey Zimin (RK3TD)

## Purpose

DXLog QSO Recorder automatically saves an audio fragment for every new QSO received from DXLog.net through its UDP XML broadcast.

The application is designed for Windows 10 and Windows 11 and is distributed only as a portable application. It does not require installation and does not use the Windows Registry.

## Highlights of version 1.2.3

- DPI-safe station conflict dialog with readable buttons.
- Station selection is requested only once per Start/Stop recording session.

- automatic session-based filtering by DXLog `stationid`;
- the first valid QSO establishes the active station;
- a 20-second warning when another station is detected;
- options to keep the current station, switch station, or record all stations;
- the station choice remains active until Stop is pressed;
- the recording browser, search, player, and portable SQLite index remain available.

## Audio processing

Before MP3 encoding, the selected source is normalized to:

- channel 1 only;
- 24,000 Hz sample rate;
- 16-bit PCM;
- mono.

This allows mono, stereo, four-channel, and other multichannel capture devices to be used without passing an unsupported channel count to LAME.

If MP3 encoding fails for any reason, the original recording is preserved as a WAV file.

## Recording structure

Recordings are grouped by the DXLog contest name:

```text
Recordings/
  CQ-M/
  CQ WW CW/
  Russian DX Contest/
  Unknown/
```

The directory name is taken from the `contestname` field. Characters not allowed in Windows file names are replaced with underscores. If the contest name is missing, the `Unknown` directory is used.

File names use the following format:

```text
yyyyMMdd_HHmmss_MYCALL_CALL_BAND_MODE.mp3
```

## Main features

- Windows audio capture-device selection;
- circular pre-buffer from 1 to 600 seconds;
- post-buffer from 0 to 600 seconds;
- MP3 output at 32, 48, or 64 kbps;
- 24 kHz, 16-bit PCM, mono normalization;
- first-channel extraction for multichannel sources;
- automatic WAV fallback;
- DXLog `contactinfo` XML reception;
- default bind address `127.0.0.1`;
- portable settings, logs, packets, temporary files, and recordings;
- no automatic deletion of recordings.

## Recommended DXLog settings

Configure DXLog.net to send `contactinfo` UDP XML broadcasts to:

```text
Address: 127.0.0.1
Port:    12060
```

Use the same values in DXLog QSO Recorder.

## Portable build for Windows x64

1. Install the .NET 8 SDK.
2. Extract the source archive.
3. Run:

```text
build-portable-win-x64.cmd
```

Alternatively, run from PowerShell:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\build-portable-win-x64.ps1
```

The output is created in:

```text
publish\win-x64
```

The first build requires internet access to download the NAudio and NAudio.Lame NuGet packages.

## Portable data

At runtime, all data is stored relative to the application directory:

```text
Data/settings.json
Logs/recorder.log
Packets/
Temp/
Recordings/
```

## Third-party components

- NAudio — MIT License
- NAudio.Lame — .NET wrapper for LAME
- LAME MP3 Encoder — GNU LGPL

See `THIRD_PARTY_NOTICES.txt` for details.

## Author

Sergey Zimin  
Callsign: RK3TD

© 2026 Sergey Zimin


## Recording browser and player

Version 1.2.0 added a searchable recordings library. The application builds a portable SQLite index at `Data/recordings.db`, scans existing MP3/WAV files, and supports filtering by callsign and contest. Double-click a result or use the Play button to listen without leaving the application. The index can be deleted safely and is rebuilt from the `Recordings` folder.

## Multi-Op station filtering

During each recording session, the first QSO packet with a non-empty DXLog `stationid` becomes the active station. If a later QSO arrives from another station, the recorder shows a 20-second warning with three choices:

- keep recording the original station (default);
- switch to the newly detected station;
- record QSO events from all stations.

The choice is kept only until **Stop** is pressed.
