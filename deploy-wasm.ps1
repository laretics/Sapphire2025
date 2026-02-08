# ============================================
# Script de despliegue - Sapphire2025 (WASM)
# ============================================

$wasmProject = "Sapphire2025"
$publishPath = ".\publish-wasm"
$remoteHost = "zafiro"
$remotePath = "/home/Zafiro/Client"

Write-Host ""
Write-Host "Iniciando despliegue de Sapphire2025 (Blazor WASM)" -ForegroundColor Cyan
Write-Host ""

# 1. Compilar proyecto Blazor WASM
Write-Host "Compilando proyecto Blazor WASM..." -ForegroundColor Yellow
dotnet publish $wasmProject -c Release -o $publishPath

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: No se pudo compilar el proyecto" -ForegroundColor Red
    exit 1
}

# 2. Verificar que existe wwwroot
if (-not (Test-Path "$publishPath\wwwroot")) {
    Write-Host "ERROR: No se encontro la carpeta wwwroot" -ForegroundColor Red
    exit 1
}

# 3. Crear paquete con tar (sin backslashes)
Write-Host "Creando paquete de despliegue..." -ForegroundColor Yellow
Push-Location "$publishPath\wwwroot"
tar -czf ..\..\wasm-deploy.tar.gz *
Pop-Location

# 4. Subir al servidor
Write-Host "Subiendo al servidor..." -ForegroundColor Green
scp .\wasm-deploy.tar.gz ${remoteHost}:/tmp/

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: No se pudo subir al servidor" -ForegroundColor Red
    Remove-Item .\wasm-deploy.tar.gz -ErrorAction SilentlyContinue
    exit 1
}

# 5. Desplegar en el servidor
Write-Host "Desplegando en el servidor..." -ForegroundColor Green

ssh $remoteHost "mkdir -p $remotePath"
ssh $remoteHost "cd $remotePath && rm -rf *"
ssh $remoteHost "tar -xzf /tmp/wasm-deploy.tar.gz -C $remotePath"
ssh $remoteHost "chown -R www-data:www-data $remotePath"
ssh $remoteHost "chmod -R 755 $remotePath"
ssh $remoteHost "rm -f /tmp/wasm-deploy.tar.gz"

# 6. Limpiar archivos temporales locales
Write-Host "Limpiando archivos temporales..." -ForegroundColor Yellow
Remove-Item .\wasm-deploy.tar.gz -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force $publishPath -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "DESPLIEGUE WASM COMPLETADO" -ForegroundColor Green
Write-Host ""
Write-Host "Archivos desplegados en: $remotePath" -ForegroundColor Cyan
