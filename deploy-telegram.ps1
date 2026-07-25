# ============================================
# Script de despliegue - Sapphire2026Telegram
# ============================================

$solutionRoot = "C:\Users\ErPe\source\repos\laretics\Sapphire2025\Sapphire25"
$workerProject = "$solutionRoot\Sapphire2026Telegram\Sapphire2026Telegram.csproj"
$publishPath = "$solutionRoot\publish-telegram"
$remoteHost = "zafiro"
$remotePath = "/home/Zafiro/TelegramBot"
$serviceName = "sapphire-telegram"

# Cambiar al directorio raíz de la solución
Set-Location $solutionRoot

# Verificar que el proyecto existe
if (-not (Test-Path $workerProject)) {
    Write-Host "ERROR: No se encuentra el proyecto en: $workerProject" -ForegroundColor Red
    Get-ChildItem "$solutionRoot\Sapphire2026Telegram" -Filter "*.csproj" | ForEach-Object {
        Write-Host "Encontrado: $($_.FullName)" -ForegroundColor Yellow
    }
    exit 1
}

Write-Host "Proyecto encontrado: $workerProject" -ForegroundColor Green
# 1. Compilar proyecto
Write-Host "Compilando proyecto..." -ForegroundColor Yellow
dotnet publish Sapphire2026Telegram\Sapphire2026Telegram.csproj -c Release -o .\publish-telegram --self-contained true -r linux-x64
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: No se pudo compilar el proyecto" -ForegroundColor Red
    exit 1
}

# 1b. Config de producción (si existe)
$configSource = "C:\Users\ErPe\DeployConfigs\appsettings.Production.Telegram.json"
if (Test-Path $configSource) {
    Write-Host "Copiando configuracion de produccion..." -ForegroundColor Yellow
    Copy-Item $configSource -Destination "$publishPath\appsettings.Production.json" -Force
    # Asegura ApiBaseAddress aunque falte en el fichero de DeployConfigs
    $prodJson = Get-Content "$publishPath\appsettings.Production.json" -Raw | ConvertFrom-Json
    if (-not $prodJson.PSObject.Properties['ApiBaseAddress']) {
        $prodJson | Add-Member -NotePropertyName ApiBaseAddress -NotePropertyValue "http://localhost:5000/api/"
        $prodJson | ConvertTo-Json -Depth 10 | Set-Content "$publishPath\appsettings.Production.json" -Encoding UTF8
    }
}

# 2. Crear paquete con tar (sin backslashes)
Write-Host "Creando paquete de despliegue..." -ForegroundColor Yellow
Push-Location $publishPath
tar -czf ..\telegram-deploy.tar.gz *
Pop-Location

# 3. Subir al servidor
Write-Host "Subiendo al servidor..." -ForegroundColor Green
scp .\telegram-deploy.tar.gz ${remoteHost}:/tmp/

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: No se pudo subir al servidor" -ForegroundColor Red
    Remove-Item .\telegram-deploy.tar.gz -ErrorAction SilentlyContinue
    exit 1
}

# 4. Desplegar en el servidor
Write-Host "Desplegando en el servidor..." -ForegroundColor Green

ssh $remoteHost "systemctl stop $serviceName"
ssh $remoteHost "mkdir -p $remotePath"
ssh $remoteHost "cd $remotePath && rm -rf *"
ssh $remoteHost "tar -xzf /tmp/telegram-deploy.tar.gz -C $remotePath"
ssh $remoteHost "chown -R zafiro:zafiro $remotePath"
ssh $remoteHost "systemctl start $serviceName"
ssh $remoteHost "systemctl status $serviceName --no-pager -l"
ssh $remoteHost "rm -f /tmp/telegram-deploy.tar.gz"

# 5. Limpiar archivos temporales locales
Write-Host "Limpiando archivos temporales..." -ForegroundColor Yellow
Remove-Item .\telegram-deploy.tar.gz -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force $publishPath -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "DESPLIEGUE COMPLETADO" -ForegroundColor Green
Write-Host ""
Write-Host "Ver logs: ssh zafiro 'journalctl -u $serviceName -n 50 -f'" -ForegroundColor Cyan
Write-Host "Estado: ssh zafiro 'systemctl status $serviceName'" -ForegroundColor Cyan