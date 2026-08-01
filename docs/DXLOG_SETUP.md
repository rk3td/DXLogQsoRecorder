# DXLog.net Setup

DXLog QSO Recorder listens for DXLog.net `contactinfo` XML messages over UDP.

Recommended local configuration:

```text
Destination address: 127.0.0.1
Destination port:    12060
```

Use the same bind address and port in DXLog QSO Recorder.

## Basic test

1. Start DXLog QSO Recorder.
2. Select the required Windows audio capture device.
3. Confirm that the audio level indicator reacts.
4. Start the recorder.
5. Enter a test QSO in DXLog.net.
6. Wait for the configured post-buffer period.
7. Check the contest subdirectory under `Recordings`.

Example:

```text
Recordings/CQ-M/20260730_142804_RT2C_RN3TT_21MHz_CW.mp3
```

If MP3 encoding fails, the recording is retained as WAV instead.
