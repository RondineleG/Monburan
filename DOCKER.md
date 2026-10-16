# 🐳 Docker Setup - Monburan

Este documento fornece instruções detalhadas para configurar e executar o projeto Monburan usando Docker.

## 📋 Pré-requisitos

- Docker Desktop instalado ([Download](https://www.docker.com/products/docker-desktop))
- Git
- Mínimo 4GB de RAM disponível

## 🚀 Início Rápido

### 1. Configurar Variáveis de Ambiente

```bash
# Copiar arquivo de exemplo
cp .env.example .env

# Editar .env com suas configurações (opcional para desenvolvimento)
# A senha padrão já está configurada
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
docker-compose -f docker-compose.dev.yml up -d

# Produção
docker-compose up -d

# Apenas banco de dados
docker-compose up -d sqlserver
```

## 🗄️ Serviços Docker

### SQL Server
- **Imagem**: mcr.microsoft.com/mssql/server:2022-latest
- **Porta**: 1433
- **Senha Padrão**: YourStrong@Passw0rd
- **Volume Persistente**: sqlserver_data

### Aplicação Monburan
- **Build**: Multi-stage build (SDK + Runtime)
- **Porta**: 8080 (HTTP)
- **Volume Uploads**: app_uploads
- **Health Check**: Configurado para `/`

## 🔧 Configuração

### Variáveis de Ambiente (.env)

```bash
# Senha do SQL Server (obrigatório para Docker)
DB_PASSWORD=YourStrong@Passw0rd

# API Key do SendGrid (opcional)
SENDGRID_API_KEY=
```

### Connection String Docker

A connection string para Docker é automaticamente configurada no docker-compose.yml:

```
Server=sqlserver,1433;Database=MonburanDB;User Id=sa;Password=${DB_PASSWORD};TrustServerCertificate=True;MultipleActiveResultSets=true
```

## 📱 Acessar a Aplicação

Após iniciar os containers:

- **Aplicação**: http://localhost:8080
- **Swagger**: http://localhost:8080/swagger
- **Health Check**: http://localhost:8080/health

## 🛠️ Comandos Úteis

### Ver Status dos Containers
```bash
docker-compose ps
docker-compose -f docker-compose.dev.yml ps
```

### Ver Logs
```bash
# Todos os serviços
docker-compose logs -f

# Apenas aplicação
docker-compose logs -f app

# Apenas banco de dados
docker-compose logs -f sqlserver
```

### Executar Migrations
```bash
# Entrar no container da aplicação
docker-compose exec app bash

# Executar migrations
dotnet ef database update

# Sair do container
exit
```

### Acessar SQL Server
```bash
# Entrar no container do SQL Server
docker-compose exec sqlserver bash

# Conectar ao SQL Server
/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd'
```

### Reiniciar Serviços
```bash
docker-compose restart app
docker-compose restart sqlserver
```

### Limpar Tudo
```bash
# Parar e remover containers
docker-compose down

# Remover volumes (cuidado: remove dados!)
docker-compose down -v

# Limpar sistema Docker
docker system prune -a
```

## 🐛 Solução de Problemas

### Porta 1433 já em uso
Se você já tiver o SQL Server instalado localmente, altere a porta no docker-compose.yml:

```yaml
ports:
  - "1434:1433"  # Mapear para porta 1434
```

### Erro de permissão no Linux/Mac
```bash
# Dar permissão ao script
chmod +x docker-build.sh
```

### Containers não iniciam
```bash
# Ver logs para diagnóstico
docker-compose logs

# Reconstruir imagens
docker-compose build --no-cache

# Limpar e tentar novamente
docker-compose down -v
docker-compose up -d
```

### Erro de conexão com banco de dados
1. Verifique se o SQL Server está healthy:
   ```bash
   docker-compose ps
   ```
2. Aguarde o health check completar (pode levar 30-40s)
3. Verifique a senha no arquivo .env

### Build falha
```bash
# Limpar cache do Docker
docker system prune -a

# Reconstruir sem cache
docker-compose build --no-cache
```

## 📊 Monitoramento

### Health Checks
```bash
# Ver health status
docker-compose ps

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

### Senhas
- **Nunca** use a senha padrão em produção
- Altere `DB_PASSWORD` no arquivo .env
- Use senhas fortes (mínimo 8 caracteres, maiúsculas, minúsculas, números, caracteres especiais)

### Certificados SSL
Para produção, configure certificados SSL:
```bash
# Adicionar ao docker-compose.yml
ports:
  - "8443:443"
volumes:
  - ./certs:/app/certs:ro
```

## 📈 Performance

### Otimizações Implementadas
- Multi-stage build para reduzir tamanho da imagem
- .dockerignore para excluir arquivos desnecessários
- Health checks para auto-recuperação
- Volume persistente para dados do banco

### Tamanho das Imagens
- **Build Stage**: ~1.5GB (SDK)
- **Runtime Stage**: ~200MB (ASP.NET Runtime)
- **Final**: ~250MB

## 🔄 Atualização

### Atualizar Imagens
```bash
docker-compose pull
docker-compose up -d
```

### Rebuild após mudanças
```bash
docker-compose down
docker-compose build
docker-compose up -d
```

## 📝 Notas Importantes

1. **Hot Reload**: No modo desenvolvimento, o código é montado como volume para hot reload
2. **Persistência**: Dados do banco são persistidos em volumes Docker
3. **Network**: Containers usam network bridge isolada
4. **Performance**: Docker pode ter overhead de 2-5% em performance
5. **Windows**: No Windows, use WSL2 para melhor performance

## 🆘 Suporte

Se encontrar problemas:

1. Verifique os logs: `docker-compose logs`
2. Consulte este documento
3. Verifique o README principal
4. Abra uma issue no GitHub

## 📚 Recursos Adicionais

- [Docker Documentation](https://docs.docker.com/)
- [Docker Compose Documentation](https://docs.docker.com/compose/)
- [.NET in Docker](https://docs.microsoft.com/en-us/dotnet/core/docker/)
- [SQL Server in Docker](https://docs.microsoft.com/en-us/sql/linux/sql-server-linux-docker-container-deployment)
