param(
    [Parameter(Mandatory)] [string]$Roteiro,
    [string]$Saida,
    [switch]$SemBuild,
    [string]$Idioma = 'pt'
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$env:NEXORA_ROTEIRO = (Resolve-Path $Roteiro).Path
$env:NEXORA_IDIOMA = $Idioma
$env:NEXORA_FOTOS = if ($Saida) { [IO.Path]::GetFullPath($Saida) } else { Join-Path (Split-Path $env:NEXORA_ROTEIRO) 'fotos' }
$projeto = Join-Path $PSScriptRoot '..\tests\Nexora.Testes'
$extra = @()
if ($SemBuild) { $extra += '--no-build' }
dotnet test $projeto @extra --filter 'FullyQualifiedName~Nexora.Testes.Tela.Passeio' --nologo -v q
$log = Join-Path $env:NEXORA_FOTOS 'log.txt'
if (Test-Path $log) { Get-Content $log -Encoding utf8 }
Write-Output "Fotos em $env:NEXORA_FOTOS"
