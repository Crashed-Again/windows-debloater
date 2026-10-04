@echo off
REM Requires the .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
dotnet publish -c Release -o out
if errorlevel 1 (echo Build failed. & pause & exit /b 1)
echo.
echo Done. Your exe is: %~dp0out\NeonBear-Debloat.exe
pause
