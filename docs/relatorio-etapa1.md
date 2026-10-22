# ESQUELETO DO RELATÓRIO — ETAPA 1

> **Instruções de uso (apague este bloco antes de exportar o PDF):**
> - Este arquivo contém **todas as seções obrigatórias** do template, na mesma ordem e numeração.
> - Os trechos entre `[COLCHETES]` dependem do grupo (comunidade, nomes, vídeo, métricas-alvo). **Não preencha com dados inventados.**
> - Os dados técnicos foram conferidos com o repositório em 22/10/2026. Reescreva com suas próprias palavras, como exige o template.
> - Formatação ABNT: fonte Times New Roman 12, espaçamento 1,5, margens esquerda 3 cm / superior 2 cm, recuo de primeira linha 1,25 cm, numeração de páginas no canto superior direito.
> - Exporte como **um único PDF** (não zip, não anexo separado). O AVA aceita **uma tentativa**.

---

## 1 Introdução

[Identifique a comunidade atendida pelo projeto (cidade, bairro ou público) e o problema social abordado.]

O projeto Monburan é uma plataforma digital, com aplicação web e aplicativo Android, que aproxima candidatos de empresas que publicam vagas. A solução centraliza perfis, currículos, vagas e candidaturas em um único lugar, em vez de dispersá-los em múltiplas plataformas e grupos de redes sociais.

[Finalize indicando como o relatório está organizado.]

## 2 Geração de Ideia

[Relate como o grupo chegou à ideia: conversas, pesquisa, observação da comunidade. Descreva as alternativas consideradas e por que a ideia final foi escolhida. Conecte com o tema "Resolvendo problemas na comunidade em que estamos inseridos". Inclua evidências, se houver.]

## 3 Prova de Conceito

[Descreva o que foi testado para confirmar que a ideia é exequível.]

O protótipo está publicado e funcional:
- **Aplicação web:** https://monburan.com.br (.NET 10 + Blazor), com páginas públicas de vagas, cadastro de candidatos e empresas e área de candidaturas.
- **Aplicativo Android:** versão 1.5.0, disponível na release https://github.com/RondineleG/Monburan/releases/tag/v1.5.0 (APK).
- **Repositório:** https://github.com/RondineleG/Monburan
- **Board de gestão:** https://github.com/users/RondineleG/projects/14

[Inserir prints das telas: página inicial, busca de vagas, candidatura e painel da empresa.]

## 4 Etapa 1

### 4.1 Definição do Escopo do Projeto

#### 4.1.1 Compreensão do Problema na Comunidade

[Descreva o problema concreto que a comunidade enfrenta, com a forma como foi apurado (pesquisa, entrevistas, dados públicos) e as fontes. **Esta é a seção mais importante da avaliação: conecte o software a uma dor real.** Sem essa evidência, a seção fica genérica.]

#### 4.1.2 Definição Clara do Objetivo

**Objetivo geral:** oferecer uma plataforma web e móvel que facilite o encontro entre candidatos e empresas que publicam vagas.

**Objetivos específicos:**
- [ ] permitir o cadastro e a autenticação de candidatos e empresas, em perfis separados;
- [ ] permitir que empresas publiquem, editem e acompanhem vagas;
- [ ] permitir que candidatos busquem vagas, se candidatem e acompanhem o andamento;
- [ ] disponibilizar o acesso também pelo aplicativo Android.

#### 4.1.3 Delimitação do Escopo

**O que o projeto faz (dentro do escopo da Etapa 1):**
- cadastro e autenticação, com perfis de candidato e de anunciante (empresa);
- publicação, edição e moderação de vagas;
- busca de vagas, candidatura e acompanhamento de candidaturas;
- currículos, com importação de PDF;
- notificações na plataforma e por e-mail;
- planos e assinaturas, com limites por plano;
- aplicativo Android (APK).

**O que o projeto NÃO faz (fora do escopo):**
- publicação nas lojas Play Store e App Store;
- aplicativo iOS (o IPA da release é não assinado e não há conta de desenvolvedor para distribuição);
- notificações push e chat em tempo real;
- isolamento de dados por tenant (o campo existe, mas a lógica ainda não foi implementada).

#### 4.1.4 Identificação dos Stakeholders da Comunidade

| Stakeholder | Papel / Interesse |
|---|---|
| Candidatos | buscam oportunidades e acompanham candidaturas |
| Empresas / anunciantes | publicam vagas e recebem candidaturas |
| Administração da plataforma | modera vagas e contas |
| [Instituição de apoio, se houver] | [definir] |
| Grupo de desenvolvimento | execução do projeto |

#### 4.1.5 Atividades e Tarefas Principais

[Descreva com suas palavras. Dados de referência:]
- **Modelagem de domínio e dados:** entidades, repositórios e migrations (PostgreSQL com EF Core 10).
- **Backend e API:** casos de uso, autenticação ASP.NET Identity com JWT, endpoints REST, upload de arquivos e paginação.
- **Interface web:** design system próprio em CSS, páginas públicas e áreas de candidato e de empresa.
- **Aplicativo mobile:** .NET MAUI Blazor Hybrid para Android, com cliente HTTP tipado e armazenamento seguro do token.
- **Qualidade:** suíte de testes automatizados. Em 22/10/2026, 1.095 testes unitários e 245 testes de API passaram; há também 57 testes de integração (com PostgreSQL em contêiner) e 99 testes E2E com Playwright.
- **Infraestrutura:** Docker, GitHub Actions, imagem no GHCR, publicação na VPS com nginx e TLS, e-mail transacional via SMTP, health checks e logs estruturados.

#### 4.1.6 Ferramentas de Desenvolvimento e Teste

| Camada | Ferramentas |
|---|---|
| Linguagem / runtime | C# / .NET 10.0 |
| Web | Blazor (WebAssembly + Server) |
| Mobile | .NET MAUI Blazor Hybrid (Android) |
| Banco de dados | PostgreSQL + Entity Framework Core 10 |
| Autenticação | ASP.NET Core Identity + JWT Bearer |
| Validação | validadores próprios em `Monburan.Core/Validations` |
| E-mail | SMTP via MailKit (Resend), SendGrid como alternativa |
| Testes | xUnit, NSubstitute, FluentAssertions, WebApplicationFactory, Testcontainers (PostgreSQL), Playwright |
| Contêineres | Docker / Docker Compose, imagem no GHCR |
| CI/CD | GitHub Actions |
| Produção | VPS com nginx e TLS (Let's Encrypt / certbot), domínio monburan.com.br |
| Logs / saúde | Serilog, Health Checks |
| Gestão | GitHub Projects, GitHub Issues e Milestones |

#### 4.1.7 Métricas de Sucesso

[Defina as metas com o grupo. Valores de referência, já verificáveis:]
- **Técnicas:** build sem erros; testes unitários e de API aprovados (1.095 e 245 em 22/10/2026); pipeline de CI/CD verde na `main`.
- **De produto:** [definir: tempo de resposta da listagem de vagas; taxa de conclusão de cadastro; número de candidaturas enviadas].
- **De impacto na comunidade:** [definir: candidatos alcançados; empresas cadastradas; taxa de resposta às candidaturas].

#### 4.1.8 Cronograma e Recursos

**Cronograma (histórico real do repositório, por data de commit):**

| Período | Entrega |
|---|---|
| 06/10/2026 | Estrutura da solução e modelo de domínio |
| 07/10/2026 | Casos de uso, DTOs, validações e camada de dados |
| 08/10/2026 | Autenticação, fluxos de conta e design system escuro |
| 09/10/2026 | Páginas públicas, marketplace de vagas e área da empresa |
| 12/10/2026 | Área do candidato, currículos, planos e assinaturas |
| 13/10/2026 | Notificações, serviços de plataforma e migrations |
| 14/10/2026 | Testes unitários, de integração e E2E |
| 15/10/2026 | Docker, deploy e utilitários |
| 16/10/2026 | API REST, aplicativo Android, documentação e configuração SCRUM |
| 17/10/2026 | Auditoria UI x API e correções funcionais e de segurança |
| 18/10/2026 | Revisão de UX/UI e nova busca de vagas |
| 19/10/2026 | Deploy automático na VPS e página 404 |
| 20/10/2026 | Correções do aplicativo Android |
| 21/10/2026 | Tokens e componentes de interface e telas de conta |
| 22/10/2026 | Pipeline de CI, correções de busca e assinaturas e release v1.5.0 |

**Recursos:** [Membros do grupo e papéis. Ambiente: estação de trabalho, GitHub, Docker e PostgreSQL.]

---

## 5 Etapa 2

## 5.1 Aplicação, Testes e Validação de um Projeto Orientado a Testes com Programação Mobile

### 5.1.1 Elaboração da Aplicação Mobile

### 5.1.2 Elaboração dos Testes

### 5.1.3 Validação do Projeto

### 5.1.4 Ciclo de Desenvolvimento e Validação com TDD

> *(Seções 5.x ficam apenas com o título nesta entrega — serão desenvolvidas na Etapa 2.)*

## 6 SCRUM Configurado

[Descreva a metodologia adotada e a configuração do Scrum com suas palavras. Base em `docs/scrum.md`.]

**Papéis:**
- Product Owner: [nome completo]
- Scrum Master: [nome completo]
- Desenvolvedores: [nomes completos]

**Artefatos:**
- **Product Backlog:** issues do repositório (https://github.com/RondineleG/Monburan/issues), com 24 itens até 22/10/2026.
- **Sprint Backlog:** itens atribuídos às sprints pelo campo *Fase* no GitHub Projects.
- **Incremento:** entrega funcional verificável ao fim de cada ciclo.

**Quadro (board) configurado:** GitHub Projects #14 (https://github.com/users/RondineleG/projects/14), com seis colunas de status — *Backlog, Ready for Dev, In Progress, In Review, In QA, Done* — e os campos personalizados *Fase* (Etapa 1 / Etapa 2) e *Prioridade* (Alta / Média / Baixa).

**Definição de "Feito":** [definir com o grupo; ex.: código revisado em PR para `dev`, testes aprovados, build sem erros, documentação atualizada.]

**Milestones:** Etapa 1 (https://github.com/RondineleG/Monburan/milestone/1), com prazo de 25/10/2026; Etapa 2 (milestone/2), sem prazo definido.

## 7 Vídeo de Apresentação

### 7.1 Etapa 1

[Insira o link do vídeo não listado e uma breve descrição do conteúdo.]

### 7.2 Etapa 2

> *(Seção da Etapa 2 — apenas o título nesta entrega.)*

## 8 Conclusão

[Escreva com suas palavras: o que foi alcançado na Etapa 1, o que fica para a Etapa 2 e a contribuição para a comunidade identificada. Inclua também as limitações conhecidas do protótipo.]

## 9 Referências Bibliográficas

[Liste apenas as fontes que o grupo realmente consultou, em ABNT (NBR 6023), em ordem alfabética. Conferir os dados antes de entregar.]

BECK, Kent. **Test-driven development**: by example. Boston: Addison-Wesley, 2002.

MARTIN, Robert C. **Clean architecture**: a craftsman's guide to software structure and design. Boston: Prentice Hall, 2017.

SCHWABER, Ken; SUTHERLAND, Jeff. **The Scrum guide**. [S. l.]: Scrum.org, 2020. Disponível em: https://scrumguides.org/. Acesso em: [data].

[Acrescente as fontes da pesquisa com a comunidade e os materiais da disciplina.]

Repositório e recursos do projeto (fontes primárias):
- MONBURAN. **Repositório do projeto**. GitHub, 2026. Disponível em: https://github.com/RondineleG/Monburan. Acesso em: [data].
- MONBURAN. **Board de gestão**. GitHub Projects, 2026. Disponível em: https://github.com/users/RondineleG/projects/14. Acesso em: [data].
- MONBURAN. **Aplicação web**. 2026. Disponível em: https://monburan.com.br. Acesso em: [data].
