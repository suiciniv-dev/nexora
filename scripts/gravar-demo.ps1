param([string[]]$Idiomas = @('en', 'pt'), [switch]$SemBuild)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent
$projeto = Join-Path $raiz 'tests\Nexora.Testes'
$destino = Join-Path $raiz 'site\img\app'
if (-not $SemBuild) { dotnet build $projeto -nologo -v q; if ($LASTEXITCODE) { throw 'A compilação falhou.' } }

foreach ($idioma in $Idiomas) {
  $saida = Join-Path ([IO.Path]::GetTempPath()) "nexora-demo-$idioma"
  $env:NEXORA_VIDEO = $saida
  $env:NEXORA_IDIOMA = $idioma
  dotnet test $projeto --no-build --nologo -v q --filter 'FullyQualifiedName~Nexora.Testes.Tela.GravarDemo'
  if ($LASTEXITCODE) { throw "A gravação em $idioma falhou." }
  $marcos = Get-Content (Join-Path $saida 'marcos.txt')
  $clique = [double](($marcos | Where-Object { $_ -like 'clique *' }) -split ' ')[1] / 1000
  $poster = [math]::Max(0, $clique - 0.2).ToString([Globalization.CultureInfo]::InvariantCulture)
  Copy-Item (Join-Path $saida 'demo.mp4') (Join-Path $destino "demo-$idioma.mp4") -Force
  ffmpeg -loglevel error -y -ss $poster -i (Join-Path $saida 'demo.mp4') -frames:v 1 -c:v libwebp -quality 82 (Join-Path $destino "demo-$idioma.webp")
  if ($LASTEXITCODE) { throw 'O pôster não foi gerado.' }
  Write-Output "demo-$idioma.mp4 e demo-$idioma.webp em $destino"
}
Remove-Item Env:NEXORA_VIDEO, Env:NEXORA_IDIOMA -ErrorAction SilentlyContinue
