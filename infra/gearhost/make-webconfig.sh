#!/usr/bin/env bash
# ============================================================
# Monburan — web.config do ambiente de homologação (GearHost/IIS)
#
# Gera <pasta>/web.config com as configurações do ambiente como variáveis de ambiente do ASP.NET Core Module. Usado pelo
# workflow deploy-dev.yml e pelo deploy local (deploy-dev.sh). O IIS não serve o web.config; os valores sensíveis ficam
# só nele, no servidor.
#
# Uso: DB_CONNECTION=... JWT_SIGNING_KEY=... SEED_PASSWORD=... [APP_URL=...] bash make-webconfig.sh <pasta-publicada>
# ============================================================
set -euo pipefail

out="${1:?Informe a pasta publicada}"
APP_URL="${APP_URL:-https://app-dev-monburan.gearhost.io}"

for name in DB_CONNECTION JWT_SIGNING_KEY SEED_PASSWORD; do
    [[ -n "${!name:-}" ]] || { echo "ERRO: falta $name" >&2; exit 1; }
done

xml() { printf '%s' "$1" | sed -e 's/&/\&amp;/g' -e 's/</\&lt;/g' -e 's/>/\&gt;/g' -e 's/"/\&quot;/g'; }
host="${APP_URL#https://}"
host="${host%%/*}"

cat > "$out/web.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet" arguments=".\Monburan.Web.dll" stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout" hostingModel="outofprocess">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Staging" />
          <environmentVariable name="APPLY_MIGRATIONS" value="true" />
          <environmentVariable name="ConnectionStrings__DefaultConnection" value="$(xml "$DB_CONNECTION")" />
          <environmentVariable name="Jwt__SigningKey" value="$(xml "$JWT_SIGNING_KEY")" />
          <environmentVariable name="App__PublicBaseUrl" value="$(xml "$APP_URL")" />
          <environmentVariable name="AllowedHosts" value="$(xml "$host")" />
          <environmentVariable name="DataProtection__KeysPath" value="App_Data\keys" />
          <environmentVariable name="Security__RequireTwoFactorForAdmins" value="false" />
          <environmentVariable name="Seed__DemoData" value="true" />
          <environmentVariable name="Seed__DemoPassword" value="$(xml "$SEED_PASSWORD")" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
EOF
