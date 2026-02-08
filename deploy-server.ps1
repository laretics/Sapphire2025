# ============================================
# Script de despliegue - Sapphire2025Server
# ============================================

$serverProject = "Sapphire2025Server"
$publishPath = ".\publish-server"
$configSource = "C:\Users\ErPe\DeployConfigs\appsettings.Production.Server.json"
$remoteHost = "zafiro"
$remotePath = "/home/Zafiro/Server"

Write-Host ""
Write-Host "Iniciando despliegue de Sapphire2025Server" -ForegroundColor Cyan
Write-Host ""

# 1. Compilar proyecto
Write-Host "Compilando proyecto..." -ForegroundColor Yellow
dotnet publish $serverProject -c Release -o $publishPath --self-contained false

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: No se pudo compilar el proyecto" -ForegroundColor Red
    exit 1
}

# 2. Verificar archivo de configuracion
if (-not (Test-Path $configSource)) {
    Write-Host "ERROR: No se encontro el archivo: $configSource" -ForegroundColor Red
    exit 1
}

Write-Host "Copiando configuracion de produccion..." -ForegroundColor Yellow
Copy-Item $configSource -Destination "$publishPath\appsettings.Production.json" -Force

# 3. Crear paquete con tar (sin backslashes)
Write-Host "Creando paquete de despliegue..." -ForegroundColor Yellow
Push-Location $publishPath
tar -czf ..\server-deploy.tar.gz *
Pop-Location

# 4. Subir al servidor
Write-Host "Subiendo al servidor..." -ForegroundColor Green
scp .\server-deploy.tar.gz ${remoteHost}:/tmp/

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: No se pudo subir al servidor" -ForegroundColor Red
    Remove-Item .\server-deploy.tar.gz -ErrorAction SilentlyContinue
    exit 1
}

# 5. Desplegar en el servidor
Write-Host "Desplegando en el servidor..." -ForegroundColor Green

ssh $remoteHost "systemctl stop sapphire2025"
ssh $remoteHost "cd $remotePath && rm -rf *"
ssh $remoteHost "tar -xzf /tmp/server-deploy.tar.gz -C $remotePath"
ssh $remoteHost "chown -R zafiro:zafiro $remotePath"
ssh $remoteHost "systemctl start sapphire2025"
ssh $remoteHost "systemctl status sapphire2025 --no-pager -l"
ssh $remoteHost "rm -f /tmp/server-deploy.tar.gz"

# 6. Limpiar archivos temporales locales
Write-Host "Limpiando archivos temporales..." -ForegroundColor Yellow
Remove-Item .\server-deploy.tar.gz -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force $publishPath -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "DESPLIEGUE COMPLETADO" -ForegroundColor Green
Write-Host ""
Write-Host "Ver logs: ssh zafiro 'journalctl -u sapphire2025 -n 50'" -ForegroundColor Cyan
