param([string]$Destino)
$ErrorActionPreference = "Stop"

$origem = Join-Path $PSScriptRoot "app"
if (-not (Test-Path (Join-Path $origem "Nexora.dll"))) { throw "Não achei a pasta app ao lado deste script. Extraia o .zip inteiro antes de instalar." }
if (-not $Destino) {
    $Destino = if (Test-Path "D:\") { "D:\NexoraApp" } else { Join-Path $env:USERPROFILE "NexoraApp" }
}

$dotnet = Join-Path $env:ProgramFiles "dotnet\dotnet.exe"
$comando = Get-Command dotnet -ErrorAction SilentlyContinue
if ($comando) { $dotnet = $comando.Source }
if (-not (Test-Path $dotnet)) {
    Write-Host "Falta o .NET 10. Instale com: winget install Microsoft.DotNet.Runtime.10" -ForegroundColor Yellow
    exit 1
}
if (-not (& $dotnet --list-runtimes | Select-String '^Microsoft\.NETCore\.App 10\.')) {
    Write-Host "Falta o runtime do .NET 10. Instale com: winget install Microsoft.DotNet.Runtime.10" -ForegroundColor Yellow
    exit 1
}

$aberto = @(Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" | Where-Object { $_.CommandLine -like "*$Destino\Nexora.dll*" }) +
    @(Get-CimInstance Win32_Process -Filter "Name='Nexora.exe'" | Where-Object { $_.ExecutablePath -like "$Destino\*" })
if ($aberto) {
    Write-Host "Fechando o Nexora aberto para atualizar..."
    $aberto | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
    Start-Sleep -Seconds 2
}

New-Item -ItemType Directory -Force $Destino | Out-Null
Copy-Item (Join-Path $origem "*") $Destino -Recurse -Force
Get-ChildItem $Destino -Recurse -File | Unblock-File

$atalhos = @([Environment]::GetFolderPath("Desktop"), [Environment]::GetFolderPath("Programs"))
$shell = New-Object -ComObject WScript.Shell
foreach ($pasta in $atalhos) {
    $atalho = $shell.CreateShortcut((Join-Path $pasta "Nexora.lnk"))
    $atalho.TargetPath = Join-Path $env:SystemRoot "System32\conhost.exe"
    $atalho.Arguments = "--headless `"$dotnet`" `"$Destino\Nexora.dll`""
    $atalho.WorkingDirectory = $Destino
    $atalho.IconLocation = "$Destino\Assets\nexora.ico,0"
    $atalho.Description = "Nexora"
    $atalho.Save()
}

Write-Host ""
Write-Host "Nexora instalado em $Destino" -ForegroundColor Green
Write-Host "Abra pelo atalho Nexora na Área de Trabalho ou no menu Iniciar."
