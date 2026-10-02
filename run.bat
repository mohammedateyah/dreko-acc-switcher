@echo off
set PATH=%LOCALAPPDATA%\Microsoft\dotnet;%PATH%
cd /d "%~dp0"
dotnet run -c Release --project DrekoAccSwitcher.csproj
