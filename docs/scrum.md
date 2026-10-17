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

### Distribuição atual das 24 issues

O quadro está distribuído em 2 grupos: **trabalho já implementado** (issues #13–#24, criadas para registrar as entregas concluídas) e **melhorias pendentes** (issues #1–#12, planejadas para as etapas).

#### Coluna `Done` — implementado, integrado e verificado (8)

| Issue | Título | Fase | Prioridade |
|---|---|---|---|
| #13 | feat: modelo de domínio e entidades principais | Etapa 1 | Alta |
| #14 | feat: autenticação JWT com access e refresh token | Etapa 1 | Alta |
| #15 | feat: marketplace público de vagas com busca e paginação | Etapa 1 | Alta |
| #16 | feat: espaço do anunciante com gestão de vagas | Etapa 1 | Média |
| #17 | feat: espaço do candidato e gestão de currículos | Etapa 1 | Média |
| #18 | feat: sistema de planos e assinaturas | Etapa 1 | Baixa |
| #19 | feat: API REST para o fluxo de candidato | Etapa 1 | Alta |
| #21 | chore: empacotamento Docker e compose de deploy | Etapa 1 | Média |

#### Coluna `In QA` — implementado, aguardando validação funcional (2)

| Issue | Título | Fase | Prioridade |
|---|---|---|---|
| #20 | test: suíte E2E com Playwright | Etapa 1 | Média |
| #22 | feat: health checks, logging estruturado e Swagger | Etapa 1 | Baixa |

#### Coluna `In Review` — implementado, aguardando revisão de código (2)

| Issue | Título | Fase | Prioridade |
|---|---|---|---|
| #23 | feat: app mobile Android com MAUI Blazor Hybrid | Etapa 2 | Alta |
| #24 | feat: middlewares de segurança e rate limiting | Etapa 1 | Média |

#### Coluna `In Progress` — em desenvolvimento ativo (2)

| Issue | Título | Fase | Prioridade |
|---|---|---|---|
| #7 | mobile: automatizar build e assinatura do APK | Etapa 2 | Alta |
| #8 | feat: endpoints da API para o lado anunciante | Etapa 2 | Média |

#### Coluna `Ready for Dev` — refinadas, prontas para puxar (7)

| Issue | Título | Fase | Prioridade |
|---|---|---|---|
| #1 | ci: substituir secret scan por ferramenta dedicada | Etapa 1 | Alta |
| #2 | chore: atualizar pacotes NuGet | Etapa 1 | Média |
| #3 | ci: executar testes E2E com Playwright no pipeline | Etapa 1 | Média |
| #4 | test: migrar testes para Testcontainers PostgreSQL | Etapa 1 | Média |
| #5 | chore: configurar Dependabot | Etapa 1 | Baixa |
| #11 | chore: branch protection e templates | Etapa 1 | Média |
| #12 | docs: documentação de arquitetura e contribuição | Etapa 1 | Baixa |

#### Coluna `Backlog` — demandas futuras de longo prazo (3)

| Issue | Título | Fase | Prioridade |
|---|---|---|---|
| #6 | feat: notificações em tempo real com SignalR | Etapa 2 | Média |
| #9 | feat: camada de cache para listagens | Etapa 2 | Baixa |
| #10 | feat: observabilidade com OpenTelemetry | Etapa 2 | Baixa |

> **Nota de transparência:** os cards nas colunas `Done`, `In QA` e `In Review` representam trabalho já desenvolvido e presente no repositório. Como o projeto foi construído com commits diretos na `main` (sem fluxo de PR), essas colunas refletem o estado de implementação/validação real de cada entregável, e não a passagem por PRs abertos — o que aconteceria ao adotar o fluxo de PR completo.

> **Entregas recentes (16/10) ainda sem card próprio no board:** deploy em produção na VPS (imagem `ghcr.io/rondineleg/monburan` + nginx + TLS via certbot em `monburan.com.br`), workflow `docker-ghcr.yml` e `infra/vps/build-push.sh` para build/push da imagem, envio de e-mail transacional via SMTP (Resend), correção do domínio para `monburan.com.br`, renovação de UI/UX do design system (web e mobile) e correção do branding para MONBURAN no app Android. A issue #21 foi movida para `Done` por cobrir o empacotamento Docker/compose de deploy.

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
