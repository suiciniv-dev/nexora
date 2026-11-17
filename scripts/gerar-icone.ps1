$ErrorActionPreference = "Stop"
$raiz = Split-Path -Parent $PSScriptRoot
$gerador = Join-Path $env:USERPROFILE ".claude\skills\icone-estilo-toca\scripts\gerar_icone.py"
$saida = Join-Path $env:TEMP "nexora-icone"

python $gerador --glifo "$raiz\docs\icone\glifo.svg" --glifo-pequeno "$raiz\docs\icone\glifo-16.svg" `
    --topo "#2C8EE8" --base "#2443C9" --nome Nexora --saida $saida `
    --largura-glifo 0.46 --sombra-cor "#071A40" --glifo-claro "#E2F0FF"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$assets = "$raiz\src\Nexora\Assets"
Copy-Item "$saida\Nexora.ico" "$assets\nexora.ico" -Force
Copy-Item "$saida\png\nexora-256.png" "$assets\nexora.png" -Force
Copy-Item "$saida\png\nexora-48.png" "$assets\marca.png" -Force
Copy-Item "$saida\nexora-icone.svg", "$saida\nexora-icone-plano.svg", "$saida\nexora-icone-pequeno.svg", "$saida\favicon.svg" "$raiz\docs\icone" -Force
Write-Host "Ícone gerado em $assets" -ForegroundColor Green
