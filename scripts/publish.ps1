# Rebuilds Lada in Release mode as a framework-dependent app,
# deploys it to %LocalAppData%\Lada\, and (re)registers it to start with
# Windows. Re-run this any time there's a new build to push out.
#
# Must publish Lada.csproj directly, not the .sln: publishing the solution
# also tries to single-file-publish Lada.Tests, which fails (a test class
# library has no apphost to bundle into).

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$installDir = Join-Path $env:LOCALAPPDATA "Lada"
$exePath = Join-Path $installDir "Lada.exe"
$stagingDir = Join-Path $env:TEMP "LadaPublish"

Write-Host "Fermeture de Lada s'il tourne..."
Get-Process -Name "Lada" -ErrorAction SilentlyContinue | Stop-Process -Force

if (Test-Path $stagingDir) {
    Remove-Item $stagingDir -Recurse -Force
}

Write-Host "Publication (Release)..."
# Windows App SDK's documented Win32 Acrylic controller needs its projection
# and bootstrap assemblies beside the executable. Keeping this as a compact
# framework-dependent folder avoids the ~190 MB extracted single-file bundle.
dotnet publish (Join-Path $repoRoot "Lada.csproj") -c Release -r win-x64 --self-contained false -o $stagingDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish a échoué."
}

Write-Host "Déploiement vers $installDir..."
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item (Join-Path $stagingDir "*") $installDir -Recurse -Force

Write-Host "Enregistrement du démarrage automatique..."
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "Lada" -Value "`"$exePath`""

Write-Host "Lancement de la nouvelle version..."
Start-Process $exePath

Write-Host "Terminé. Lada est installé dans $installDir et démarre avec Windows."
