# Changelog

## [1.2.4] - 2026-08-04

### Changed

- Preserve mono recording for one-channel audio sources.
- Preserve stereo recording for two-channel audio sources.
- For sources with three or more channels, record only channels 1 and 2 as stereo.
- Keep 24 kHz, 16-bit PCM normalization and the existing user-selected MP3 bitrate.
- Avoid continuous channel-level analysis to keep background CPU usage low during contests.

## [1.2.3] - 2026-08-02

### Fixed

- The DXLog station conflict dialog now scales correctly at higher Windows DPI settings and no longer clips button captions.
- The station choice is now asked only once per recording session.
- Keep, switch, record-all, and the 20-second timeout decisions remain locked until Stop is pressed.


## [1.2.2] - 2026-08-02

### Added
- Automatic DXLog `stationid` session detection for Multi-Op networks.
- The first valid QSO packet establishes the active station for the current recording session.
- A 20-second non-blocking warning when a QSO from another station is detected.
- Choices to keep the current station, switch to the new station, or record all stations.
- The choice remains active until Stop is pressed.

### Changed
- Foreign-station QSO packets no longer start recordings unless the operator switches station or enables all-station recording.

## [1.2.1] - 2026-08-01

### Fixed

- Create the portable `Data` directory before opening `Data/recordings.db`.
- Prevent the application from terminating silently when startup initialization fails.
- Write unhandled startup exceptions to `startup-error.log` and show the error location.
- Add a defensive directory check inside `RecordingIndexService`.

## [1.2.0] - 2026-08-01

### Added
- Recordings browser with combined callsign and contest filters.
- Portable SQLite index stored in `Data/recordings.db`.
- Automatic indexing of existing MP3 and WAV recordings.
- Built-in playback with play, pause, stop, seek, time display, and folder opening.
- Automatic index update after each completed recording.

## 1.1.0 — English edition

- Converted the complete user interface to English.
- Converted validation, status, warning, and error messages to English.
- Converted remaining source-code exception messages to English.
- Replaced the Russian readme with an English `README.md`.
- Updated build scripts and assembly metadata to version 1.1.0.
- Preserved the portable-only distribution model.
- Preserved 24 kHz, 16-bit PCM, mono normalization using channel 1.
- Preserved WAV fallback when MP3 encoding fails.

## 1.0.2

- Added normalization to 24 kHz, 16-bit PCM, mono.
- Added first-channel extraction for multichannel audio sources.
- Fixed MP3 encoding for four-channel capture devices.
- Added detailed source-format and normalization logging.
- Added a command-file build launcher.

## 1.0.1

- Replaced Media Foundation MP3 encoding with LAME through NAudio.Lame.
- Added WAV fallback when MP3 encoding fails.
- Added contest-based recording directories.
- Improved the About dialog and recording-list updates.
