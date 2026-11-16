param([Parameter(Mandatory = $true)][string]$Tag)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent
$notas = Join-Path $raiz "notas\$Tag.md"
if (-not (Test-Path $notas)) { throw "Faltam as notas em notas\$Tag.md" }

$env:GH_TOKEN = (gh auth token --hostname github.com --user suiciniv-dev)
if (-not $env:GH_TOKEN) { throw 'O gh não está logado na suiciniv-dev' }

$pasta = Join-Path $env:TEMP "nexora-release-$Tag"
if (Test-Path $pasta) { Remove-Item $pasta -Recurse -Force }
gh release download $Tag -R suiciniv-dev/nexora -p 'Nexora-Setup-*.exe' -p 'Nexora-Portatil-*.zip' -p 'Nexora-Mac-*.dmg' -D $pasta
$arquivos = @(Get-ChildItem $pasta -File | ForEach-Object { $_.FullName })
if (-not ($arquivos | Where-Object { $_ -like '*Nexora-Setup-*.exe' })) { throw "O release $Tag do repositório privado não tem instalador" }

gh release create $Tag $arquivos -R suiciniv-dev/nexora-releases --title "Nexora $($Tag.TrimStart('v'))" --notes-file $notas
Remove-Item $pasta -Recurse -Force

$versao = $Tag.TrimStart('v')
$redirects = Join-Path $raiz "site\_redirects"
$base = "https://github.com/suiciniv-dev/nexora-releases/releases/download/$Tag"
$texto = "/baixar $base/Nexora-Setup-$versao.exe 302`n/baixar/portatil $base/Nexora-Portatil-$versao.zip 302`n"
$anteriores = if (Test-Path $redirects) { @(Get-Content $redirects) } else { @() }
if ($arquivos | Where-Object { $_ -like '*Nexora-Mac-AppleSilicon-*.dmg' }) { $texto += "/baixar/mac $base/Nexora-Mac-AppleSilicon-$versao.dmg 302`n" }
else { $anteriores | Where-Object { $_ -like '/baixar/mac *' } | ForEach-Object { $texto += "$_`n" } }
if ($arquivos | Where-Object { $_ -like '*Nexora-Mac-Intel-*.dmg' }) { $texto += "/baixar/mac-intel $base/Nexora-Mac-Intel-$versao.dmg 302`n" }
else { $anteriores | Where-Object { $_ -like '/baixar/mac-intel *' } | ForEach-Object { $texto += "$_`n" } }
[IO.File]::WriteAllText($redirects, $texto, (New-Object System.Text.UTF8Encoding $false))
$semMac = -not ($arquivos | Where-Object { $_ -like '*Nexora-Mac-*.dmg' })
& (Join-Path $PSScriptRoot 'versao-no-site.ps1') -Versao $versao -SemMac:$semMac
Write-Host "Release pública $Tag criada, com o site\_redirects e a versão do site em português e em inglês trocados. Confira e publique o site."
