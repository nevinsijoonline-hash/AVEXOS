@echo off
setlocal
set "DOTNET_ROOT=C:\Users\Nevin Sijo\AppData\Local\Microsoft\dotnet"
set "PATH=%DOTNET_ROOT%;%PATH%"
cd /d "C:\Users\Nevin Sijo\Documents\Qoder\2026-11-07\9f0855d1\src\AVEXOS.App\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64"
echo === launching ===
AVEXOS.App.exe
echo === exited with code %ERRORLEVEL% ===
