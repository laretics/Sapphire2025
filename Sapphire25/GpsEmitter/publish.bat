@echo off
setlocal
cd /d "%~dp0"

set OUT=%~dp0publish\win-x64
echo.
echo  Publicando GpsEmitter como .exe autonomo en:
echo    %OUT%
echo.

dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o "%OUT%"

if errorlevel 1 (
  echo ERROR en publish.
  exit /b 1
)

echo.
echo  Listo. Copia la carpeta publish\win-x64 al PC del GPS y ejecuta GpsEmitter.exe
echo  Configuracion: appsettings.json (mismo directorio que el .exe)
echo.
exit /b 0
