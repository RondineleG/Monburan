# Estratégia de Testes - Monburan

## Visão Geral

Este documento descreve a estratégia de testes implementada para o projeto Monburan, uma plataforma de recrutamento desenvolvida em .NET 9.0 com Blazor.

## Estrutura de Testes

O projeto está organizado em três camadas de testes:

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

**Total de Testes:** 79 testes

### 2. Testes de Integração (Monburan.IntegrationTests)

**Objetivo:** Testar a integração entre componentes e com banco de dados.

**Localização:** `tests/Monburan.IntegrationTests/`

**Framework:** xUnit + Entity Framework Core InMemory

**Cobertura:**
- **Repositories:** Testes para repositórios de dados
  - JobRepositoryTests

**Observações:**
- Os testes de integração foram criados mas estão enfrentando problemas de dependência de recursos (resources) que precisam ser resolvidos
- Utiliza banco de dados em memória para isolamento
- Testa operações CRUD completas

### 3. Testes E2E (Monburan.E2ETests)

**Objetivo:** Testar o fluxo completo do usuário através da interface.

**Localização:** `tests/Monburan.E2ETests/`

**Framework:** xUnit + Playwright

**Status:** Projeto configurado mas testes não implementados

## Ferramentas Utilizadas

### Framework de Testes
- **xUnit:** Framework de testes unitários para .NET
- **NSubstitute:** Biblioteca para mocking de objetos
- **FluentAssertions:** Biblioteca para assertions mais legíveis

### Cobertura de Código
- **Coverlet:** Ferramenta de coleta de cobertura de código
- **ReportGenerator:** Geração de relatórios HTML de cobertura

### Testes E2E
- **Playwright:** Framework para automação de testes end-to-end

## Execução de Testes

### Executar Todos os Testes Unitários
```bash
cd tests/Monburan.UnitTests
dotnet test
```

### Executar Testes com Cobertura
```bash
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

### Executar Testes de Integração
```bash
cd tests/Monburan.IntegrationTests
dotnet test
```

### Executar Testes E2E
```bash
cd tests/Monburan.E2ETests
dotnet test
```

## CI/CD e Cobertura

O pipeline de CI/CD está configurado para:

1. Executar testes automaticamente em cada push/PR
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
1. Resolver problemas de dependência de recursos nos testes de integração
2. Implementar testes E2E críticos para fluxos principais
3. Aumentar cobertura de testes unitários para outros Use Cases

### Médio Prazo
1. Adicionar testes de performance para operações críticas
2. Implementar testes de carga para APIs
3. Adicionar testes de segurança

### Longo Prazo
1. Implementar testes de mutação
2. Adicionar testes de contratos (Contract Testing)
3. Automatizar testes visuais

## Métricas Atuais

- **Testes Unitários:** 79 testes passando
- **Cobertura de Código:** Configurada mas não medida
- **Tempo de Execução:** < 1 segundo para testes unitários

## Melhores Práticas

1. **Isolamento:** Cada teste deve ser independente e não depender da ordem de execução
2. **AAA Pattern:** Seguir o padrão Arrange-Act-Assert
3. **Nomes Descritivos:** Nomes de testes devem descrever claramente o que está sendo testado
4. **Mocking:** Usar mocks para dependências externas em testes unitários
5. **Testes Rápidos:** Testes unitários devem ser rápidos (< 100ms)
6. **Testes Determinísticos:** Evitar aleatoriedade nos testes

## Problemas Conhecidos

1. **Dependência de Recursos:** Alguns Value Objects dependem de arquivos de recursos que não estão configurados corretamente, causando falhas em testes que tentam validar exceções
2. **Testes de Integração:** Problemas com EF Core e ValueComparer causando falhas ao tentar rastrear entidades
3. **Health Checks:** Implementação de health checks precisa ser revisada para usar a API correta

## Referências

- [xUnit Documentation](https://xunit.net/)
- [NSubstitute Documentation](https://nsubstitute.github.io/)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [Coverlet Documentation](https://github.com/coverlet-coverage/coverlet)
- [Playwright Documentation](https://playwright.dev/)
