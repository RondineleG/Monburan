# Sugestões de melhoria de currículo com IA

A candidata ou o candidato abre o próprio currículo, autoriza o envio e recebe sugestões: resumo profissional
reescrito, frases de destaque por experiência, habilidades que valem destacar, o que falta e dicas gerais. **Nada é
alterado nem guardado**: o resultado só existe na tela (e na resposta da API), e a pessoa decide o que aproveitar.

A aplicação não depende de um provedor específico. Ela fala o protocolo de **"chat completions" da OpenAI**
(`POST {BaseUrl}/chat/completions`), então qualquer serviço compatível serve (OpenAI, gateways, servidores locais
como o Ollama). Hoje o ambiente aponta para o **Xiaomi MiMo** (`mimo-v2.6-pro`).

## Como funciona

1. A tela do currículo mostra o painel **só quando a IA está configurada** (URL, chave e modelo). Sem isso nada
   aparece no site nem no app.
2. A pessoa marca a autorização expressa de envio e pede as sugestões. A API exige o mesmo
   (`acceptsExternalProcessing: true` no corpo), então nenhum cliente envia sem autorização.
3. O caso de uso (`ImproveResumeUseCase`) confere que o currículo é da própria pessoa, aplica o limite de usos e
   monta a conversa. Sai **só conteúdo profissional**: título, resumo, experiências, formação, habilidades e idiomas.
   Não vão nome, e-mail, telefone nem endereço, e e-mails, telefones, CPF/CNPJ, CEP e links digitados em campos
   livres são trocados por marcadores (`[e-mail]`, `[telefone]`, `[documento]`, `[cep]`, `[link]`). O texto é
   limitado em tamanho.
4. O cliente (`OpenAiCompatibleClient`) chama o provedor, tira o raciocínio interno do modelo da resposta e devolve
   o texto. O caso de uso lê o JSON com tolerância (aceita cercas de código, texto em volta, `snake_case`) e limita
   o tamanho de cada item.
5. As falhas viram mensagens seguras (tempo esgotado, serviço ocupado, "não foi possível gerar agora"). O detalhe do
   provedor (status e um trecho curto do erro) vai só para o log, nunca a chave nem o texto do currículo.

O modelo recebe a instrução de **não inventar** empresas, cargos, datas, formações nem números: quando uma melhoria
pede um resultado que só a pessoa conhece, ele usa um marcador como `[X%]`. Mesmo assim, a tela avisa que as
sugestões são um ponto de partida e que só vale usar o que for verdadeiro e comprovável.

## Configuração

Seção `Ai` (valores padrão sem segredo em `appsettings.json`):

| Chave | Padrão | Para quê |
|---|---|---|
| `Ai:BaseUrl` | `https://token-plan-sgp.xiaomimimo.com/v1` | Raiz da API com o caminho de versão. O cliente acrescenta `/chat/completions`. Exige `https` (só endereço local aceita `http`). |
| `Ai:Model` | `mimo-v2.6-pro` | Modelo usado. |
| `Ai:ProviderName` | `Xiaomi MiMo` | Nome mostrado à pessoa como destino do conteúdo (vazio usa o endereço do servidor). |
| `Ai:ApiKey` | vazio | **Segredo.** Sem ela o recurso fica desligado. |
| `Ai:TimeoutSeconds` | `90` | Tempo máximo de uma chamada (entre 5 e 300). Modelos de raciocínio levam dezenas de segundos. |
| `Ai:MaxOutputTokens` | `4000` | Limite da resposta. Em modelos de raciocínio o raciocínio também conta: não reduza demais. |
| `Ai:Temperature` | `0.4` | Criatividade (0 a 2). |
| `Ai:UseJsonResponseFormat` | `true` | Pede `response_format: json_object`. Desligue se o servidor recusar o campo. |
| `Ai:MaxRequestsPerHour` | `5` | Usos por pessoa por hora (cada chamada tem custo). |

**A chave nunca vai para o repositório.**

- Desenvolvimento: `dotnet user-secrets set "Ai:ApiKey" "<chave>" --project src/Monburan.Web`.
- Produção (VPS): `AI_API_KEY=<chave>` no `.env` de `/opt/monburan` (permissão `600`) e
  `docker compose up -d app`. O compose repassa `AI_BASE_URL`, `AI_MODEL`, `AI_PROVIDER_NAME` e `AI_API_KEY`
  (ver `infra/vps/env.example`). Basta a chave para ativar.
- Para trocar de provedor, mude `AI_BASE_URL`, `AI_MODEL` e `AI_PROVIDER_NAME` (e a chave). Não há mudança de código.

O mesmo provedor oferece também um endereço compatível com o protocolo da Anthropic (`.../anthropic`). A aplicação
usa o protocolo da OpenAI; aceitar o outro exigiria um segundo cliente.

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
  currículo, limite, falhas), cliente com handler falso, limitador e opções.
- API: o cliente de IA é trocado por um falso (os testes nunca falam com um provedor de verdade, mesmo com segredo
  de usuário na máquina). Cobrem autorização, dono, limite por hora e falhas.
- Cliente em HTTP de verdade contra um servidor local que imita o protocolo (cabeçalhos, JSON e acentos reais,
  status de erro e tempo esgotado).

## Pendências

- O app (MAUI) ainda não tem a tela: deve consultar `availability` e chamar o `POST` com a mesma autorização.
- O limite por hora é em memória: com mais de uma instância da aplicação cada uma conta por conta própria.
- Aplicar uma sugestão direto no formulário de edição (hoje a pessoa copia o texto).
