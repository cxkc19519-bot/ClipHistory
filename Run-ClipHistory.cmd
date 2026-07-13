@echo off
setlocal
set "DOTNET=%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe"
set "APP=%~dp0src\ClipHistory.App\bin\Release\net8.0-windows\ClipHistory.App.dll"

if not exist "%DOTNET%" (
  echo .NET 8 SDK was not found at:
  echo %DOTNET%
  pause
  exit /b 1
)

if not exist "%APP%" (
  echo ClipHistory has not been built yet:
  echo %APP%
  pause
  exit /b 1
)

start "ClipHistory" "%DOTNET%" "%APP%"
endlocal

