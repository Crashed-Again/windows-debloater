@echo off
setlocal
REM ---------------------------------------------------------------------------
REM  Builds Cub.exe and wraps it in an installable .msi
REM  Needs: .NET 8 SDK  (https://dotnet.microsoft.com/download/dotnet/8.0)
REM         WiX v5 CLI  (installed automatically below via "dotnet tool")
REM  The version is read from <Version> in Cub.csproj - edit it there only.
REM ---------------------------------------------------------------------------
set VERSION=
for /f "usebackq delims=" %%v in (`powershell -NoProfile -Command "(Select-String -Path Cub.csproj -Pattern '<Version>(.*)</Version>').Matches[0].Groups[1].Value"`) do set VERSION=%%v
if "%VERSION%"=="" (
    echo Could not read the version from Cub.csproj
    goto fail
)
echo Building Cub %VERSION%

echo.
echo [1/3] Publishing the app...
dotnet publish -c Release -o out
if errorlevel 1 goto fail

echo.
echo [2/3] Checking for the WiX tool...
wix --version >nul 2>nul
if errorlevel 1 (
    dotnet tool install --global wix --version 5.0.2
    if errorlevel 1 goto fail
    set "PATH=%PATH%;%USERPROFILE%\.dotnet\tools"
)

echo.
echo [3/3] Building the installer...
wix build installer\Installer.wxs -arch x64 -d Version=%VERSION% -d ExePath=out\Cub.exe -d IconPath=icon.ico -o out\Cub-%VERSION%.msi
if errorlevel 1 goto fail

echo.
echo Done. Installer: %~dp0out\Cub-%VERSION%.msi
pause
exit /b 0

:fail
echo.
echo Build failed. Scroll up for the error message.
pause
exit /b 1
