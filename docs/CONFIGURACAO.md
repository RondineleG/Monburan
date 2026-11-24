# Configuração

## Arquivos de configuração

- `appsettings.json`: configuração base, sem credenciais.
- `appsettings.Development.json`: configuração de desenvolvimento (PostgreSQL local e chave JWT de dev).
- `appsettings.Production.json.example`: modelo para produção.

## Segredos de produção

```bash
# Obrigatórias para produção
export ConnectionStrings__DefaultConnection="Host=...;Database=...;Username=...;Password=..."
export Jwt__SigningKey="$(openssl rand -base64 48)"   # obrigatório: a aplicação falha no boot sem ele
# Recomendadas: e-mail (SMTP; sem Smtp__Host cai no SendGrid)
export Smtp__Host="smtp.resend.com"
export Smtp__User="resend"
export Smtp__Password="sua-api-key-resend"
```

Em produção, a stack da VPS lê esses valores do `.env` (chmod 600), gerado a partir de `infra/vps/env.example`.

`App__PublicBaseUrl` (ex.: `https://monburan.com.br`) é opcional, mas recomendado: os links de confirmação de e-mail e de reset de senha usam essa base. Sem ela, a aplicação usa `scheme://host` da requisição, e atrás de proxy TLS sem `ForwardedHeaders` os links saem como `http://interno`.

## appsettings.json (trecho)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": ""  // use variáveis de ambiente
  },
  "SendGrid": {
    "ApiKey": "",  // fallback caso Smtp:Host não esteja configurado
    "FromEmail": "noreply@monburan.com.br",
    "FromName": "Monburan"
  },
  "Jwt": {
    "Issuer": "monburan.api",
    "Audience": "monburan.app",
    "AccessTokenExpiration": 3600,
    "RefreshTokenExpiration": 2592000,
    "SigningKey": ""  // obrigatório: configure via env ou User Secrets
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": { "Microsoft": "Warning", "System": "Warning" }
    },
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "File", "Args": { "path": "logs/monburan-.log", "rollingInterval": "Day" } }
    ]
  }
}
```

## E-mail

Quando `Smtp:Host` está configurado (variáveis `Smtp__Host`, `Smtp__User`, `Smtp__Password`, `Smtp__Port`, `Smtp__FromEmail`, `Smtp__FromName`), o envio usa SMTP. Em produção, usamos o **Resend** (`smtp.resend.com:465`, usuário `resend`, senha = API key). Sem `Smtp:Host`, o envio cai no SendGrid.

## Sugestões de IA (melhoria de currículo)

O administrador cadastra os provedores (OpenAI, OpenRouter, DeepSeek, Anthropic, Xiaomi ou qualquer serviço compatível) na tela **Provedores de IA** (`/admin/provedores-ia`), escolhe o padrão e informa o nome da variável de ambiente da chave (`AI_KEY_*`). A chave fica só no servidor:

- desenvolvimento: `dotnet user-secrets set "AI_KEY_OPENROUTER" "<chave>" --project src/Monburan.Web`;
- VPS: arquivo `ai-keys.env`.

Sem provedor padrão e chave, o recurso fica desligado. Guia da tela, segurança e erros em [IA-PROVEDORES.md](IA-PROVEDORES.md); privacidade e limites em [IA-CURRICULO.md](IA-CURRICULO.md).

## Sugestões de vaga (evidências)

| Chave | Padrão | Para quê |
|---|---|---|
| `Evidence:StoragePath` | `App_Data/evidencias` (dentro da pasta do app) | Pasta privada das imagens enviadas em `/sugerir-vaga`. Em produção é o volume `app_evidence` (`/app/App_Data/evidencias`), incluído no backup. |
| `Evidence:RetentionDays` | `90` | Dias, depois da moderação, até apagar as imagens de sugestões rejeitadas ou duplicadas. `0` desliga a limpeza. A passada diária é incremental (só confere o intervalo que venceu desde a anterior); a marca fica em memória, então o primeiro ciclo depois de um reinício confere tudo uma vez. |
| `Submissions:HashKey` (`Submissions__HashKey`; no Compose, `SUBMITTER_HASH_KEY`) | vazio | Chave secreta do HMAC-SHA256 que pseudonimiza o IP de quem sugere. Gere com `openssl rand -base64 48`. Vazia, o app usa uma chave aleatória por processo (seguro, mas o hash muda a cada reinício). |

O limite de envios é de 5 por hora por IPv4 ou por bloco IPv6 `/64`, em memória (reiniciar zera o contador). O formulário tem um campo-isca contra robôs. As imagens da moderação saem por `GET /admin/sugestoes/evidencias/{id}`, restrito à política `AdminOnly`, e não pelo circuito do Blazor.

## Cobrança dos planos

| Chave | Padrão | Para quê |
|---|---|---|
| `Billing:MercadoPago:AccessToken` | vazio | Access token privado do Mercado Pago. Configure em User Secrets ou como `Billing__MercadoPago__AccessToken`; nunca no repositório. |
| `Billing:MercadoPago:WebhookSecret` | vazio | Chave secreta de assinatura do webhook no Mercado Pago, configurada como `Billing__MercadoPago__WebhookSecret`. |
| `Billing:MercadoPago:PublicBaseUrl` | `App:PublicBaseUrl` | URL pública HTTPS do portal. O webhook fica em `/api/v1/webhooks/mercadopago`; pode usar `Billing__MercadoPago__PublicBaseUrl` para sobrescrever. |

Com as três configurações corretas, `/plans` abre uma assinatura recorrente mensal ou anual. O acesso só muda após webhook assinado e confirmação server-to-server de status, valor e moeda. Sem essas chaves, a página mantém o contato manual e o servidor não ativa planos pagos pelo endpoint de upgrade. Cancelar em `/minha-assinatura` também cancela a recorrência no provedor.

## User Secrets (desenvolvimento)

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=monburan_dev;Username=postgres;Password=postgres"
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)"
dotnet user-secrets set "Smtp:Password" "sua-api-key-resend"
dotnet user-secrets set "Billing:MercadoPago:AccessToken" "seu-access-token-privado"
dotnet user-secrets set "Billing:MercadoPago:WebhookSecret" "sua-chave-secreta-de-webhook"
```

> **Nunca commite segredos no repositório.** Use User Secrets em desenvolvimento e variáveis de ambiente em produção.
