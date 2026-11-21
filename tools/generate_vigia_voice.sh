#!/bin/zsh
# Genera la voz de VIGÍA: un WAV (44.1 kHz, mono, 16 bits) por variante de cada frase del catálogo.
# Voz de España: prefiere una variante premium o mejorada si está instalada (Ajustes > Accesibilidad > Contenido hablado > Voces del sistema)
# y si no, "Mónica" (es_ES). Ritmo ~175 palabras por minuto, con pausas tras cada frase, en los puntos suspensivos y breves en las comas.
# Las pausas solo van en lo hablado: los subtítulos (vigia_frases.txt) no cambian.
# Solo hace falta para regenerar: la build usa los WAV del repo.
# Uso: ./tools/generate_vigia_voice.sh   (desde la raíz del repo)
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CAT="$ROOT/game/Assets/_Project/Audio/Vigia/Resources/vigia_frases.txt"
OUT="$ROOT/game/Assets/_Project/Audio/Vigia/Resources"
RATE=175
VOICES="$(say -v '?')"
VOICE=""
for cand in "Marisol (Premium)" "Mónica (Premium)" "Mónica (Mejorada)" "Mónica (Enhanced)" "Jorge (Mejorada)" "Jorge (Enhanced)" "Marisol (Mejorada)" "Mónica"; do
  if print -r -- "$VOICES" | grep -q "^$cand "; then VOICE="$cand"; break; fi
done
[[ -z "$VOICE" ]] && VOICE="Paulina"
echo "Voz: $VOICE ($RATE ppm)"
TMP="$(mktemp -d)"
n=0
while IFS=$'\t' read -r id cat prio variants; do
  [[ -z "$id" || "$id" == \#* ]] && continue
  i=1
  for v in "${(@s: | :)variants}"; do
    # pausas humanas: puntos suspensivos largos, fin de frase medio, comas breves (comandos [[slnc ms]] de say)
    spoken="${v//.../ [[slnc 380]] }"
    spoken="${spoken//. /. [[slnc 260]] }"
    spoken="${spoken//\? /? [[slnc 260]] }"
    spoken="${spoken//! /! [[slnc 240]] }"
    spoken="${spoken//, /, [[slnc 120]] }"
    say -v "$VOICE" -r $RATE -o "$TMP/v.aiff" -- "$spoken"
    afconvert -f WAVE -d LEI16@44100 -c 1 "$TMP/v.aiff" "$OUT/vigia_${id}_${i}.wav"
    i=$((i+1)); n=$((n+1))
  done
done < "$CAT"
rm -rf "$TMP"
echo "$n clips en $OUT"
