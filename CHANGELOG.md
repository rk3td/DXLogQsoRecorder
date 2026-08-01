# Changelog

## 1.1.0 — English edition

- Added GitHub repository support files, CI build, automated tagged releases, issue forms, and contribution documentation.
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
