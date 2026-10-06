# RoggoR NuGet Packages (Local)

Este diretório contém os pacotes NuGet locais do RoggoR gerados para testes e desenvolvimento.

## Pacotes Disponíveis

- **RoggoR.Client.1.0.0.nupkg** (16.3 KB) - Client SDK principal com HTTP transport
- **RoggoR.RabbitMq.1.0.0.nupkg** (9.7 KB) - RabbitMQ transport
- **RoggoR.AwsSqs.1.0.0.nupkg** (8.9 KB) - AWS SQS transport  
- **RoggoR.AzureServiceBus.1.0.0.nupkg** (8.6 KB) - Azure Service Bus transport
- **RoggoR.PostgreSQL.1.0.0.nupkg** (13.8 KB) - PostgreSQL data store

## Como Usar os Pacotes Locais

### 1. Adicionar Fonte Local do NuGet

```bash
dotnet nuget add source ./nuget-packages --name LocalRoggoR
```

### 2. Instalar os Pacotes

```bash
# Cliente principal
dotnet add package RoggoR.Client --source LocalRoggoR

# Transportes opcionais
dotnet add package RoggoR.RabbitMq --source LocalRoggoR
dotnet add package RoggoR.AwsSqs --source LocalRoggoR
dotnet add package RoggoR.AzureServiceBus --source LocalRoggoR

# Banco de dados opcional
dotnet add package RoggoR.PostgreSQL --source LocalRoggoR
```

### 3. Usar no Projeto

```csharp
using Microsoft.Extensions.Logging;
using RoggoR.Client.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Adicionar RoggoR Client
builder.Logging.AddRoggoR(builder.Configuration);

var app = builder.Build();
```

### 4. Configurar appsettings.json

```json
{
  "RoggoR": {
    "ServerUrl": "http://localhost:5200",
    "ApiKey": "your-api-key",
    "ServiceName": "MyApplication",
    "Environment": "Development"
  }
}
```

## Exemplo Completo

### Criar um Projeto de Teste

```bash
# Criar projeto de console
dotnet new console -o TestRoggoR
cd TestRoggoR

# Adicionar fonte local
dotnet nuget add source ../nuget-packages --name LocalRoggoR

# Instalar RoggoR Client
dotnet add package RoggoR.Client --source LocalRoggoR

# Adicionar pacotes necessários
dotnet add package Microsoft.Extensions.Hosting
dotnet add package Microsoft.Extensions.Configuration.Json
dotnet add package Microsoft.Extensions.Logging.Console
```

### Program.cs

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RoggoR.Client.Extensions;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .Build();

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddLogging(logging =>
        {
            logging.AddConfiguration(configuration.GetSection("Logging"));
            logging.AddConsole();
            logging.AddRoggoR(configuration);
        });
    })
    .Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Hello from RoggoR!");

await host.RunAsync();
```

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "RoggoR": {
    "ServerUrl": "http://localhost:5200",
    "ApiKey": "your-api-key",
    "ServiceName": "TestRoggoR",
    "Environment": "Development",
    "Enabled": true
  }
}
```

### Executar

```bash
dotnet run
```

## Remover Fonte Local

Quando não precisar mais dos pacotes locais:

```bash
dotnet nuget remove source LocalRoggoR
```

## Testar Pacotes Individualmente

### Testar RoggoR.Client

```bash
cd examples/ConsoleApp
dotnet remove package RoggoR.Client
dotnet add package RoggoR.Client --source ../nuget-packages
dotnet run
```

### Testar Transportes

```bash
# Testar RabbitMQ
dotnet add package RoggoR.RabbitMq --source ../nuget-packages

# Testar AWS SQS
dotnet add package RoggoR.AwsSqs --source ../nuget-packages

# Testar Azure Service Bus
dotnet add package RoggoR.AzureServiceBus --source ../nuget-packages
```

## Publicar no NuGet.org

Quando estiver pronto para publicar no NuGet oficial:

```bash
# Publicar RoggoR.Client
dotnet nuget push nuget-packages/RoggoR.Client.1.0.0.nupkg --source nuget.org --api-key YOUR_API_KEY

# Publicar transportes
dotnet nuget push nuget-packages/RoggoR.RabbitMq.1.0.0.nupkg --source nuget.org --api-key YOUR_API_KEY
dotnet nuget push nuget-packages/RoggoR.AwsSqs.1.0.0.nupkg --source nuget.org --api-key YOUR_API_KEY
dotnet nuget push nuget-packages/RoggoR.AzureServiceBus.1.0.0.nupkg --source nuget.org --api-key YOUR_API_KEY
dotnet nuget push nuget-packages/RoggoR.PostgreSQL.1.0.0.nupkg --source nuget.org --api-key YOUR_API_KEY
```

## Informações dos Pacotes

### RoggoR.Client (v1.0.0)
- **Descrição**: Client SDK principal com HTTP transport
- **Dependências**: RoggoR.Core, Microsoft.Extensions.Http, Microsoft.Extensions.Logging.Abstractions
- **Uso**: Integração básica com RoggoR via ILogger

### RoggoR.RabbitMq (v1.0.0)
- **Descrição**: RabbitMQ transport para mensageria assíncrona
- **Dependências**: RoggoR.Core, RabbitMQ.Client
- **Uso**: Para sistemas que usam RabbitMQ

### RoggoR.AwsSqs (v1.0.0)
- **Descrição**: AWS SQS transport para mensageria na AWS
- **Dependências**: RoggoR.Core, AWSSDK.SQS
- **Uso**: Para sistemas hospedados na AWS

### RoggoR.AzureServiceBus (v1.0.0)
- **Descrição**: Azure Service Bus transport para mensageria no Azure
- **Dependências**: RoggoR.Core, Azure.Messaging.ServiceBus
- **Uso**: Para sistemas hospedados no Azure

### RoggoR.PostgreSQL (v1.0.0)
- **Descrição**: PostgreSQL data store para o servidor RoggoR
- **Dependências**: RoggoR.Core, RoggoR.Server, Npgsql.EntityFrameworkCore.PostgreSQL
- **Uso**: Para usar PostgreSQL como banco de dados no servidor

## Suporte

Para problemas com os pacotes locais:
- Verifique se a fonte NuGet foi adicionada corretamente
- Confirme que o caminho está correto
- Verifique se há conflitos de versão
- Consulte a documentação principal em [docs/NUGET_PUBLISH.md](../docs/NUGET_PUBLISH.md)