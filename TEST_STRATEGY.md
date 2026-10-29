# Estratégia de Testes - Monburan

## Visão Geral

Este documento descreve a estratégia de testes implementada para o projeto Monburan, uma plataforma de recrutamento desenvolvida em .NET 10 com Blazor (web) e .NET MAUI Blazor Hybrid (mobile).

## Estrutura de Testes

O projeto está organizado em quatro camadas de testes (números de 2026-10-29 na seção "Métricas Atuais"):

### 1. Testes Unitários (Monburan.UnitTests)

**Objetivo:** Testar componentes isolados sem dependências externas.

**Localização:** `tests/Monburan.UnitTests/`

**Framework:** xUnit + NSubstitute + FluentAssertions

**Cobertura:**
- **Use Cases:** Testes para lógica de negócio dos casos de uso
  - GetJobByIdUseCaseTests
  - CreateJobUseCaseTests
  - ListJobsUseCaseTests
- **Validations:** Testes para validadores de domínio
  - CpfValidatorTests
  - CnpjValidatorTests
  - JobValidatorTests
- **Value Objects:** Testes para objetos de valor
  - IdTests

**Total:** 1276 testes

### 2. Testes de Integração (Monburan.IntegrationTests)

**Objetivo:** Testar a integração entre componentes e com banco de dados.

**Localização:** `tests/Monburan.IntegrationTests/`

**Framework:** xUnit + Entity Framework Core InMemory

**Cobertura:**
- **Repositories:** Testes para repositórios de dados
  - JobRepositoryTests

**Total:** 57 métodos de teste

**Observações:**
- Utiliza banco de dados em memória para isolamento
- Testa operações CRUD completas

### 3. Testes de API (Monburan.ApiTests)

**Objetivo:** Testar os endpoints REST `/api/auth/*` e `/api/v1/*` de ponta a ponta, subindo a aplicação real em memória.

**Localização:** `tests/Monburan.ApiTests/`

**Framework:** xUnit + `Microsoft.AspNetCore.Mvc.Testing` (WebApplicationFactory) + EF Core InMemory

**Infraestrutura:**
- `MonburanApiFactory` substitui o PostgreSQL por EF InMemory e roda em
  `Development` para manter o seed de dados (vagas, planos, usuários)
- Cada factory usa um banco InMemory exclusivo (seed não é idempotente)

**Cobertura:**
- `AuthApiTests` — login, refresh token, 401 sem token
- `JobsApiTests` — listagem pública paginada, detalhe, CRUD de anunciante
- `ApplicationsApiTests` — candidatura end-to-end
- `FilesApiTests` — upload multipart (válido/inválido)
- `SubscriptionsApiTests` — planos e assinaturas

**Total:** 256 testes

### 4. Testes E2E (Monburan.E2ETests)

**Objetivo:** Testar o fluxo completo do usuário através da interface.

**Localização:** `tests/Monburan.E2ETests/`

**Framework:** xUnit + Playwright

**Cobertura:**
- `PublicPagesTests` — páginas públicas (home, listagem, detalhe de vaga)
- `AuthenticationTests` — registro, login, logout
- `CandidateFlowTests` — fluxo do candidato (busca, candidatura, currículos)
- `AdvertiserFlowTests` — fluxo do anunciante (criar/gerir vagas, candidaturas)
- `ShellInteractionTests` — navegação e shell do app
- `FullSweepTests` — varredura de rotas principais
- `E2ETestBase` — infraestrutura compartilhada (browser, base URL)

**Total:** 99 métodos de teste

## Ferramentas Utilizadas

### Framework de Testes
- **xUnit:** Framework de testes unitários para .NET
- **NSubstitute:** Biblioteca para mocking de objetos
- **FluentAssertions:** Biblioteca para assertions mais legíveis

### Testes de API
- **Microsoft.AspNetCore.Mvc.Testing:** `WebApplicationFactory<Program>` sobe a app inteira em memória
- **EF Core InMemory:** banco isolado por factory, sem dependência de PostgreSQL
  (o comportamento específico do PostgreSQL — transações, travas, migrations — é coberto pelo `Monburan.IntegrationTests`, com Testcontainers)

### Cobertura de Código
- **Coverlet:** Ferramenta de coleta de cobertura de código
- **ReportGenerator:** Geração de relatórios HTML de cobertura

### Testes E2E
- **Playwright:** Framework para automação de testes end-to-end

## Execução de Testes

### Executar Todos os Testes
```bash
dotnet test
```

> ⚠️ O restore/build da solution exige o workload MAUI Android instalado
> (`dotnet workload install maui-android`), porque o projeto `Monburan.Mobile`
> faz parte da `Monburan.sln`. O CI já instala o workload no job `build-and-test`.

### Executar Testes com Cobertura
```bash
dotnet test --collect:"XPlat Code Coverage" --settings tests/coverlet.runsettings
```

### Executar por Projeto
```bash
dotnet test tests/Monburan.UnitTests
dotnet test tests/Monburan.IntegrationTests
dotnet test tests/Monburan.ApiTests
dotnet test tests/Monburan.E2ETests
```

### Executar Testes E2E
Os testes E2E usam Playwright e sobem a aplicação real. Na primeira execução:

```bash
cd tests/Monburan.E2ETests
dotnet build
pwsh bin/Debug/net10.0/playwright.ps1 install   # baixa os browsers
dotnet test
```

## CI/CD e Cobertura

O pipeline de CI/CD (`.github/workflows/ci-cd.yml`) está configurado para:

1. Executar testes automaticamente em cada push/PR (`main` e `dev`)
2. Coletar cobertura de código usando Coverlet
3. Gerar relatórios HTML usando ReportGenerator
4. Upload dos relatórios de cobertura como artefatos

## Convenções de Testes

### Nomenclatura
- Classes de teste: `{NomeClasse}Tests`
- Métodos de teste: `{MetodoTestado}_{Cenario}_{ResultadoEsperado}`
- Exemplo: `GetJobByIdUseCaseTests.ExecuteAsync_WhenJobExists_ReturnsJobResponse`

### Estrutura dos Testes
```csharp
[Fact]
public async Task MetodoTestado_CenarioEsperado_ResultadoEsperado()
{
    // Arrange
    // Configuração do teste
    
    // Act
    // Execução do método testado
    
    // Assert
    // Verificação do resultado
}
```

### Testes de Teoria (Data-Driven)
```csharp
[Theory]
[InlineData(valor1, esperado1)]
[InlineData(valor2, esperado2)]
public void MetodoTestado_ComVariosInputs_RetornaEsperado(string input, bool expected)
{
    // Act & Assert
}
```

## Próximos Passos

### Curto Prazo
1. Executar os testes E2E no pipeline de CI (issue #3)
2. Aumentar cobertura de testes unitários para outros Use Cases

### Médio Prazo
1. Investigar a causa da falha intermitente em `FullSweepTests.Sweep_Advertiser_AllRoutes` (hoje mascarada pela retentativa do job E2E)
2. Adicionar testes de performance para operações críticas
3. Adicionar testes de segurança

### Longo Prazo
1. Implementar testes de mutação
2. Adicionar testes de contratos (Contract Testing)
3. Automatizar testes visuais

## Métricas Atuais

| Projeto | Testes | Framework principal |
|---|---|---|
| Monburan.UnitTests | 1276 | xUnit + NSubstitute |
| Monburan.IntegrationTests | 57 métodos | xUnit + Testcontainers (PostgreSQL 16) |
| Monburan.ApiTests | 256 | WebApplicationFactory + EF InMemory |
| Monburan.E2ETests | 99 métodos | Playwright |
| **Total** | **~1700** | unitários e de API contados por execução; integração e E2E por método `[Fact]`/`[Theory]` |

- **Cobertura de Código:** Coletada no CI (Coverlet + ReportGenerator, artefato `coverage-report`)
- **Tempo de Execução:** < 1 segundo para testes unitários

## Melhores Práticas

1. **Isolamento:** Cada teste deve ser independente e não depender da ordem de execução
2. **AAA Pattern:** Seguir o padrão Arrange-Act-Assert
3. **Nomes Descritivos:** Nomes de testes devem descrever claramente o que está sendo testado
4. **Mocking:** Usar mocks para dependências externas em testes unitários
5. **Testes Rápidos:** Testes unitários devem ser rápidos (< 100ms)
6. **Testes Determinísticos:** Evitar aleatoriedade nos testes
7. **API Tests:** usar `MonburanApiFactory` — banco InMemory isolado por factory, seed de Development

## Referências

- [xUnit Documentation](https://xunit.net/)
- [NSubstitute Documentation](https://nsubstitute.github.io/)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [Coverlet Documentation](https://github.com/coverlet-coverage/coverlet)
- [Playwright Documentation](https://playwright.dev/)
- [WebApplicationFactory — ASP.NET Core integration tests](https://learn.microsoft.com/aspnet/core/test/integration-tests)
