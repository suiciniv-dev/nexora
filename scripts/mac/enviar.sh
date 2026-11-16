#!/bin/bash
set -euo pipefail
raiz="$(cd "$(dirname "$0")/../.." && pwd)"
rid="${1:-osx-x64}"
versao="$(grep -o '<Version>[^<]*' "$raiz/src/Nexora/Nexora.csproj" | cut -d'>' -f2)"
saida="$(mktemp -d)"
dotnet publish "$raiz/src/Nexora" -c Release -r "$rid" --self-contained -p:UseAppHost=true -o "$saida" -nologo -v q >/dev/null
find "$saida" -name '*.pdb' -delete
ssh mac 'rm -rf ~/nexora-pub && mkdir -p ~/nexora-pub ~/nexora-dev'
tar -C "$saida" -cf - . | ssh mac 'tar -C ~/nexora-pub -xf -'
ssh mac 'cat > ~/nexora-dev/montar-app.sh && chmod +x ~/nexora-dev/montar-app.sh' < "$raiz/scripts/mac/montar-app.sh"
ssh mac "~/nexora-dev/montar-app.sh ~/nexora-pub ~/nexora-dev $versao"
rm -rf "$saida"
