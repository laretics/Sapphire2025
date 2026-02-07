# ============================================
# Script de despliegue - Sapphire2025Server
# ============================================

# Configuración
$serverProject = "Sapphire2025Server"
$publishPath = ".\publish-server"
$configSource = "C:\Users\ErPe\DeployConfigs\appsettings.Production.Server.json"
$remoteHost = "zafiro"
$remotePath = "/home/Zafiro/Server"

Write-Host "`n🚀 Iniciando despliegue de Sapphire2025Server`n" -ForegroundColor Cyan

# 1. Compilar proyecto
Write-Host "🔨 Compilando proyecto..." -ForegroundColor Yellow
dotnet publish $serverProject -c Release -o $publishPath --self-contained false

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Error al compilar el proyecto" -ForegroundColor Red
    exit 1
}

# 2. Verificar archivo de configuración
if (-not (Test-Path $configSource)) {
    Write-Host "❌ No se encontró el archivo de configuración: $configSource" -ForegroundColor Red
    exit 1
}

Write-Host "📝 Copiando configuración de producción..." -ForegroundColor Yellow
Copy-Item $configSource -Destination "$publishPath\appsettings.Production.json" -Force

# 3. Crear paquete ZIP
Write-Host "📦 Creando paquete de despliegue..." -ForegroundColor Yellow
Compress-Archive -Path "$publishPath\*" -DestinationPath ".\server-deploy.zip" -Force

# 4. Subir al servidor
Write-Host "⬆️  Subiendo al servidor..." -ForegroundColor Green
scp .\server-deploy.zip ${remoteHost}:/tmp/

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Error al subir archivos al servidor" -ForegroundColor Red
    Remove-Item .\server-deploy.zip -ErrorAction SilentlyContinue
    exit 1
}

# 5. Desplegar en el servidor
Write-Host "🔄 Desplegando en el servidor..." -ForegroundColor Green
ssh $remoteHost @"
    echo '⏸️  Deteniendo servicio...'
    sudo systemctl stop sapphire2025
    
    echo '🗑️  Limpiando directorio...'
    cd $remotePath
    sudo rm -rf *
    
    echo '📂 Descomprimiendo archivos...'
    sudo unzip -q /tmp/server-deploy.zip -d $remotePath
    
    echo '🔐 Ajustando permisos...'
    sudo chown -R zafiro:zafiro $remotePath
    
    echo '▶️  Iniciando servicio...'
    sudo systemctl start sapphire2025
    
    echo '📊 Estado del servicio:'
    sudo systemctl status sapphire2025 --no-pager -l
    
    echo '🧹 Limpiando temporal...'
    rm /tmp/server-deploy.zip
    
    echo '✅ Despliegue completado en el servidor'
"@

# 6. Limpiar archivos temporales locales
Write-Host "🧹 Limpiando archivos temporales..." -ForegroundColor Yellow
Remove-Item .\server-deploy.zip -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force $publishPath -ErrorAction SilentlyContinue

Write-Host "`n✅ ¡Despliegue completado exitosamente!`n" -ForegroundColor Green
Write-Host "📋 Ver logs en tiempo real: " -NoNewline
Write-Host "ssh zafiro 'sudo journalctl -u sapphire2025 -f'" -ForegroundColor Cyan
Write-Host "📊 Ver estado del servicio: " -NoNewline
Write-Host "ssh zafiro 'sudo systemctl status sapphire2025'" -ForegroundColor Cyan