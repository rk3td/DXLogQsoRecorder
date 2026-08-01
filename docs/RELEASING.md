# Release Process

## Version update

Before publishing a release, update:

- `Version`, `AssemblyVersion`, and `FileVersion` in `DXLogQsoRecorder/DXLogQsoRecorder.csproj`;
- the version displayed by the main window and About dialog, if not read automatically from assembly metadata;
- `README.md`;
- `CHANGELOG.md`.

## Automated GitHub release

1. Commit and push the release changes.
2. Create and push an annotated tag:

```bash
git tag -a v1.1.0 -m "DXLog QSO Recorder v1.1.0"
git push origin v1.1.0
```

The `release.yml` workflow builds a self-contained Windows x64 portable package and creates a GitHub Release with the ZIP file attached.

## Manual build

Run:

```text
build-portable-win-x64.cmd
```

The output is created under `publish/win-x64`.
