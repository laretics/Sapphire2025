# Configuración
$remoteHost = "zafiro"
$remoteUser = "zafiroextern"
$remoteDb = "zafiro"
$remotePassword = "zafiroextern2233"
$remotePort = 4406
$backupFile = "/tmp/zafiro-backup.sql"
$localBackupPath = "C:\Users\ErPe\Backups\zafiro-backup.sql"

Write-Host "Iniciando backup de la base de datos..." -ForegroundColor Cyan

# 1. Crear backup en el servidor
$dumpCmd = "mysqldump -u$remoteUser -p'$remotePassword' -h127.0.0.1 -P$remotePort $remoteDb > $backupFile"
ssh $remoteHost $dumpCmd

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: No se pudo crear el backup en el servidor" -ForegroundColor Red
    exit 1
}

Write-Host "Descargando backup a equipo local..." -ForegroundColor Yellow
scp ($remoteHost + ":" + $backupFile) $localBackupPath

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: No se pudo descargar el backup" -ForegroundColor Red
    exit 1
}

# 3. Borrar backup temporal en el servidor
ssh $remoteHost "rm -f $backupFile"

Write-Host "Backup completado: $localBackupPath" -ForegroundColor Green
