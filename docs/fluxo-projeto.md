# Fluxos do projeto Monburan

Diagramas do funcionamento da plataforma e do fluxo de desenvolvimento. Estão em Mermaid, que o GitHub renderiza diretamente. As imagens usadas no relatório ficam em `docs/fluxogramas/`.

## 1. Fluxo de uso: candidato e empresa

```mermaid
flowchart TD
    V([Visitante]) --> B[Busca vagas sem login]
    B --> D[Detalhe da vaga]
    V --> SUG[Sugere uma vaga<br/>sem conta, com imagem da publicação]
    SUG --> SMOD{Moderação humana<br/>com rascunho de IA}
    SMOD -->|Aprova| EXT[Vaga externa na busca]
    SMOD -->|Rejeita ou duplicada| SREJ[Imagem apagada após o prazo]
    EXT --> B
    V --> C{Cria conta}
    C -->|Candidato| CC[Cadastro de candidato]
    C -->|Empresa| EC[Cadastro de empresa]
    CC --> LOGIN[Login]
    EC --> LOGIN
    LOGIN --> CAND{Perfil}

    CAND -->|Candidato| CR[Monta currículo ou importa PDF]
    CR --> LINK[Link público do currículo<br/>campos escolhidos, revogável]
    CR --> SAVE[Salva vaga e cria alertas]
    CR --> APP[Candidata-se à vaga]
    APP --> ACOMP[Acompanha em Minhas candidaturas]
    ACOMP --> NOTIF[Recebe notificações]
    CAND --> DEL[Exclui a conta<br/>dados pessoais anonimizados]

    CAND -->|Empresa| PUB[Publica vaga]
    PUB --> ATIVA[Vaga ativa na busca]
    ATIVA -.moderação posterior.-> MOD{Administração}
    MOD -->|Oculta ou reprova, com motivo| REJ[Empresa é avisada]
    ATIVA --> RECEB[Recebe candidaturas]
    RECEB --> SIT[Move a situação de cada candidatura]
    SIT --> NOTIF

    PLANO[Planos e assinaturas] -.limite de vagas ativas.-> PUB
    PLANO -.assistente de IA só no Premium.-> CR
```

## 2. Arquitetura

```mermaid
flowchart LR
    WEB[Site Blazor<br/>WebAssembly + Server] --> API
    APP[App Android<br/>MAUI Blazor Hybrid] -->|JWT Bearer| API
    WEB -->|cookies Identity| API
    API[API REST e páginas<br/>Monburan.Web] --> UC[Casos de uso<br/>Monburan.Core]
    UC --> REPO[Repositórios<br/>EF Core]
    REPO --> DB[(PostgreSQL)]
    UC --> MAIL[E-mail<br/>SMTP / Resend]
    UC --> FILES[Arquivos<br/>uploads locais]
    UC --> EVID[Evidências das sugestões<br/>pasta privada, apagadas após 90 dias]
    UC --> FLAGS[Feature flags<br/>por grupo de acesso]
    UC --> IA[IA: melhoria de currículo,<br/>importação de PDF e rascunho de vaga<br/>provedor configurado]
    API --> HEALTH["Health checks: /health e /health/ready"]
    NGINX[nginx + TLS] --> WEB
```

## 3. Fluxo de desenvolvimento e entrega

```mermaid
flowchart TD
    BR[Branch de trabalho<br/>a partir da dev] --> PR1[PR para dev]
    PR1 --> CI1[CI: build, testes unitários,<br/>integração, API e segurança<br/>sem E2E]
    CI1 -->|verde| MERGE1[Merge na dev]
    MERGE1 --> CI2[CI na dev<br/>validação do push]
    MERGE1 --> PR2[PR dev para main<br/>promoção]
    PR2 --> GATE[origem-do-pr: só aceita a dev]
    PR2 --> CI3[CI completo com E2E<br/>Playwright]
    GATE --> CI3
    CI3 -->|verde| MERGE2[Merge na main]
    MERGE2 --> IMG[docker-ghcr: imagem no GHCR<br/>se mudou src ou infra]
    IMG --> VPS[deploy-vps: atualiza a VPS<br/>backup, rollback e /health]
    MERGE2 --> REL[android-release e ios-build<br/>compilam APK e IPA quando a versão muda]
    REL --> RELEASE[Release no GitHub<br/>publicada só depois do deploy-vps]
    VPS --> RELEASE
```

## 4. Fluxo do board (GitHub Projects)

```mermaid
flowchart LR
    BL[Backlog] --> RFD[Ready for Dev]
    RFD --> IP[In Progress]
    IP --> IR[In Review]
    IR --> QA[In QA]
    QA --> DONE[Done]
```

Campos: *Fase* (Etapa 1 / Etapa 2) e *Prioridade* (Alta / Média / Baixa).
