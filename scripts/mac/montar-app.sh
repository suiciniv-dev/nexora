#!/bin/bash
set -euo pipefail
publicado="$1"
destino="$2"
versao="${3:-0.0.0}"
app="$destino/Nexora.app"
rm -rf "$app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
cp -R "$publicado"/. "$app/Contents/MacOS/"
chmod +x "$app/Contents/MacOS/Nexora"
icones="$(mktemp -d)/nexora.iconset"
mkdir -p "$icones"
for t in 16 32 128 256 512; do
  sips -z $t $t "$publicado/Assets/nexora.png" --out "$icones/icon_${t}x${t}.png" >/dev/null
  d=$((t * 2))
  sips -z $d $d "$publicado/Assets/nexora.png" --out "$icones/icon_${t}x${t}@2x.png" >/dev/null
done
iconutil -c icns "$icones" -o "$app/Contents/Resources/nexora.icns"
cat > "$app/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>Nexora</string>
  <key>CFBundleDisplayName</key><string>Nexora</string>
  <key>CFBundleIdentifier</key><string>dev.suiciniv.nexora</string>
  <key>CFBundleVersion</key><string>$versao</string>
  <key>CFBundleShortVersionString</key><string>$versao</string>
  <key>CFBundleExecutable</key><string>Nexora</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleIconFile</key><string>nexora</string>
  <key>LSMinimumSystemVersion</key><string>12.0</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSPrincipalClass</key><string>NSApplication</string>
</dict>
</plist>
PLIST
echo "$app"
