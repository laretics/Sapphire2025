@echo off
setlocal
cd /d "%~dp0"

echo.
echo  GpsEmitter — modo desarrollo (dotnet run)
echo  Edita appsettings.json para Port / BroadcastAddress / BroadcastPort
echo.

dotnet run -c Debug --no-launch-profile
exit /b %ERRORLEVEL%
