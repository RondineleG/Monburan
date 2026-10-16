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

- **Colunas de status:** `Todo` → `In Progress` → `Done`
- **Campo `Fase`:** `Etapa 1` | `Etapa 2`
- **Campo `Prioridade`:** `Alta` | `Media` | `Baixa`

### Distribuição atual das 12 issues

| Issue | Título | Fase | Prioridade |
|---|---|---|---|
| #1 | ci: substituir secret scan por ferramenta dedicada | Etapa 1 | Alta |
| #2 | chore: atualizar pacotes NuGet | Etapa 1 | Média |
| #3 | ci: executar testes E2E com Playwright no pipeline | Etapa 1 | Média |
| #4 | test: migrar testes para Testcontainers PostgreSQL | Etapa 1 | Média |
| #5 | chore: configurar Dependabot | Etapa 1 | Baixa |
| #6 | feat: notificações em tempo real com SignalR | Etapa 2 | Média |
| #7 | mobile: automatizar build e assinatura do APK | Etapa 2 | Alta |
| #8 | feat: endpoints da API para o lado anunciante | Etapa 2 | Média |
| #9 | feat: camada de cache para listagens | Etapa 2 | Baixa |
| #10 | feat: observabilidade com OpenTelemetry | Etapa 2 | Baixa |
| #11 | chore: branch protection e templates | Etapa 1 | Média |
| #12 | docs: documentação de arquitetura e contribuição | Etapa 1 | Baixa |

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
