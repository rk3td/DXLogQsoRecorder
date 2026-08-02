@echo off
setlocal
cd /d "%~dp0"

echo Publishing DXLog QSO Recorder v1.2.3 by RK3TD...
dotnet publish "DXLogQsoRecorder\DXLogQsoRecorder.csproj" -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:DebugType=None ^
  -p:DebugSymbols=false ^
  -o "publish\win-x64"

if errorlevel 1 (
  echo.
  echo Build failed. Make sure .NET 8 SDK is installed.
  pause
  exit /b 1
)

echo.
echo Portable release created: %CD%\publish\win-x64
pause
