# ESQUELETO DO RELATÓRIO — ETAPA 1

> **Instruções de uso (apague este bloco antes de exportar o PDF):**
> - Este arquivo contém **todas as seções obrigatórias** do template, na mesma ordem e numeração.
> - As passagens entre `[COLCHETES]` são **orientações do que escrever**. Substitua pelo seu próprio texto.
> - Os dados técnicos já conferidos com o repositório estão pré-preenchidos nas seções 4.1.5, 4.1.6 e 4.1.8 — aproveite-os, mas reescreva com suas palavras.
> - Formatação ABNT: fonte Times New Roman 12, espaçamento 1,5, margens esquerda 3 cm / superior 2 cm, recuo de primeira linha 1,25 cm, numeração de páginas no canto superior direito.
> - Exporte como **um único PDF** (não zip, não anexo separado).

---

## 1 Introdução

[Apresente o trabalho: tema geral, o problema social abordado, a solução proposta e a estrutura do relatório. Explique que o projeto consiste no desenvolvimento de uma plataforma digital (web + mobile) de intermediação entre candidatos e oportunidades de trabalho, voltada a uma comunidade específica — identifique essa comunidade aqui. Finalize indicando como o relatório está organizado.]

## 2 Geração de Ideia

[Relate como o grupo chegou à ideia: brainstorming, rodas de conversa com a comunidade, identificação de dores reais. Descreva as alternativas consideradas e por que a ideia final foi escolhida. Conecte o tema "Resolvendo problemas na comunidade em que estamos inseridos" com a escolha.]

## 3 Prova de Conceito

[Descreva a validação inicial da viabilidade: o que foi testado para confirmar que a ideia é exequível. Informe que já existe um protótipo funcional — aplicação web (.NET 10 + Blazor) e app mobile (MAUI Blazor Hybrid) compilando sem erros, com 160 testes automatizados aprovados. Mencione prints/telas, o endereço do repositório GitHub (https://github.com/RondineleG/Monburan) e o board de gestão (https://github.com/users/RondineleG/projects/14).]

## 4 Etapa 1

### 4.1 Definição do Escopo do Projeto

#### 4.1.1 Compreensão do Problema na Comunidade

[Descreva o problema concreto que a comunidade enfrenta — ex.: dificuldade de acesso a vagas de trabalho, desinformação sobre oportunidades locais, barreiras digitais. Apresente dados (se houver fontes) e como o problema foi apurado. **Esta é a seção mais importante da avaliação — conecte o software a uma dor real.**]

#### 4.1.2 Definição Clara do Objetivo

**Objetivo geral:** [Descreva em uma frase o objetivo do projeto.]

**Objetivos específicos:**
- [ ] objetivo específico 1
- [ ] objetivo específico 2
- [ ] objetivo específico 3

#### 4.1.3 Delimitação do Escopo

**O que o projeto faz (dentro do escopo):**
- [funcionalidades incluídas — ex.: cadastro e autenticação, publicação e gestão de vagas, busca e candidatura, currículos, notificações, planos e assinaturas, app mobile Android]

**O que o projeto NÃO faz (fora do escopo):**
- [limites definidos — ex.: publicação na Play Store/App Store na Etapa 1, notificações push, processos seletivos por vídeo, integração com LinkedIn]

#### 4.1.4 Identificação dos Stakeholders da Comunidade

| Stakeholder | Papel / Interesse |
|---|---|
| Candidatos (comunidade local) | [buscam oportunidades] |
| Empresas / anunciantes locais | [publicam vagas] |
| Centro de emprego / prefeitura | [apoio institucional] |
| Faculdade / disciplina | [orientação acadêmica] |
| Grupo de desenvolvimento | [execução do projeto] |

#### 4.1.5 Atividades e Tarefas Principais

[Descreva o que foi feito. Sugestão de parágrafos curtos por frente:]
- **Modelagem de domínio e camada de dados:** entidades, repositórios, migrações de banco (PostgreSQL/EF Core).
- **Backend e API:** casos de uso, autenticação JWT (access/refresh), endpoints REST, upload de arquivos, paginação.
- **Interface web:** shell do aplicativo, design system próprio (dark), páginas públicas e áreas do anunciante e do candidato.
- **App mobile:** projeto MAUI Blazor Hybrid com páginas de candidato, cliente HTTP tipado e SecureStorage.
- **Qualidade:** suíte de testes (unitários, integração, API e E2E) — 160 testes automatizados.
- **Infraestrutura:** Docker, CI/CD (GitHub Actions), imagem publicada no GHCR, deploy na VPS com nginx + TLS (certbot), e-mail transacional via SMTP (Resend), health checks e logging estruturado.

#### 4.1.6 Ferramentas de Desenvolvimento e Teste

| Camada | Ferramentas |
|---|---|
| Linguagem / runtime | C# / .NET 10.0 |
| Web | Blazor (WebAssembly + Server) |
| Mobile | .NET MAUI Blazor Hybrid (Android) |
| Banco de dados | PostgreSQL + Entity Framework Core |
| Autenticação | JWT (access/refresh), ASP.NET Identity |
| E-mail | SMTP via MailKit (Resend), SendGrid como fallback |
| Testes | xUnit, NSubstitute, FluentAssertions, WebApplicationFactory (EF InMemory), Playwright |
| Contêineres | Docker / Docker Compose, imagem no GHCR |
| CI/CD | GitHub Actions |
| Produção | VPS + nginx + TLS (Let's Encrypt/certbot), domínio monburan.com.br |
| Logs / saúde | Serilog, Health Checks |
| Gestão | GitHub Projects, GitHub Issues |

#### 4.1.7 Métricas de Sucesso

[Defina métricas. Sugestões — combine as que fizerem sentido:]
- **Técnicas:** build sem erros (0 erros / 0 avisos), 160 testes automatizados aprovados, pipeline de CI/CD operante.
- **De produto:** tempo médio de resposta da listagem de vagas, taxa de conclusão de cadastro, nº de candidaturas enviadas.
- **De impacto na comunidade:** nº de candidatos alcançados, nº de empresas cadastradas, taxa de resposta a candidaturas.

#### 4.1.8 Cronograma e Recursos

**Cronograma (histórico real de desenvolvimento):**

| Período | Entrega |
|---|---|
| 06/10/2026 | Estruturação da solução e modelo de domínio |
| 07/10/2026 | Casos de uso, DTOs, validações, camada de dados |
| 08/10/2026 | Autenticação e design system |
| 09/10/2026 | Páginas públicas, marketplace e área do anunciante |
| 12/10/2026 | Área do candidato, currículos, planos e assinaturas |
| 13/10/2026 | Notificações e migrações de banco |
| 14/10/2026 | Testes unitários, de integração e E2E |
| 15/10/2026 | Docker, deploy e utilitários |
| 16/10/2026 | API REST, app mobile, documentação, deploy na VPS (imagem no GHCR + nginx/TLS), domínio monburan.com.br, e-mail via SMTP (Resend) e renovação de UI/UX (web e mobile) |

**Recursos:** [descreva recursos humanos (membros do grupo e papéis), materiais (estação de trabalho, contas de serviço) e de infraestrutura (GitHub, Docker, banco de dados).]

---

## 5 Etapa 2

## 5.1 Aplicação, Testes e Validação de um Projeto Orientado a Testes com Programação Mobile

### 5.1.1 Elaboração da Aplicação Mobile

### 5.1.2 Elaboração dos Testes

### 5.1.3 Validação do Projeto

### 5.1.4 Ciclo de Desenvolvimento e Validação com TDD

> *(Seções 5.x ficam apenas com o título nesta entrega — serão desenvolvidas na Etapa 2.)*

## 6 SCRUM Configurado

[Descreva a metodologia ágil adotada e a configuração do Scrum.]

**Papéis:**
- Product Owner: [nome]
- Scrum Master: [nome]
- Desenvolvedores: [nomes]

**Artefatos:**
- **Product Backlog:** [GitHub Issues — https://github.com/RondineleG/Monburan/issues, com 24 itens priorizados]
- **Sprint Backlog:** [itens atribuídos às sprints no GitHub Projects]
- **Incremento:** [entrega funcional por sprint]

**Quadro (board) configurado:** [GitHub Projects #14 — https://github.com/users/RondineleG/projects/14, com fluxo de 6 colunas — *Backlog / Ready for Dev / In Progress / In Review / In QA / Done* — e campos personalizados *Fase* (Etapa 1 / Etapa 2) e *Prioridade* (Alta / Média / Baixa).]

**Definição de "Feito":** [ex.: código revisado, testes aprovados, build sem erros, documentação atualizada.]

**Milestones:** [Etapa 1 — https://github.com/RondineleG/Monburan/milestone/1 (prazo 25/10/2026); Etapa 2 — milestone/2.]

## 7 Vídeo de Apresentação

### 7.1 Etapa 1

[Insira o link do vídeo da Etapa 1 e uma breve descrição do conteúdo.]

### 7.2 Etapa 2

> *(Seção da Etapa 2 — apenas o título nesta entrega.)*

## 8 Conclusão

[Apresente as conclusões do relatório parcial: o que foi alcançado na Etapa 1, o que ficou pendente para a Etapa 2, e a contribuição do projeto para a comunidade identificada.]

## 9 Referências Bibliográficas

[Lista de referências em formato ABNT (NBR 10235 / ABNT NBR 6023). Exemplos de formatação:]

SILVA, João das Neves. **Engenharia de software**: princípios e práticas. São Paulo: Atlas, 2024.

SOMMERVILLE, Ian. **Engenharia de software**. 10. ed. São Paulo: Pearson, 2019.

BECK, Kent. **TDD explícito**: testes e código de produção. Rio de Janeiro: Alta Books, 2021.

SCHWABER, Ken; SUTHERLAND, Jeff. **Guia do Scrum**. Disponível em: https://scrumguides.org/. Acesso em: 16 out. 2026.

[Mantenha a ordem alfabética. Inclua as fontes técnicas e acadêmicas que realmente consultaram.]
