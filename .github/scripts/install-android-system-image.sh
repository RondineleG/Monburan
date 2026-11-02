#!/usr/bin/env bash
set -euo pipefail

: "${ANDROID_HOME:?ANDROID_HOME deve apontar para o SDK Android}"
package='system-images;android-34;google_apis;x86_64'
sdkmanager="$ANDROID_HOME/cmdline-tools/latest/bin/sdkmanager"
if [[ ! -x "$sdkmanager" ]]; then
  sdkmanager="$(command -v sdkmanager || true)"
fi
if [[ -z "$sdkmanager" ]]; then
  echo "::error::sdkmanager não encontrado em ANDROID_HOME/cmdline-tools/latest/bin nem no PATH"
  exit 1
fi

# O runner do emulador instala sem retry. Antecipar o download permite recuperar
# arquivos incompletos antes de criar o AVD, inclusive quando há snapshot em cache.
for attempt in 1 2 3; do
  echo "Instalando $package (tentativa $attempt/3)"
  if timeout 12m "$sdkmanager" --install "$package" --channel=0; then
    exit 0
  fi

  if [[ "$attempt" == 3 ]]; then
    echo "::error::Não foi possível instalar a imagem Android após 3 tentativas"
    exit 1
  fi

  # O sdkmanager mantém o ZIP parcial em .temp e pode reutilizá-lo no retry.
  # Limitar a limpeza ao diretório temporário do SDK neste runner Linux.
  if [[ "$ANDROID_HOME" != /* || "$ANDROID_HOME" == / ]]; then
    echo "::error::Caminho do SDK inválido para limpeza: $ANDROID_HOME"
    exit 1
  fi
  rm -rf -- "$ANDROID_HOME/.temp"
  sleep 10
done
