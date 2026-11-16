namespace Nexora.Core.Nuvem;

public static class ScriptRemoto
{
    public const int Versao = 3;
    public const string Caminho = "$HOME/.nexora/bin/nexora-remoto";

    public static string Chamar(string acao, params string[] argumentos) =>
        "sh \"" + Caminho + "\" " + string.Join(' ', [Shell.Aspas(acao), .. argumentos.Select(Shell.Aspas)]);

    public static string Instalar =>
        "mkdir -p \"$HOME/.nexora/bin\" && chmod 700 \"$HOME/.nexora\" && cat > \"" + Caminho + ".novo\" && mv \"" + Caminho + ".novo\" \"" + Caminho + "\" && echo instalado";

    public static string Conferir => "grep -q 'nexora-remoto versao=" + Versao + "$' \"" + Caminho + "\" 2>/dev/null && echo pronto || echo falta";

    public static string Conteudo { get; } = """
        #!/bin/sh
        # nexora-remoto versao=3
        BASE="$HOME/.nexora"
        PATH="$HOME/.local/bin:$HOME/.npm-global/bin:$HOME/.claude/local:$HOME/.bun/bin:$HOME/bin:/usr/local/bin:/opt/homebrew/bin:$PATH"
        export PATH
        acao="$1"
        [ $# -gt 0 ] && shift
        pasta_da() { printf '%s/execucoes/%s' "$BASE" "$1"; }
        valido() { case "$1" in ''|*[!A-Za-z0-9._-]*) echo "nome invalido: $1" >&2; exit 2;; esac; }
        versao_de() { if command -v "$1" >/dev/null 2>&1; then "$1" --version 2>/dev/null | head -n 1; fi; }
        rodando() {
          d=$(pasta_da "$1")
          [ -f "$d/pid" ] || return 1
          [ -f "$d/fim" ] && return 1
          kill -0 "$(cat "$d/pid")" 2>/dev/null
        }
        case "$acao" in
          sobre)
            echo "sistema=$(uname -s 2>/dev/null) $(uname -r 2>/dev/null) $(uname -m 2>/dev/null)"
            echo "home=$HOME"
            echo "git=$(versao_de git)"
            echo "node=$(versao_de node)"
            echo "claude=$(versao_de claude)"
            echo "codex=$(versao_de codex)"
            echo "tmux=$(versao_de tmux)"
            ;;
          iniciar)
            id="$1"; pasta="$2"; valido "$id"
            d=$(pasta_da "$id")
            mkdir -p "$d" && chmod 700 "$d"
            if rodando "$id"; then echo "ja-rodando"; exit 0; fi
            rm -f "$d/fim" "$d/pid" "$d/tail.pid" "$d/agente.pid"
            : > "$d/entrada"; : > "$d/saida"; : > "$d/erros"
            while IFS= read -r linha; do
              [ -z "$linha" ] && break
              nome=${linha%%=*}
              case "$nome" in ''|[0-9]*|*[!A-Za-z0-9_]*) continue;; esac
              export "$linha"
            done
            cat > "$d/comando"
            printf '%s\n' "$pasta" > "$d/pasta"
            (
              cd "$pasta" 2>>"$d/erros" || { echo 97 > "$d/fim"; exit 0; }
              ( tail -n +1 -f "$d/entrada" & echo $! > "$d/tail.pid"; wait ) |
              {
                exec 3<&0
                if command -v setsid >/dev/null 2>&1; then
                  setsid sh "$d/comando" 0<&3 3<&- >> "$d/saida" 2>> "$d/erros" &
                else
                  sh "$d/comando" 0<&3 3<&- >> "$d/saida" 2>> "$d/erros" &
                fi
                exec 3<&-
                echo $! > "$d/agente.pid"
                wait $!
                codigo=$?
                i=0
                while [ ! -s "$d/tail.pid" ] && [ $i -lt 20 ]; do sleep 0.1; i=$((i+1)); done
                kill "$(cat "$d/tail.pid" 2>/dev/null)" 2>/dev/null
                echo "$codigo" > "$d/fim.novo" && mv "$d/fim.novo" "$d/fim"
              }
            ) < /dev/null > /dev/null 2>&1 &
            echo $! > "$d/pid"
            echo "iniciado"
            ;;
          estado)
            id="$1"; valido "$id"
            d=$(pasta_da "$id")
            tamanho=0
            [ -f "$d/saida" ] && tamanho=$(wc -c < "$d/saida" | tr -d ' ')
            if [ -f "$d/fim" ]; then echo "fim $(cat "$d/fim") $tamanho"
            elif rodando "$id"; then echo "rodando 0 $tamanho"
            elif [ -d "$d" ]; then echo "perdido 0 $tamanho"
            else echo "nenhum 0 0"; fi
            ;;
          ler)
            id="$1"; desde="$2"; valido "$id"
            d=$(pasta_da "$id")
            [ -f "$d/saida" ] || { echo "sem-execucao" >&2; exit 3; }
            echo "nexora-lendo" >&2
            tail -c +$((desde + 1)) -f "$d/saida" &
            t=$!
            trap 'kill $t 2>/dev/null; exit 0' HUP INT TERM PIPE
            while rodando "$id"; do
              sleep 1
              echo "nexora-vivo" >&2 || { kill $t 2>/dev/null; exit 0; }
            done
            sleep 1
            kill $t 2>/dev/null
            wait $t 2>/dev/null
            echo "nexora-fim" >&2
            ;;
          escrever)
            id="$1"; bytes="$2"; valido "$id"
            d=$(pasta_da "$id")
            [ -f "$d/entrada" ] || { echo "sem-execucao" >&2; exit 3; }
            tmp="$d/entrada.$$"
            head -c "$bytes" > "$tmp"
            if [ "$(wc -c < "$tmp" | tr -d ' ')" = "$bytes" ]; then cat "$tmp" >> "$d/entrada"; rm -f "$tmp"; echo "ok"
            else rm -f "$tmp"; echo "incompleto" >&2; exit 4; fi
            ;;
          erros)
            id="$1"; valido "$id"
            tail -c 8000 "$(pasta_da "$id")/erros" 2>/dev/null
            ;;
          inicio-do-turno)
            id="$1"; valido "$id"
            d=$(pasta_da "$id")
            ultima=$(grep -b '^{"type":"result"' "$d/saida" 2>/dev/null | tail -n 1 | cut -d: -f1)
            if [ -z "$ultima" ]; then echo 0; else
              resto=$(tail -c +$((ultima + 1)) "$d/saida" | head -n 1 | wc -c | tr -d ' ')
              echo $((ultima + resto))
            fi
            ;;
          parar)
            id="$1"; valido "$id"
            d=$(pasta_da "$id")
            if rodando "$id"; then
              kill "$(cat "$d/tail.pid" 2>/dev/null)" 2>/dev/null
              i=0
              while rodando "$id" && [ $i -lt 30 ]; do sleep 0.1; i=$((i+1)); done
              if rodando "$id"; then
                p=$(cat "$d/agente.pid" 2>/dev/null)
                kill -TERM "-$p" 2>/dev/null || kill "$p" 2>/dev/null
                i=0
                while rodando "$id" && [ $i -lt 30 ]; do sleep 0.1; i=$((i+1)); done
                kill -KILL "-$p" 2>/dev/null || kill -9 "$p" 2>/dev/null
              fi
            fi
            echo "parado"
            ;;
          esquecer)
            id="$1"; valido "$id"
            rm -rf "$(pasta_da "$id")"
            ;;
          *)
            echo "acao desconhecida: $acao" >&2; exit 2
            ;;
        esac
        """.Replace("\r\n", "\n") + "\n";
}

public static class Shell
{
    public static string Aspas(string valor) =>
        valor.Length > 0 && valor.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or '/' or '=' or ':' or ',' or '@' or '+')
            ? valor
            : "'" + valor.Replace("'", "'\\''") + "'";

    public static string Comando(string programa, IEnumerable<string> argumentos) =>
        string.Join(' ', [Aspas(programa), .. argumentos.Select(Aspas)]);

    public static string Caminho(string caminho) =>
        caminho.StartsWith("~/", StringComparison.Ordinal) ? "\"$HOME\"/" + Aspas(caminho[2..]) : Aspas(caminho);
}
