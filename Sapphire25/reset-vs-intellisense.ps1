# Reset Visual Studio design-time caches for this repo.
# Close Visual Studio completely before running.

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

$devenv = Get-Process devenv -ErrorAction SilentlyContinue
if ($devenv) {
    Write-Host "Visual Studio (devenv) is still running (PIDs: $($devenv.Id -join ', '))."
    Write-Host "Close it completely and run this script again."
    exit 1
}

$paths = @(
    (Join-Path $root ".vs"),
    (Join-Path $root "Tourmaline26\bin"),
    (Join-Path $root "Tourmaline26\obj")
)

foreach ($p in $paths) {
    if (Test-Path $p) {
        Write-Host "Removing $p"
        Remove-Item -Recurse -Force $p
    }
}

Write-Host "Restoring and building Tourmaline26..."
dotnet restore (Join-Path $root "Tourmaline26\Tourmaline26.csproj")
dotnet build (Join-Path $root "Tourmaline26\Tourmaline26.csproj") --no-restore

Write-Host ""
Write-Host "Done. Open Tourmaline26.slnf (recommended) or Sapphire25.sln in Visual Studio."
Write-Host "Wait until the status bar shows the solution finished loading before testing IntelliSense."
