#!/bin/zsh
# Genera la voz de VIGÍA: un WAV (44.1 kHz, mono, 16 bits) por variante de cada frase del catálogo.
# Usa la voz del sistema "Paulina" (es_MX) de macOS; si no existe, "Mónica" (es_ES). Solo hace falta para regenerar: la build usa los WAV del repo.
# Uso: ./tools/generate_vigia_voice.sh   (desde la raíz del repo)
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CAT="$ROOT/game/Assets/_Project/Audio/Vigia/Resources/vigia_frases.txt"
OUT="$ROOT/game/Assets/_Project/Audio/Vigia/Resources"
VOICE="Paulina"
say -v '?' | grep -q "^Paulina" || VOICE="Mónica"
echo "Voz: $VOICE"
TMP="$(mktemp -d)"
n=0
while IFS=$'\t' read -r id cat prio variants; do
  [[ -z "$id" || "$id" == \#* ]] && continue
  i=1
  for v in "${(@s: | :)variants}"; do
    say -v "$VOICE" -r 185 -o "$TMP/v.aiff" -- "$v"
    afconvert -f WAVE -d LEI16@44100 -c 1 "$TMP/v.aiff" "$OUT/vigia_${id}_${i}.wav"
    i=$((i+1)); n=$((n+1))
  done
done < "$CAT"
rm -rf "$TMP"
echo "$n clips en $OUT"
