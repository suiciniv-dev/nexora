param(
    [string]$Cenarios = 'repouso,1,3,5',
    [int]$Minutos = 3,
    [int]$Passos = 60,
    [string]$Saida,
    [switch]$SemBuild
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$raiz = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$base = Join-Path $env:TEMP 'nexora-benchmark'
$app = Join-Path $base 'app'
$sim = Join-Path $base 'sim'
if (-not $Saida) { $Saida = Join-Path $base ('resultado-' + (Get-Date -Format 'yyyyMMdd-HHmm')) }
$Saida = [IO.Path]::GetFullPath($Saida)
New-Item -ItemType Directory -Force $Saida | Out-Null

if (-not $SemBuild) {
    Write-Output 'Publicando o Nexora (Release, ReadyToRun) e o Claude simulado...'
    dotnet publish "$raiz\src\Nexora" -c Release -r win-x64 --self-contained false -o $app -nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'O publish do Nexora falhou.' }
    dotnet build "$raiz\tools\ClaudeSimulado" -c Release -o $sim -nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'O build do Claude simulado falhou.' }
}

$nucleos = [Environment]::ProcessorCount

function NovoProjeto([string]$nome) {
    $pasta = Join-Path $base "projetos\$nome"
    if (Test-Path $pasta) { Remove-Item $pasta -Recurse -Force }
    New-Item -ItemType Directory -Force "$pasta\src" | Out-Null
    Get-ChildItem "$raiz\src\Nexora.Core" -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
        Select-Object -First 80 | ForEach-Object { Copy-Item $_.FullName "$pasta\src\$($_.Name)" }
    "passos=$Passos;builds=" | Out-File "$pasta\.sim" -Encoding ascii -NoNewline
    return $pasta
}

function Simulados {
    @(Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" | Where-Object { $_.CommandLine -like "*$sim\claude.dll*" })
}

function Mediana([double[]]$valores) {
    if ($valores.Count -eq 0) { return 0 }
    $ordem = $valores | Sort-Object
    return $ordem[[math]::Floor(($ordem.Count - 1) / 2)]
}

function Medir([string]$cenario) {
    $sessoes = if ($cenario -eq 'repouso') { 0 } else { [int]$cenario }
    $pasta = Join-Path $Saida $cenario
    New-Item -ItemType Directory -Force $pasta | Out-Null
    $argumentos = @("`"$app\Nexora.dll`"", '--isolada', '--sem-hub', '--sem-atualizacao', '--janela', '1440x900', '--desempenho', "`"$pasta`"")
    if ($sessoes -gt 0) {
        $projetos = 1..$sessoes | ForEach-Object { NovoProjeto "p$_" }
        $argumentos += @('--pasta', "`"$($projetos -join ';')`"", '--enviar', '"Resolva a issue"', '--claude-simulado', "`"$sim\claude.dll`"")
    }
    $relogio = [Diagnostics.Stopwatch]::StartNew()
    $processo = Start-Process dotnet -ArgumentList ($argumentos -join ' ') -PassThru
    while ($processo.MainWindowHandle -eq 0 -and $relogio.Elapsed.TotalSeconds -lt 60 -and -not $processo.HasExited) {
        Start-Sleep -Milliseconds 50
        $processo.Refresh()
    }
    $abertura = $relogio.Elapsed.TotalSeconds
    $amostras = @()
    $cpuAntes = $processo.TotalProcessorTime.TotalMilliseconds
    $tempoAntes = $relogio.Elapsed.TotalMilliseconds
    while ($relogio.Elapsed.TotalMinutes -lt $Minutos -and -not $processo.HasExited) {
        Start-Sleep -Seconds 2
        $processo.Refresh()
        $cpu = $processo.TotalProcessorTime.TotalMilliseconds
        $agora = $relogio.Elapsed.TotalMilliseconds
        $agentes = Simulados
        $amostras += [pscustomobject]@{
            Seg = [math]::Round($agora / 1000)
            Privada = $processo.PrivateMemorySize64 / 1MB
            EmUso = $processo.WorkingSet64 / 1MB
            Cpu = 100 * ($cpu - $cpuAntes) / ($agora - $tempoAntes)
            Agentes = $agentes.Count
            AgentesMb = ($agentes | Measure-Object WorkingSetSize -Sum).Sum / 1MB
        }
        $cpuAntes = $cpu
        $tempoAntes = $agora
    }
    if (-not $processo.HasExited) { Stop-Process -Id $processo.Id -Force }
    Simulados | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    $amostras | Export-Csv "$pasta\amostras.csv" -NoTypeInformation -Encoding utf8
    $atraso = 0
    Get-ChildItem $pasta -Filter 'amostras-*.jsonl' -ErrorAction SilentlyContinue | ForEach-Object {
        Get-Content $_.FullName -Encoding utf8 | ForEach-Object { $atraso = [math]::Max($atraso, ($_ | ConvertFrom-Json).AtrasoMaximo) }
    }
    $metade = @($amostras | Select-Object -Skip ([math]::Floor($amostras.Count / 2)))
    [pscustomobject]@{
        Cenario = if ($sessoes -eq 0) { 'Em repouso' } elseif ($sessoes -eq 1) { '1 sessão ativa' } else { "$sessoes sessões ativas" }
        Abertura = [math]::Round($abertura, 1)
        Privada = [math]::Round((Mediana ($metade | ForEach-Object Privada)))
        Pico = [math]::Round(($amostras | Measure-Object Privada -Maximum).Maximum)
        EmUso = [math]::Round((Mediana ($metade | ForEach-Object EmUso)))
        Cpu = [math]::Round(($amostras | Measure-Object Cpu -Average).Average, 1)
        CpuPico = [math]::Round(($amostras | Measure-Object Cpu -Maximum).Maximum, 1)
        Atraso = $atraso
    }
}

$resultados = foreach ($cenario in $Cenarios.Split(',')) {
    Write-Host "Medindo: $cenario ($Minutos min)"
    Medir $cenario.Trim()
}

$maquina = Get-CimInstance Win32_Processor | Select-Object -First 1
$memoria = [math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB)
$linhas = @(
    "# Benchmark do Nexora",
    "",
    "$(Get-Date -Format 'dd/MM/yyyy HH:mm') · $($maquina.Name.Trim()) ($nucleos núcleos) · $memoria GB · $Minutos min por cenário · agentes simulados com $Passos passos",
    "",
    "| Cenário | Abertura da janela | RAM privada (mediana) | RAM privada (pico) | RAM em uso | CPU média | CPU pico | Maior travada da tela |",
    "|---|---|---|---|---|---|---|---|"
)
foreach ($r in $resultados) {
    $linhas += "| $($r.Cenario) | $($r.Abertura) s | $($r.Privada) MB | $($r.Pico) MB | $($r.EmUso) MB | $($r.Cpu)% | $($r.CpuPico)% | $($r.Atraso) ms |"
}
$linhas += ""
$linhas += "Só o processo do Nexora. CPU em % de um núcleo. Os agentes são o Claude simulado (tools/ClaudeSimulado), que manda o mesmo tipo de evento que o claude real sem gastar tokens; o consumo do claude real é o mesmo em qualquer app e fica de fora."
$linhas | Out-File "$Saida\benchmark.md" -Encoding utf8
$linhas | ForEach-Object { Write-Output $_ }
Write-Output "Resultado em $Saida"
