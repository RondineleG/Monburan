# 🐳 Docker Setup - Monburan

Este documento fornece instruções detalhadas para configurar e executar o projeto Monburan usando Docker, tanto em desenvolvimento quanto em produção (VPS com nginx + TLS).

## 📋 Pré-requisitos

- Docker Desktop instalado ([Download](https://www.docker.com/products/docker-desktop))
- Git
- Mínimo 4GB de RAM disponível

## 🚀 Início Rápido

### 1. Configurar Variáveis de Ambiente

```bash
# Copiar arquivo de exemplo
cp .env.example .env

# Editar .env com suas configurações.
# OBRIGATÓRIO para subir a app completa: JWT_SIGNING_KEY
# Gere uma com: openssl rand -base64 48
```

### 2. Iniciar com Script (Recomendado)

#### Windows:
```powershell
# Desenvolvimento
.\docker-build.ps1 dev

# Produção
.\docker-build.ps1 prod

# Parar containers
.\docker-build.ps1 stop

# Ver logs
.\docker-build.ps1 logs
```

#### Linux/Mac:
```bash
# Dar permissão de execução (Linux/Mac)
chmod +x docker-build.sh

# Desenvolvimento
./docker-build.sh dev

# Produção
./docker-build.sh prod

# Parar containers
./docker-build.sh stop

# Ver logs
./docker-build.sh logs
```

### 3. Iniciar Manualmente

```bash
# Desenvolvimento
docker compose -f docker-compose.dev.yml up -d

# Produção local
docker compose up -d

# Apenas banco de dados (para rodar a app com dotnet run)
docker compose up -d postgres
```

## 🗄️ Serviços Docker

### PostgreSQL
- **Imagem**: `postgres:16-alpine`
- **Porta**: 5432
- **Credenciais padrão (dev)**: `postgres`/`postgres`, database `monburan`
- **Volume Persistente**: `postgres_data`
- **Health Check**: `pg_isready` (a app só sobe depois do banco ficar saudável)

### Aplicação Monburan
- **Build**: Multi-stage build (SDK + Runtime) — `src/Monburan.Web/Dockerfile`
- **Porta**: 8080 → 8080 (HTTP; a imagem roda sem root, como o usuário `app`)
- **Volumes**: `app_uploads` (`/app/wwwroot/uploads`), `app_keys` (`/app/keys`), `app_evidence` (`/app/App_Data/evidencias`) e, em produção, `app_logs` (`/app/logs`)
- **Volumes antigos (criados quando o app rodava como root)**: em produção o serviço `volume-permissions` acerta o dono sozinho; no compose local, se o app não conseguir gravar, rode `docker compose down -v` (apaga os dados locais) ou `docker run --rm -v <volume>:/d alpine chown -R 1654:1654 /d`
- **Health Check**: endpoint `/health`
- **Migrations**: aplicadas no boot quando `APPLY_MIGRATIONS=true`

## 🔧 Configuração

### Variáveis de Ambiente (.env)

```bash
# Banco PostgreSQL (Docker)
DB_NAME=monburan
DB_USER=postgres
DB_PASSWORD=postgres   # troque em produção!

# JWT — obrigatório, a app falha no boot sem chave de assinatura
# Gere com: openssl rand -base64 48
JWT_SIGNING_KEY=

# URL pública da app — usada em links de e-mail (confirmação/reset).
# Vazio = usa scheme/host da request (não funciona atrás de proxy TLS).
APP_PUBLIC_BASE_URL=https://monburan.com.br

# E-mail — opção 1: SMTP (usado em produção com Resend)
SMTP_HOST=smtp.resend.com
SMTP_PORT=465
SMTP_USER=resend
SMTP_PASSWORD=
SMTP_FROM_EMAIL=noreply@monburan.com.br
SMTP_FROM_NAME=Monburan

# E-mail — opção 2 (fallback quando Smtp:Host está vazio): SendGrid
SENDGRID_API_KEY=
```

### Connection String Docker

A connection string para Docker é configurada automaticamente no `docker-compose.yml`:

```
Host=postgres;Port=5432;Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASSWORD}
```

## 📱 Acessar a Aplicação

Após iniciar os containers:

- **Aplicação**: http://localhost:8080
- **Swagger** (só em Development): http://localhost:8080/swagger
- **Health Check**: http://localhost:8080/health

## 🛠️ Comandos Úteis

### Ver Status dos Containers
```bash
docker compose ps
docker compose -f docker-compose.dev.yml ps
```

### Ver Logs
```bash
# Todos os serviços
docker compose logs -f

# Apenas aplicação
docker compose logs -f app

# Apenas banco de dados
docker compose logs -f postgres
```

### Executar Migrations

A imagem final é runtime-only (sem SDK/`dotnet ef`). Opções:

```bash
# 1. Automático no boot (recomendado)
#    Defina APPLY_MIGRATIONS=true no environment da app
#    (já é o padrão no docker-compose.prod.yml)

# 2. A partir da máquina host (com .NET SDK instalado)
dotnet ef database update --project src/Monburan.Web \
  --connection "Host=localhost;Port=5432;Database=monburan;Username=postgres;Password=postgres"
```

### Acessar o PostgreSQL
```bash
# Entrar no container do Postgres
docker compose exec postgres bash

# Conectar ao banco
psql -U postgres -d monburan

# Ou direto, sem entrar no container:
docker compose exec postgres psql -U postgres -d monburan
```

### Reiniciar Serviços
```bash
docker compose restart app
docker compose restart postgres
```

### Limpar Tudo
```bash
# Parar e remover containers
docker compose down

# Remover volumes (cuidado: remove dados!)
docker compose down -v

# Limpar sistema Docker
docker system prune -a
```

## 🚀 Produção na VPS (GHCR + nginx + TLS)

A stack de produção fica em `infra/vps/` e serve `https://monburan.com.br`:

```
infra/vps/
├── docker-compose.prod.yml  # app (imagem GHCR) + postgres, app só em 127.0.0.1:8080
├── nginx.conf               # reverse proxy: 80→443, WebSocket p/ Blazor Server
├── provision.sh             # prepara um servidor novo: Docker, nginx, certbot, age, rclone
├── harden.sh                # firewall, SSH só por chave, fail2ban, atualizações automáticas
├── deploy.sh                # sobe/atualiza a stack e o nginx; na 1ª vez cria o .env e emite o TLS
├── validate.sh              # confere o servidor, só lendo (de fora e por dentro)
└── env.example              # template do .env de produção
```

### Fluxo de publicação

1. **CI/CD**: a cada push na `main` (e toda segunda, pelo `rebuild-semanal.yml`) o workflow
   `.github/workflows/docker-ghcr.yml` constrói e publica `ghcr.io/rondineleg/monburan:latest`
   (e a tag do commit); em seguida o job `deploy-vps` envia `infra/vps` para `/opt/monburan` e
   roda o `deploy.sh` na VPS. Só a `main` publica e faz deploy.
2. **Servidor novo** (como root): `bash provision.sh --apply`, `bash harden.sh --apply`, copiar
   `infra/vps` para `/opt/monburan` e rodar:
   ```bash
   cd /opt/monburan
   bash deploy.sh --email voce@exemplo.com --admin-email adm@exemplo.com
   ```

Na primeira vez o `deploy.sh` cria o `.env` com a senha do banco e a chave JWT geradas no
servidor, sobe a stack, emite o certificado (certbot) e instala o `nginx.conf`. Nas seguintes,
tira um backup, guarda a imagem atual para rollback, baixa as imagens novas e reinicia o app.
Passo a passo e rollback em [docs/CI-CD.md](docs/CI-CD.md) e [docs/SEGURANCA.md](docs/SEGURANCA.md).

### Arquitetura de produção

```
Internet → nginx (host, :443 TLS certbot)
            └── proxy → 127.0.0.1:8080 → container monburan-app (:8080, sem root)
                                             └── postgres (rede interna)
```

- O app **não** expõe porta pública — só `127.0.0.1:8080`.
- Uploads persistem no volume `app_uploads`, imagens das sugestões em `app_evidence`, logs em `app_logs`; banco no volume `postgres_data`.
- `APPLY_MIGRATIONS=true` aplica migrations no boot.
- E-mail sai via SMTP (Resend) usando as variáveis `Smtp__*`.

## 🐛 Solução de Problemas

### Porta 5432 já em uso
Se você já tiver o PostgreSQL instalado localmente, altere a porta no compose:

```yaml
ports:
  - "5433:5432"  # Mapear para porta 5433
```

### Erro de permissão no Linux/Mac
```bash
chmod +x docker-build.sh
```

### Containers não iniciam
```bash
# Ver logs para diagnóstico
docker compose logs

# Reconstruir imagens
docker compose build --no-cache

# Limpar e tentar novamente
docker compose down -v
docker compose up -d
```

### App falha no boot com erro de `Jwt:SigningKey`
A aplicação exige `Jwt__SigningKey` (`ValidateOnStart`). Defina `JWT_SIGNING_KEY`
no `.env` (gere com `openssl rand -base64 48`) e suba a stack novamente.

### Erro de conexão com banco de dados
1. Verifique se o Postgres está healthy:
   ```bash
   docker compose ps
   ```
2. Aguarde o health check completar (pode levar 15-30s)
3. Verifique as credenciais no arquivo `.env` (`DB_USER`/`DB_PASSWORD`/`DB_NAME`)

### Build falha
```bash
# Limpar cache do Docker
docker system prune -a

# Reconstruir sem cache
docker compose build --no-cache
```

### E-mail não está sendo enviado
- Com `Smtp__Host` configurado: verifique `SMTP_PASSWORD` (API key do Resend) e
  os logs do app — falhas de SMTP são logadas como `Error ao enviar email via SMTP`.
- Sem `Smtp__Host`: verifique `SENDGRID_API_KEY`.
- Sem nenhum dos dois: o envio é ignorado com `LogWarning` (dev é assim mesmo).

## 📊 Monitoramento

### Health Checks
```bash
# Ver health status
docker compose ps

# Testar health check manual
curl http://localhost:8080/health
```

### Resource Usage
```bash
# Ver uso de recursos
docker stats

# Ver uso de disco
docker system df
```

## 🔐 Segurança

### Senhas e Segredos
- **Nunca** use as senhas padrão em produção
- Altere `DB_PASSWORD` e defina `JWT_SIGNING_KEY` no `.env`
- O `.env` de produção deve ter `chmod 600` (o `infra/vps/deploy.sh` já aplica)
- Use senhas fortes (mínimo 12 caracteres com letras, números e símbolos)

### TLS
Em produção na VPS, o TLS é terminado no **nginx** do host com certificados do
Let's Encrypt (certbot) — veja `infra/vps/nginx.conf` e `deploy.sh`. O container
da app expõe apenas HTTP interno (`127.0.0.1:8080`).

## 📈 Performance

### Otimizações Implementadas
- Multi-stage build para reduzir tamanho da imagem
- `.dockerignore` para excluir arquivos desnecessários
- Health check no Postgres (app só sobe com o banco pronto)
- Volumes persistentes para dados do banco e uploads
- Cache de camadas do build no GHCR (`cache-from/to: type=gha`)

### Tamanho das Imagens
- **Build Stage**: ~1.5GB (SDK)
- **Runtime Stage**: ~200MB (ASP.NET Runtime)
- **Final**: ~250MB

## 🔄 Atualização

### Atualizar Imagens
```bash
docker compose pull
docker compose up -d
```

### Rebuild após mudanças
```bash
docker compose down
docker compose build
docker compose up -d
```

### Deploy de nova versão na VPS
```bash
# Na VPS: puxa a imagem nova do GHCR e reinicia
docker compose -f docker-compose.prod.yml pull app
docker compose -f docker-compose.prod.yml up -d
```

## 📝 Notas Importantes

1. **Hot Reload**: No modo desenvolvimento, o código é montado como volume para hot reload
2. **Persistência**: Dados do banco são persistidos em volumes Docker
3. **Network**: Containers usam network bridge isolada
4. **Performance**: Docker pode ter overhead de 2-5% em performance
5. **Windows**: No Windows, use WSL2 para melhor performance
6. **Compose v2**: use `docker compose` (plugin) — `docker-compose` v1 está descontinuado, mas os scripts `docker-build.sh`/`.ps1` ainda o invocam por compatibilidade

## 🆘 Suporte

Se encontrar problemas:

1. Verifique os logs: `docker compose logs`
2. Consulte este documento
3. Verifique o README principal
4. Abra uma issue no GitHub

## 📚 Recursos Adicionais

- [Docker Documentation](https://docs.docker.com/)
- [Docker Compose Documentation](https://docs.docker.com/compose/)
- [.NET in Docker](https://docs.microsoft.com/en-us/dotnet/core/docker/)
- [PostgreSQL in Docker](https://hub.docker.com/_/postgres)
- [GitHub Container Registry (GHCR)](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry)
