param([Parameter(Mandatory = $true)][string]$Versao, [switch]$SemMac)

$ErrorActionPreference = 'Stop'
$Versao = $Versao.TrimStart('v')
if ($Versao -notmatch '^\d+(\.\d+)+$') { throw "Versão inválida: $Versao" }
$raiz = Split-Path $PSScriptRoot -Parent
$numero = '\d+(?:\.\d+)+'
$arquivos = if ($SemMac) { 'Setup|Portatil' } else { 'Setup|Portatil|Mac-AppleSilicon|Mac-Intel' }
$trocas = [ordered]@{
  'softwareVersion' = "(?<=""softwareVersion"": "")$numero(?="")"
  'nomes dos arquivos' = "(?<=Nexora-(?:$arquivos)-)$numero(?=\.(?:exe|zip|dmg))"
  'versão no texto' = "(?<=(?:versão|version) )$numero"
}
foreach ($pagina in 'site\index.html', 'site\en\index.html') {
  $caminho = Join-Path $raiz $pagina
  $html = [IO.File]::ReadAllText($caminho)
  foreach ($nome in $trocas.Keys) {
    if (-not [regex]::IsMatch($html, $trocas[$nome])) { throw "Não achei $nome em $pagina" }
    $html = [regex]::Replace($html, $trocas[$nome], $Versao)
  }
  [IO.File]::WriteAllText($caminho, $html, (New-Object System.Text.UTF8Encoding $false))
  Write-Host "$pagina na versão $Versao"
}
