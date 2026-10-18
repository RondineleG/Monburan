# Sugestões de melhoria de currículo com IA

A candidata ou o candidato abre o próprio currículo, autoriza o envio e recebe sugestões: resumo profissional
reescrito, frases de destaque por experiência, habilidades que valem destacar, o que falta e dicas gerais. **Nada é
alterado nem guardado**: o resultado só existe na tela (e na resposta da API), e a pessoa decide o que aproveitar.

A aplicação não depende de um provedor específico. Ela fala dois protocolos: o de **"chat completions" da OpenAI**
(`POST {endereço}/chat/completions`, que serve OpenAI, OpenRouter, DeepSeek, Xiaomi, Ollama e os demais compatíveis) e o de
**mensagens da Anthropic** (`POST {endereço}/v1/messages`). Quais serviços existem, qual é o padrão, o modelo e o nome mostrado
à pessoa são cadastrados pelo administrador na tela **Provedores de IA**, e vivem no banco: veja
[IA-PROVEDORES.md](IA-PROVEDORES.md).

## Como funciona

1. A tela do currículo mostra o painel **só quando a IA está configurada**: há um provedor padrão ativo e a chave dele
   existe no servidor. Sem isso nada aparece no site nem no app.
2. A pessoa marca a autorização expressa de envio e pede as sugestões. A API exige o mesmo
   (`acceptsExternalProcessing: true` no corpo), então nenhum cliente envia sem autorização.
3. O caso de uso (`ImproveResumeUseCase`) confere que o currículo é da própria pessoa, aplica o limite de usos e
   monta a conversa. Sai **só conteúdo profissional**: título, resumo, experiências, formação, habilidades e idiomas.
   Não vão nome, e-mail, telefone nem endereço, e e-mails, telefones, CPF/CNPJ, CEP e links digitados em campos
   livres são trocados por marcadores (`[e-mail]`, `[telefone]`, `[documento]`, `[cep]`, `[link]`). O texto é
   limitado em tamanho.
4. O cliente (`AiTextClient`) escolhe o provedor padrão, lê a chave da variável de ambiente dele e fala o protocolo
   certo (`OpenAiProtocol` ou `AnthropicProtocol`); tira o raciocínio interno do modelo da resposta e devolve o texto. O caso de uso lê o JSON com tolerância (aceita cercas de código, texto em volta, `snake_case`) e limita
   o tamanho de cada item.
5. As falhas viram mensagens seguras (tempo esgotado, serviço ocupado, "não foi possível gerar agora"). O detalhe do
   provedor (status e um trecho curto do erro) vai só para o log, nunca a chave nem o texto do currículo.

O modelo recebe a instrução de **não inventar** empresas, cargos, datas, formações nem números: quando uma melhoria
pede um resultado que só a pessoa conhece, ele usa um marcador como `[X%]`. Mesmo assim, a tela avisa que as
sugestões são um ponto de partida e que só vale usar o que for verdadeiro e comprovável.

## Configuração

Os **provedores** (endereço, modelo, protocolo, nome, tempo limite, tokens, qual é o padrão) são cadastrados pela tela do
administrador e ficam no banco; a **chave** de cada um fica numa variável de ambiente `AI_KEY_*` do servidor. Tudo isso
(passo a passo, campos, modelos prontos, segurança e erros do teste) está em [IA-PROVEDORES.md](IA-PROVEDORES.md).

No `appsettings.json` ficam só os ajustes gerais, seção `Ai` (sem segredo):

| Chave | Padrão | Para quê |
|---|---|---|
| `Ai:MaxRequestsPerHour` | `5` | Usos por pessoa por hora (cada chamada tem custo). |
| `Ai:AllowPrivateNetworks` | `false` | Libera endereços da rede interna ou do próprio computador (um Ollama local). **Só em desenvolvimento** (`appsettings.Development.json`); em produção deixe `false`, senão um provedor cadastrado poderia levar o servidor a acessar serviços internos. A tela não altera isso. |
| `Ai:CatalogRefreshSeconds` | `60` | De quantos em quantos segundos cada instância relê os provedores do banco. |

**Trocar de provedor** é escolher outro como padrão na tela (ou cadastrar um novo), sem deploy.

## Onde guardar a chave

**A chave nunca vai para o repositório, nem para o banco, nem pela tela.** Cada ambiente tem o seu lugar:

- **Desenvolvimento:** `dotnet user-secrets set "AI_KEY_OPENROUTER" "<chave>" --project src/Monburan.Web` (fica fora do
  repositório, no perfil do usuário). O nome da variável é o que o administrador informa na tela.
- **Produção (VPS):** uma linha `AI_KEY_OPENROUTER=<chave>` no arquivo `ai-keys.env` de `/opt/monburan` (modelo em
  `infra/vps/ai-keys.env.example`; permissão `600`) e `docker compose up -d --force-recreate app`. O compose carrega esse arquivo
  (`env_file`); só variáveis `AI_KEY_*` são lidas pelo app. **O `docker-compose.yml` da VPS é uma cópia** de
  `infra/vps/docker-compose.prod.yml`: ao receber uma versão nova do repositório, copie o arquivo de novo, senão o
  `ai-keys.env` não é carregado.
- **GitHub (Settings → Secrets and variables → Actions):** guarda segredos que os *workflows* leem
  (`${{ secrets.NOME }}`), e ninguém consegue ler o valor depois de salvo. Nenhum workflow usa as chaves de IA (o app roda
  na VPS e lê o `ai-keys.env`), então **não é preciso guardá-las no GitHub**. Só passaria a fazer sentido se um workflow de
  deploy fosse criado para gravá-las na VPS (precisaria também do acesso SSH ao servidor como segredo).

## API

Ambos exigem o perfil de candidato (JWT `ApiBearer`):

- `GET /api/v1/resumes/improvements/availability` devolve `{ "enabled": bool, "provider": string|null }`. O app deve
  esconder o recurso quando `enabled` for falso.
- `POST /api/v1/resumes/{id}/improvements` com `{ "acceptsExternalProcessing": true }` devolve
  `ResumeImprovementResponse` (`summary`, `experiences[]`, `skillsToAdd[]`, `missingSections[]`, `tips[]`, `model`,
  `generatedAt`). Erros: `400` sem autorização, IA desligada, currículo sem conteúdo, limite de uso ou falha do
  provedor (mensagem segura em `detail`); `403` para quem não é candidato; `404` para currículo inexistente ou de
  outra pessoa.

## Privacidade (LGPD)

O envio é um compartilhamento com terceiro, possivelmente fora do Brasil. Por isso: autorização expressa a cada uso
(na tela e na API), conteúdo mínimo e sem identificadores, nada guardado do nosso lado, e a Política de Privacidade
(`/privacy`, item 5) descreve o envio. O tratamento no provedor segue os termos dele, que a empresa deve
conferir antes de ativar em produção, com apoio jurídico para o texto da política.

## Testes

- Unitários: remoção de dados pessoais, montagem da conversa, leitura da resposta, caso de uso (autorização, dono do
  currículo, limite, falhas), limitador e a camada de provedores (protocolos, cliente, catálogo, regras do cadastro,
  casos de uso do administrador, classificação de endereços).
- API: o cliente de IA é trocado por um falso (os testes nunca falam com um provedor de verdade, mesmo com segredo
  de usuário na máquina). Cobrem autorização, dono, limite por hora e falhas.
- Os protocolos em HTTP de verdade contra um servidor local que imita OpenAI e Anthropic (cabeçalhos, JSON e acentos
  reais, status de erro, tempo esgotado, redirecionamento e resposta gigante).
- E2E do administrador pela interface: [IA-PROVEDORES.md](IA-PROVEDORES.md#testes).

## Pendências

- O app (MAUI) ainda não tem a tela: deve consultar `availability` e chamar o `POST` com a mesma autorização.
- O limite por hora é em memória: com mais de uma instância da aplicação cada uma conta por conta própria.
- Aplicar uma sugestão direto no formulário de edição (hoje a pessoa copia o texto).
