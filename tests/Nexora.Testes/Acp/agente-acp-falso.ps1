$ErrorActionPreference = 'Stop'
[Console]::InputEncoding = [Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
if ($env:FALSO_ANTIGO -eq '1') { [Console]::Error.WriteLine('error: unexpected argument "acp" found'); exit 2 }

function Mandar($objeto) {
    $objeto['jsonrpc'] = '2.0'
    [Console]::Out.WriteLine(($objeto | ConvertTo-Json -Compress -Depth 20))
    [Console]::Out.Flush()
}

function Ler {
    while ($true) {
        $linha = [Console]::In.ReadLine()
        if ($null -eq $linha) { exit 0 }
        if ($linha.Trim().Length -gt 0) { return ($linha | ConvertFrom-Json) }
    }
}

function Atualizar($sessao, $update) {
    Mandar @{ method = 'session/update'; params = @{ sessionId = $sessao; update = $update } }
}

function Texto($sessao, $tipo, $texto) {
    Atualizar $sessao @{ sessionUpdate = $tipo; content = @{ type = 'text'; text = $texto } }
}

function Esperar($id) {
    $resposta = Ler
    while ($resposta.id -ne $id) { $resposta = Ler }
    return $resposta
}

$logado = $env:FALSO_LOGIN -ne '1' -and $env:FALSO_LOGIN -ne '2'
$modo = 'default'
$pasta = ''
[Console]::Out.WriteLine('agente falso iniciando, isto nao e json')
[Console]::Out.Flush()

while ($true) {
    $m = Ler
    switch ($m.method) {
        'initialize' {
            Mandar @{ id = $m.id; result = @{
                protocolVersion = 1
                agentInfo = @{ name = 'agente-falso'; version = '1.0.0' }
                agentCapabilities = @{ loadSession = $true; promptCapabilities = @{ image = $true }; mcpCapabilities = @{ http = $true; sse = $false } }
                authMethods = @(@{ id = 'entrar'; name = 'Entrar' })
            } }
        }
        'authenticate' {
            if ($env:FALSO_LOGIN -eq '2') {
                Mandar @{ id = $m.id; error = @{ code = -32000; message = 'Login recusado' } }
                break
            }
            $logado = $true
            Mandar @{ id = $m.id; result = @{} }
        }
        'session/new' {
            if (-not $logado) {
                Mandar @{ id = $m.id; error = @{ code = -32000; message = 'Authentication required' } }
                break
            }
            $pasta = $m.params.cwd
            Mandar @{ id = $m.id; result = @{
                sessionId = 'ses_falso'
                models = @{ currentModelId = 'modelo-a'; availableModels = @(@{ modelId = 'modelo-a'; name = 'Modelo A' }, @{ modelId = 'modelo-b'; name = 'Modelo B'; description = 'O outro' }) }
                modes = @{ currentModeId = 'default'; availableModes = @(@{ id = 'default'; name = 'Default' }, @{ id = 'plan'; name = 'Plan' }) }
            } }
            Atualizar 'ses_falso' @{ sessionUpdate = 'available_commands_update'; availableCommands = @(@{ name = 'revisar'; description = 'Revisa o codigo'; input = @{ hint = 'arquivo' } }) }
        }
        'session/load' {
            $pasta = $m.params.cwd
            $sessao = $m.params.sessionId
            Texto $sessao 'user_message_chunk' 'pergunta '
            Texto $sessao 'user_message_chunk' 'antiga'
            Texto $sessao 'agent_message_chunk' 'resposta antiga'
            Mandar @{ id = $m.id; result = @{ modes = @{ currentModeId = 'default'; availableModes = @(@{ id = 'default'; name = 'Default' }) } } }
        }
        'session/set_mode' {
            $modo = $m.params.modeId
            Mandar @{ id = $m.id; result = @{} }
        }
        'session/set_model' { Mandar @{ id = $m.id; result = @{} } }
        'session/cancel' { }
        'session/prompt' {
            $sessao = $m.params.sessionId
            $blocos = @($m.params.prompt)
            $texto = $blocos[$blocos.Count - 1].text
            if ($texto -eq 'demorar') {
                Texto $sessao 'agent_message_chunk' 'Vou demorar'
                Mandar @{ id = 950; method = 'session/request_permission'; params = @{ sessionId = $sessao; toolCall = @{ toolCallId = 'longo'; title = 'sleep 100'; kind = 'execute' }; options = @(@{ optionId = 'o1'; name = 'Permitir'; kind = 'allow_once' }) } }
                $cancelou = $false
                $permissao = ''
                while (-not $cancelou -or $permissao -eq '') {
                    $r = Ler
                    if ($r.method -eq 'session/cancel') { $cancelou = $true }
                    elseif ($r.id -eq 950) { $permissao = $r.result.outcome.outcome }
                }
                Texto $sessao 'agent_message_chunk' " permissao=$permissao"
                Mandar @{ id = $m.id; result = @{ stopReason = 'cancelled' } }
                break
            }
            if ($texto -eq 'depois') {
                Texto $sessao 'agent_message_chunk' 'Recebi depois'
                Mandar @{ id = $m.id; result = @{ stopReason = 'end_turn' } }
                break
            }
            Atualizar $sessao @{ sessionUpdate = 'agent_thought_chunk'; content = @{ type = 'text'; text = 'pensando' } }
            Texto $sessao 'agent_message_chunk' 'Recebi: '
            Texto $sessao 'agent_message_chunk' "$texto (modo=$modo, blocos=$($blocos.Count))"
            Atualizar $sessao @{ sessionUpdate = 'tool_call'; toolCallId = 't1'; title = 'Rodar testes'; kind = 'execute'; status = 'pending'; rawInput = @{ command = 'dotnet test' } }
            Mandar @{ id = 900; method = 'session/request_permission'; params = @{ sessionId = $sessao; toolCall = @{ toolCallId = 't1'; title = 'Rodar testes'; kind = 'execute'; rawInput = @{ command = 'dotnet test' } }; options = @(@{ optionId = 'o1'; name = 'Permitir'; kind = 'allow_once' }, @{ optionId = 'a1'; name = 'Sempre'; kind = 'allow_always' }, @{ optionId = 'r1'; name = 'Negar'; kind = 'reject_once' }) } }
            $resposta = Esperar 900
            $escolha = $resposta.result.outcome.optionId
            Atualizar $sessao @{ sessionUpdate = 'tool_call_update'; toolCallId = 't1'; status = 'completed'; content = @(@{ type = 'content'; content = @{ type = 'text'; text = "escolha=$escolha" } }) }
            Mandar @{ id = 901; method = 'fs/read_text_file'; params = @{ sessionId = $sessao; path = (Join-Path $pasta 'leia.txt') } }
            $lido = Esperar 901
            Mandar @{ id = 902; method = 'terminal/create'; params = @{ sessionId = $sessao; command = 'ls' } }
            $terminal = Esperar 902
            Mandar @{ id = 903; method = 'fs/read_text_file'; params = @{ sessionId = $sessao; path = 'C:\Windows\win.ini' } }
            $fora = Esperar 903
            Atualizar $sessao @{ sessionUpdate = 'usage_update'; used = 1234; size = 200000 }
            Texto $sessao 'agent_message_chunk' "lido=$($lido.result.content) terminal=$($terminal.error.code) fora=$($null -ne $fora.error)"
            Mandar @{ id = $m.id; result = @{ stopReason = 'end_turn' } }
        }
        default { if ($null -ne $m.id) { Mandar @{ id = $m.id; error = @{ code = -32601; message = "falso nao conhece $($m.method)" } } } }
    }
}
