# Configuração SCRUM — Monburan

Este documento registra a metodologia ágil configurada para o projeto, servindo de evidência para a **seção 6 (SCRUM Configurado)** do relatório da Etapa 1.

## 1. Visão geral

O projeto segue a metodologia **Scrum** conforme o *Scrum Guide* (Schwaber e Sutherland). O trabalho é organizado em **sprints** gerenciadas por meio do **GitHub Projects**, com backlog priorizado no **GitHub Issues**.

- **Repositório:** https://github.com/RondineleG/Monburan
- **Quadro (board):** https://github.com/users/RondineleG/projects/14
- **Backlog:** https://github.com/RondineleG/Monburan/issues

## 2. Papéis

| Papel | Responsável |
|---|---|
| Product Owner | `[nome do integrante]` |
| Scrum Master | `[nome do integrante]` |
| Desenvolvedores | `[nomes dos integrantes]` |

## 3. Artefatos

### 3.1 Product Backlog
Backlog priorizado no **GitHub Issues** com rótulos por tipo (`feat`, `fix`, `chore`, `ci`, `test`, `docs`, `mobile`) e campos personalizados de **Prioridade** (Alta / Média / Baixa).

### 3.2 Sprint Backlog
Itens atribuídos à sprint corrente através do campo **Fase** (Etapa 1 / Etapa 2) e do status no quadro.

### 3.3 Incremento
Entrega funcional a cada sprint: build compilando sem erros, testes aprovados e funcionalidade verificável.

## 4. Quadro (board) configurado

O **GitHub Projects #14** está configurado com:

- **Campo `Fase`:** `Etapa 1` | `Etapa 2`
- **Campo `Prioridade`:** `Alta` | `Media` | `Baixa`

### Fluxo otimizado (6 colunas)

O quadro segue o fluxo de trabalho abaixo, com critério de sucesso definido para cada coluna:

| # | Coluna | Significado | Critério de sucesso |
|---|---|---|---|
| 1 | **Backlog** | Ideias e demandas futuras; tarefas brutas de longo prazo ainda não refinadas. | Nenhuma tarefa entra sem descrição mínima do problema. |
| 2 | **Ready for Dev** | Aprovado para execução; cards refinados, estimados e com critérios de aceite claros. | Especificação completa, sem dúvidas técnicas antes do início. |
| 3 | **In Progress** | Em desenvolvimento ativo (com limite de WIP — ex.: máx. 1–2 cards por pessoa). | Existe branch criada ou Draft PR associada ao card. |
| 4 | **In Review** | Revisão de código / CI; PR aberto aguardando aprovação ou testes automatizados. | PR passou nas verificações de CI e possui os approvals necessários. |
| 5 | **In QA** | Validação e testes; alteração em ambiente de Staging/Preview. | Funcionalidade testada (manual/automatizada) no staging sem regressões. |
| 6 | **Done** | Concluído / em produção; código mesclado na `main` e disponibilizado. | PR fechada/mesclada e card arquivado/concluído. |

### Distribuição atual das 12 issues

| Issue | Título | Coluna | Fase | Prioridade |
|---|---|---|---|---|
| #6 | feat: notificações em tempo real com SignalR | Backlog | Etapa 2 | Média |
| #9 | feat: camada de cache para listagens | Backlog | Etapa 2 | Baixa |
| #10 | feat: observabilidade com OpenTelemetry | Backlog | Etapa 2 | Baixa |
| #1 | ci: substituir secret scan por ferramenta dedicada | Ready for Dev | Etapa 1 | Alta |
| #2 | chore: atualizar pacotes NuGet | Ready for Dev | Etapa 1 | Média |
| #3 | ci: executar testes E2E com Playwright no pipeline | Ready for Dev | Etapa 1 | Média |
| #4 | test: migrar testes para Testcontainers PostgreSQL | Ready for Dev | Etapa 1 | Média |
| #5 | chore: configurar Dependabot | Ready for Dev | Etapa 1 | Baixa |
| #11 | chore: branch protection e templates | Ready for Dev | Etapa 1 | Média |
| #12 | docs: documentação de arquitetura e contribuição | Ready for Dev | Etapa 1 | Baixa |
| #7 | mobile: automatizar build e assinatura do APK | In Progress | Etapa 2 | Alta |
| #8 | feat: endpoints da API para o lado anunciante | In Progress | Etapa 2 | Média |

> **Nota sobre a coluna In Progress:** o critério formal exige branch ou Draft PR associada. O projeto foi desenvolvido com commits diretos na `main` (sem fluxo de PR), então os cards #7 e #8 estão em `In Progress` por terem implementação parcial já presente na `main` (#7: job `android-build` criado, faltando gatilho por `paths` e cache de NuGet; #8: endpoints e guards criados, faltando teste de dashboard). Ao adotar o fluxo de PR, esses cards migrariam para `In Review`/`In QA` conforme o fluxo define.

As colunas `In Review`, `In QA` e `Done` estão vazias por enquanto — serão preenchidas conforme os cards avançarem pelo fluxo (hoje não há PR aberto nem ambiente de staging).

## 5. Milestones (etapas da avaliação)

| Milestone | Escopo | Prazo |
|---|---|---|
| **Etapa 1** | Escopo, prova de conceito, infraestrutura, CI/segurança, documentação e relatório parcial | 25/10/2026 |
| **Etapa 2** | Aplicação mobile orientada a testes, TDD e validação | a definir |

## 6. Definição de "Feito" (DoD)

Um item é considerado pronto quando:

- [ ] Código implementado e revisado
- [ ] Testes automatizados adicionados e aprovados
- [ ] Build compilando sem erros e sem avisos
- [ ] Nenhuma credencial ou segredo hardcoded
- [ ] Documentação atualizada quando aplicável
- [ ] Issue movida para `Done` no quadro

## 7. Eventos Scrum

| Evento | Frequência | Duração |
|---|---|---|
| Sprint Planning | Início de cada sprint | `[definir]` |
| Daily Scrum | Diária | 15 min |
| Sprint Review | Fim de cada sprint | `[definir]` |
| Retrospectiva | Fim de cada sprint | `[definir]` |

> **Nota:** preencha os campos entre crases (`[...]`) com os nomes e frequências reais do grupo antes de exportar o relatório em PDF.
