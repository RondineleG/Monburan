# ux-smoke — validação de UX com navegador real

Harness isolado (não faz parte da solução nem da esteira): sobe o site com o
`MonburanApiFactory` (EF InMemory + FakeAiTextClient, sem banco compartilhado e sem
provedor de IA real) via `UseKestrel(0)` numa porta loopback aleatória e roda um
fluxo Playwright: login do candidato seedado, geração de sugestões, rascunho
restaurado, revisão/edição/aplicação, CV em PDF, permissões do recrutador, perfil
(AddressId) e barreira de candidatura duplicada.

## Rodar (offline, cache NuGet local)

```bash
cd tests/ux-smoke
dotnet restore --ignore-failed-sources
dotnet run
```

Sai com código não-zero na primeira falha (asserts lançam exceção). `dotnet run -- --serve`
mantém o host vivo para inspeção no endereço `HOST=` impresso no log.

- Navegador restrito a loopback (`127.0.0.1`/`localhost`) — nenhuma chamada externa.
- Requer SDK .NET 10 e os navegadores Playwright já instalados no perfil do usuário.
- Seed InMemory: cria currículo/assinatura/candidatura só no banco em memória do teste.

## Captura para revisão visual e regressão (`--capture`)

Percorre, somente leitura, todas as rotas do site por papel (visitante, candidato, empresa, admin) em
1440×900 e 390×844 nos dois temas, 834×1112 só no claro, e as telas do app pela pré-visualização (no claro só 390×844; no escuro 360×800, 390×844 e 412×915)
(`tests/ux-mobile-preview`). Grava as capturas, `results.json` (métricas por página: contraste de cada texto, texto
< 12px, alvos < 24px, campos sem rótulo, controles sem nome, ids duplicados, overflow, títulos, erros de console) e
`summary.txt`. Diálogos e gavetas são só abertos e fechados; nada é confirmado.

```bash
cd tests/ux-mobile-preview && dotnet build            # só para incluir o app
cd ../ux-smoke && dotnet run -- --capture all --out <pasta>   # all | web | mobile
cd ../ux-smoke && dotnet run -- --serve-only              # sobe o host sem rodar o fluxo
```

O harness não reprova sozinho: grava as métricas para revisão manual. Ao revisar o `summary.txt`, confira overflow, contraste abaixo de AA, texto < 12px, campo sem rótulo e controle sem nome.

## Notificações (`--notifications`)

`dotnet run -- --notifications` usa dados isolados e mensagens longas para validar a tela em 320, 390, 834 e 1440px,
nos dois temas. Também verifica marcação pelo teclado, bloqueio de ações simultâneas, preservação da lista durante
recarga, falha e recuperação da consulta e persistência da leitura. Capturas e métricas ficam em
`%TEMP%/opencode/monburan-notifications`; qualquer falha de métrica ou interação encerra com erro.
