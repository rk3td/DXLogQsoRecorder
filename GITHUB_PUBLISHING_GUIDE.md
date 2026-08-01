# GitHub Publishing Guide

This package is ready to become the root of a GitHub repository.

## 1. Create the repository

Recommended repository name:

```text
DXLogQsoRecorder
```

Suggested description:

```text
Portable per-QSO audio recorder for DXLog.net on Windows 10/11.
```

Recommended settings:

- Visibility: Public
- Initialize with README: No
- Add `.gitignore`: No
- Add license: No

The package already includes those files.

## 2. Upload with Git

Open a terminal in the extracted package directory:

```bash
git init
git branch -M main
git add .
git commit -m "Initial public release"
git remote add origin https://github.com/RK3TD/DXLogQsoRecorder.git
git push -u origin main
```

Replace the repository URL if the GitHub account or repository name differs.

## 3. Check GitHub Actions

Open the **Actions** tab. The `Build` workflow should restore, build, publish, and upload a Windows x64 artifact.

If Actions are disabled for the repository, enable them in **Settings → Actions → General**.

## 4. Publish the first release

After confirming the build succeeds, create and push a version tag:

```bash
git tag -a v1.1.0 -m "DXLog QSO Recorder v1.1.0"
git push origin v1.1.0
```

The `Release` workflow will create:

```text
DXLogQsoRecorder-v1.1.0-win-x64-portable.zip
```

and attach it to a new GitHub Release.

## 5. Recommended repository settings

- Enable Issues.
- Enable Discussions only if you want a separate place for general questions.
- Enable private vulnerability reporting under **Settings → Security**.
- Protect the `main` branch after the first successful workflow run.
- Require the `Build` status check for pull requests if outside contributions are accepted.

## 6. Add screenshots

Place clean screenshots in `screenshots/` and reference them from `README.md`. Do not include private callsigns, recordings, unrelated desktop windows, or sensitive paths.

## 7. Optional repository topics

```text
dxlog
amateur-radio
ham-radio
contest-logging
audio-recorder
wpf
dotnet
windows
portable
```
