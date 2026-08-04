$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "DXLogQsoRecorder\DXLogQsoRecorder.csproj"
$output = Join-Path $root "publish\win-x64"

Write-Host "Publishing DXLog QSO Recorder v1.2.4 by RK3TD..."
dotnet publish $project -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  -o $output

Write-Host "Portable release created: $output"
