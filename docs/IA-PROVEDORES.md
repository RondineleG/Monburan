# Provedores de IA (tela do administrador)

Quem administra o Monburan cadastra, pela tela **Administração → Provedores de IA** (`/admin/provedores-ia`), os
serviços de IA que a aplicação pode usar, escolhe qual é o **padrão** e diz em qual **variável de ambiente** está a
chave de cada um. Trocar de provedor, de modelo ou de endereço não exige deploy nem mudança de código.

Os recursos de IA (hoje, as [sugestões de melhoria de currículo](IA-CURRICULO.md)) usam sempre o provedor padrão. Sem
provedor padrão ativo, ou sem a chave dele no servidor, os recursos de IA ficam desligados e nada aparece no site nem no app.

## O que fica onde

| O quê | Onde | Quem mexe |
|---|---|---|
| Endereço, modelo, protocolo, nome mostrado, tempo limite, tokens, qual é o padrão | **Banco** (tabela `AiProviders`) | Administrador, pela tela |
| **Nome** da variável da chave (`AI_KEY_...`) | Banco | Administrador, pela tela |
| **A chave** | Ambiente do servidor: `ai-keys.env` na VPS, segredos de usuário em desenvolvimento | Quem opera o servidor |
| Limite de usos por pessoa, permissão de rede interna, intervalo de releitura | `appsettings.json`, seção `Ai` | Código (PR) |

A chave **nunca** é digitada na tela, nunca vai para o banco, nunca aparece nos logs e nunca volta para o navegador: a tela
só mostra se a variável existe no servidor ("definida no servidor" ou "não definida neste servidor").

## Quem pode abrir a tela

Só o papel **Admin** (política `AdminOnly`); candidatos e empresas recebem acesso negado e visitantes vão para o login.

- **Desenvolvimento:** o seed cria `admin@monburan.com.br` e `adm@monburan.com.br` (senhas de desenvolvimento do próprio seed).
- **Produção:** não existe usuário administrador pronto. Defina `ADMIN_EMAILS` no `.env` da VPS (e-mails separados por
  vírgula), cadastre-se no site com um desses e-mails e **confirme o e-mail** pelo link recebido. A conta só vira administradora
  com o e-mail confirmado: sem isso, bastaria cadastrar o e-mail de outra pessoa. Tirar o e-mail da lista tira o acesso no
  próximo login (ou na próxima revalidação da sessão). Se a VPS não envia e-mail (`SMTP_PASSWORD` vazio), o link de
  confirmação não chega; nesse caso confirme uma vez direto no banco:
  `UPDATE "Identity"."Users" SET "EmailConfirmed" = true WHERE "NormalizedEmail" = 'SEU@EMAIL.COM';`
  (o e-mail normalizado é em maiúsculas)
- **2FA obrigatório:** administradores só abrem as telas de administração com a verificação em duas etapas ligada na conta;
  sem ela, a tela explica e leva à configuração. Detalhes e a saída de emergência em [SEGURANCA.md](SEGURANCA.md#administração-do-site).

## A tela

Cada provedor é um cartão com o nome, o modelo, o servidor, a variável da chave e os botões:

| Botão | O que faz |
|---|---|
| **Testar** | Faz uma chamada mínima (`Responda somente com a palavra: ok`) ao provedor, ativo ou não, e explica o resultado em português. Custa poucos tokens e não conta no limite das pessoas. |
| **Editar** | Abre o formulário com os valores atuais. |
| **Tornar padrão** | Passa os recursos de IA para este provedor (precisa estar ativo). Vale na hora, sem reiniciar. |
| **Ativar / Desativar** | Liga ou desliga o provedor. O padrão não pode ser desativado. |
| **Excluir** | Remove o provedor da lista (a variável no servidor não é tocada). |

Regras: o nome é único; o provedor padrão precisa estar ativo; o padrão só pode ser desativado ou excluído depois de
escolher outro (a não ser que seja o único, quando a IA fica desligada); o primeiro provedor ativo cadastrado vira o padrão
sozinho.

### Campos do formulário

| Campo | Para quê |
|---|---|
| **Começar de um modelo** | Preenche protocolo, endereço, modelo e variável de um serviço conhecido (lista abaixo). |
| **Nome** | O que a pessoa vê como destino do conteúdo, na tela de autorização e na Política de Privacidade. Deve ser o nome real do serviço. |
| **Protocolo** | *Compatível com OpenAI* (`POST {endereço}/chat/completions`, chave em `Authorization: Bearer`) ou *Anthropic* (`POST {endereço}/v1/messages`, chave em `x-api-key`). |
| **Endereço (URL)** | Raiz da API, sem o caminho final. Exige `https` (só o próprio computador aceita `http`) e não pode ter usuário, `?` nem `#`. |
| **Modelo** | O nome do modelo como o serviço o conhece. |
| **Variável de ambiente da chave** | Só o nome, depois do `AI_KEY_` fixo (ex.: `OPENROUTER` vira `AI_KEY_OPENROUTER`). |
| **Ativo / padrão** | Se o provedor pode ser usado e se é o padrão. |
| **Ajustes avançados** | Tempo limite (5 a 300 s), limite de tokens da resposta (modelos de raciocínio gastam tokens pensando: deixe folga), temperatura, e opções de compatibilidade (enviar ou não a temperatura, pedir `response_format: json_object`, usar `max_completion_tokens`). |

### Modelos prontos

| Modelo | Protocolo | Endereço | Variável | Observação |
|---|---|---|---|---|
| Xiaomi MiMo | OpenAI | `https://token-plan-sgp.xiaomimimo.com/v1` | `AI_KEY_XIAOMI` | `mimo-v2.6-pro`, modelo de raciocínio. Já cadastrado, ativo e padrão. |
| Xiaomi MiMo (Anthropic) | Anthropic | `https://token-plan-sgp.xiaomimimo.com/anthropic` | `AI_KEY_XIAOMI` | Mesmo serviço e mesma chave. |
| OpenRouter | OpenAI | `https://openrouter.ai/api/v1` | `AI_KEY_OPENROUTER` | Um endereço para centenas de modelos. Modelo no formato `fabricante/modelo` (ex.: `deepseek/deepseek-chat`, `anthropic/claude-haiku-5.5`). |
| DeepSeek | OpenAI | `https://api.deepseek.com` | `AI_KEY_DEEPSEEK` | `deepseek-chat`; `deepseek-reasoner` para o de raciocínio. |
| Anthropic (Claude) | Anthropic | `https://api.anthropic.com` | `AI_KEY_ANTHROPIC` | `claude-haiku-5-5` (rápido e barato); também `claude-sonnet-5-5` e `claude-opus-5-5`. |
| OpenAI | OpenAI | `https://api.openai.com/v1` | `AI_KEY_OPENAI` | Informe o modelo. Os novos pedem a opção `max_completion_tokens` e podem recusar a temperatura (desligue "enviar a temperatura"). |
| Outro (compatível com OpenAI) | OpenAI | o seu | `AI_KEY_...` | Qualquer serviço com `/chat/completions`: gateways, LM Studio, Ollama... |

Na primeira execução, com a tabela vazia, a aplicação cadastra Xiaomi (ativo e padrão) e, desativados, Xiaomi pelo protocolo
Anthropic, OpenRouter, DeepSeek e Anthropic. Depois disso a tela manda em tudo: nada é recriado nem sobrescrito.

## Passo a passo: adicionar um provedor

1. **Guarde a chave no servidor** (a chave nunca vai pela tela):
   - *Desenvolvimento:* `dotnet user-secrets set "AI_KEY_OPENROUTER" "<chave>" --project src/Monburan.Web`
   - *VPS:* acrescente `AI_KEY_OPENROUTER=<chave>` ao `ai-keys.env` de `/opt/monburan` (modelo em
     `infra/vps/ai-keys.env.example`; permissão `600`) e rode `docker compose up -d --force-recreate app` para o contêiner
     ler a variável nova.
2. Abra **Provedores de IA → Adicionar provedor**, escolha o modelo pronto, confira o modelo (por exemplo,
   `deepseek/deepseek-chat`) e salve. O cartão deve mostrar "definida no servidor".
3. Clique em **Testar**. Deu certo: **Ativar** (se estiver desativado) e, se quiser, **Tornar padrão**.

Trocar a chave de um provedor é editar o `ai-keys.env` (ou o segredo de usuário) e recriar o contêiner; nada muda na tela.
Excluir um provedor não apaga a variável do servidor: remova a linha do `ai-keys.env` se a chave não for mais usada.

Em desenvolvimento, para testar um servidor na própria máquina (Ollama em `http://localhost:11434/v1`), defina qualquer
valor na variável (o Ollama ignora a chave, mas a aplicação exige que ela exista). Isso só funciona em desenvolvimento: em
produção o endereço local é bloqueado (ver abaixo).

## Segurança

- **Só o administrador** abre a tela e altera provedores. Cada alteração e cada teste vai para o log com a identidade de
  quem fez (nunca a chave).
- **A chave fica fora do banco e da tela.** O banco guarda só o nome da variável. A aplicação só lê variáveis `AI_KEY_*`
  (letras maiúsculas, números e sublinhado): sem essa trava, um administrador (ou uma conta de administrador invadida)
  poderia apontar um provedor para `Jwt__SigningKey` ou para a senha do banco e mandar esse segredo para um endereço escolhido.
- **Proteção contra SSRF.** O endereço de um provedor é uma entrada de quem administra, então o servidor nunca pode ser
  levado a acessar a rede interna: a conexão resolve o DNS e **recusa se qualquer endereço for interno ou reservado**
  (loopback, redes privadas, CGNAT, *link-local* com o endereço de metadados da nuvem `169.254.169.254`, IPv6 local e de
  transição); não segue redirecionamentos (a chave não pode ser levada a outro endereço); limita a resposta a 2 MB.
  `Ai:AllowPrivateNetworks` libera a rede interna **só em desenvolvimento** (`appsettings.Development.json`); em produção
  fica `false` e a tela não consegue mudar isso.
- **https obrigatório** (a chave não viaja sem criptografia); o `http` só vale para o próprio computador, em desenvolvimento.
- **Privacidade.** O nome do provedor padrão é o destino mostrado à pessoa antes de ela autorizar o envio. A autorização
  expressa a cada uso, o conteúdo mínimo e a Política de Privacidade seguem como descritos em
  [IA-CURRICULO.md](IA-CURRICULO.md). Ao cadastrar um provedor novo, confira os termos dele sobre uso e retenção dos dados.

## Quando o teste não funciona

| Mensagem do teste | O que conferir |
|---|---|
| "A variável AI_KEY_... não está definida neste servidor" | A chave não chegou ao app: confira o `ai-keys.env` e se o contêiner foi recriado (`--force-recreate`); em desenvolvimento, `dotnet user-secrets list`. |
| "O provedor recusou a chave (401/403)" | Valor da chave errado, expirado ou sem permissão para o modelo. |
| "O provedor limitou as chamadas (429)" | Cota ou plano esgotado; tente de novo ou confira o plano no serviço. |
| "O provedor falhou: ... respondeu 402/404/400" | 402: sem crédito. 404: endereço ou modelo errado. 400: parâmetro recusado; ajuste os ajustes avançados (temperatura, `response_format`, `max_completion_tokens`). |
| "A resposta não veio no formato esperado" | Protocolo ou endereço trocados (ex.: protocolo OpenAI apontando para um endereço Anthropic). |
| "A resposta foi cortada pelo limite de tokens" | Aumente o limite de tokens do provedor (modelos de raciocínio precisam de folga). |
| "O provedor não respondeu a tempo" | Endereço errado ou serviço lento; aumente o tempo limite. |
| "O endereço aponta para uma rede interna ou reservada" | Bloqueado de propósito em produção (ver Segurança). |

## Várias instâncias

Cada instância guarda os provedores em memória e relê o banco a cada `Ai:CatalogRefreshSeconds` (60 s por padrão), além de
reler na hora quando a alteração foi feita nela. Com mais de uma instância, uma mudança feita em uma chega às demais em
até esse intervalo.

## Testes

- **Unitários:** protocolos OpenAI e Anthropic (carga, cabeçalhos, leitura da resposta, falhas, o que vai e o que nunca vai
  para o log), classificação de endereços (SSRF), leitura das chaves e a lista de nomes permitidos, cliente e catálogo,
  validação do cadastro e os casos de uso (salvar, padrão, excluir, testar, listar, semente inicial).
- **API:** os dois protocolos em HTTP de verdade contra um servidor local pela mesma pilha da produção (sem
  redirecionamento, teto de resposta), o bloqueio de rede interna com a configuração de produção, a semente na
  inicialização, o acesso à tela e a regra de administrador por e-mail confirmado.
- **Integração:** o repositório no EF (um só padrão, exclusão lógica, nome sem diferenciar maiúsculas).
- **E2E:** o fluxo completo do administrador pela interface (cadastrar, testar, tornar padrão, editar, desativar e excluir)
  contra um servidor de IA falso, e o acesso negado para candidato, empresa e visitante. Nenhum teste fala com um serviço de
  verdade; a chave usada no E2E é um valor falso de `appsettings.Development.json`.
