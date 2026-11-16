$ErrorActionPreference = "Continue"
$raiz = Split-Path -Parent $PSScriptRoot
Set-Location $raiz
$falhas = @()

function Passo($nome, [scriptblock]$acao) {
    Write-Host "`n== $nome" -ForegroundColor Cyan
    & $acao
    if ($LASTEXITCODE -ne 0) { $script:falhas += $nome; Write-Host "FALHOU: $nome" -ForegroundColor Red }
}

Passo "Testes" { dotnet test tests/Nexora.Testes -nologo -v q }
Passo "Compilar o Nexora" { dotnet build src/Nexora -c Release -nologo -v q }

if ($falhas.Count) {
    Write-Host "`n$($falhas.Count) passo(s) falharam: $($falhas -join ', ')" -ForegroundColor Red
    exit 1
}
Write-Host "`nTudo certo" -ForegroundColor Green
