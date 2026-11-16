$ErrorActionPreference = "Stop"
$raiz = Split-Path -Parent $PSScriptRoot
$destino = "D:\NexoraApp"
$temporaria = Join-Path $env:TEMP "nexora-publicar"

if ((Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" | Where-Object { $_.CommandLine -like "*NexoraApp\Nexora.dll*" }) -or
    (Get-CimInstance Win32_Process -Filter "Name='Nexora.exe'" | Where-Object { $_.ExecutablePath -like "$destino\*" })) {
    throw "Feche o Nexora antes de publicar."
}
if (Test-Path $temporaria) { Remove-Item $temporaria -Recurse -Force }
dotnet publish "$raiz\src\Nexora" -c Release -r win-x64 --self-contained false -o $temporaria -nologo -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Get-ChildItem $temporaria -Recurse -Filter *.pdb | Remove-Item
if (Test-Path "$destino\runtimes") { Remove-Item "$destino\runtimes" -Recurse -Force }
robocopy $temporaria $destino /E /NJH /NJS /NFL /NDL /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "A cópia para $destino falhou (robocopy $LASTEXITCODE)." }
Get-ChildItem $destino -File | Where-Object { -not (Test-Path (Join-Path $temporaria $_.Name)) -and $_.Name -notlike "unins*" } | Remove-Item -Force
Remove-Item $temporaria -Recurse -Force
Write-Host "Publicado em $destino" -ForegroundColor Green
exit 0
