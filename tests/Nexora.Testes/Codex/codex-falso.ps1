$ErrorActionPreference = 'Stop'
[Console]::InputEncoding = [Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)

function Mandar($objeto) {
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

while ($true) {
    $m = Ler
    switch ($m.method) {
        'initialize' { Mandar @{ id = $m.id; result = @{ userAgent = 'codex-falso' } } }
        'initialized' { }
        'thread/start' {
            Mandar @{ id = $m.id; result = @{ thread = @{ id = 'thr_falso'; sessionId = 'thr_falso' }; model = 'gpt-falso' } }
            Mandar @{ method = 'thread/started'; params = @{ thread = @{ id = 'thr_falso' } } }
        }
        'thread/resume' { Mandar @{ id = $m.id; result = @{ thread = @{ id = $m.params.threadId } } } }
        'account/rateLimits/read' {
            Mandar @{ id = $m.id; result = @{ rateLimits = @{ primary = @{ usedPercent = 25; resetsAt = 1893456000 }; secondary = @{ usedPercent = 60 } } } }
        }
        'turn/interrupt' { Mandar @{ id = $m.id; result = @{} } }
        'model/list' { Mandar @{ id = $m.id; result = @{ data = @(@{ id = 'g'; model = 'gpt-falso'; displayName = 'GPT Falso'; description = 'teste'; hidden = $false; isDefault = $true; defaultReasoningEffort = 'medium'; supportedReasoningEfforts = @(@{ reasoningEffort = 'low'; description = '' }) }) } } }
        'thread/read' {
            $usuario = @{ type = 'userMessage'; id = 'u1'; content = @(@{ type = 'text'; text = 'oi de antes' }) }
            $resposta = @{ type = 'agentMessage'; id = 'a1'; text = 'resposta de antes' }
            $comando = @{ type = 'commandExecution'; id = 'c0'; command = 'ls'; cwd = '.'; status = 'completed'; commandActions = @(); aggregatedOutput = 'a.txt'; exitCode = 0 }
            Mandar @{ id = $m.id; result = @{ thread = @{ id = $m.params.threadId; turns = @(@{ id = 'turn_0'; status = 'completed'; items = @($usuario, $comando, $resposta) }) } } }
        }
        'thread/revert' {
            if ($m.params.beforeTurnId -eq 'turn_1') { Mandar @{ id = $m.id; result = @{ thread = @{ id = $m.params.threadId; turns = @() } } } }
            else { Mandar @{ id = $m.id; error = @{ code = -32600; message = "turno desconhecido: $($m.params.beforeTurnId)" } } }
        }
        'turn/start' {
            $texto = $m.params.input[0].text
            Mandar @{ id = $m.id; result = @{ turn = @{ id = 'turn_1'; status = 'inProgress'; items = @() } } }
            Mandar @{ method = 'turn/started'; params = @{ turn = @{ id = 'turn_1' } } }
            Mandar @{ method = 'item/started'; params = @{ threadId = 'thr_falso'; turnId = 'turn_1'; startedAtMs = 0; item = @{ type = 'agentMessage'; id = 'm1'; text = '' } } }
            Mandar @{ method = 'item/agentMessage/delta'; params = @{ threadId = 'thr_falso'; turnId = 'turn_1'; itemId = 'm1'; delta = 'Recebi: ' } }
            Mandar @{ method = 'item/completed'; params = @{ threadId = 'thr_falso'; turnId = 'turn_1'; completedAtMs = 0; item = @{ type = 'agentMessage'; id = 'm1'; text = "Recebi: $texto" } } }
            Mandar @{ method = 'item/started'; params = @{ threadId = 'thr_falso'; turnId = 'turn_1'; startedAtMs = 0; item = @{ type = 'commandExecution'; id = 'c1'; command = 'dotnet test'; cwd = '.'; status = 'inProgress'; commandActions = @() } } }
            Mandar @{ id = 900; method = 'item/commandExecution/requestApproval'; params = @{ threadId = 'thr_falso'; turnId = 'turn_1'; itemId = 'c1'; startedAtMs = 0; command = 'dotnet test'; reason = 'Rodar os testes' } }
            $resposta = Ler
            while ($resposta.id -ne 900) { $resposta = Ler }
            $decisao = $resposta.result.decision
            Mandar @{ method = 'item/completed'; params = @{ threadId = 'thr_falso'; turnId = 'turn_1'; completedAtMs = 0; item = @{ type = 'commandExecution'; id = 'c1'; command = 'dotnet test'; cwd = '.'; status = 'completed'; commandActions = @(); aggregatedOutput = "decisao=$decisao"; exitCode = 0 } } }
            Mandar @{ method = 'thread/tokenUsage/updated'; params = @{ threadId = 'thr_falso'; turnId = 'turn_1'; tokenUsage = @{ last = @{ inputTokens = 1234; totalTokens = 1500 }; total = @{ totalTokens = 1500 }; modelContextWindow = 272000 } } }
            Mandar @{ method = 'turn/completed'; params = @{ threadId = 'thr_falso'; turn = @{ id = 'turn_1'; status = 'completed'; items = @() } } }
        }
        default { if ($null -ne $m.id) { Mandar @{ id = $m.id; error = @{ code = -32601; message = "falso nao conhece $($m.method)" } } } }
    }
}
