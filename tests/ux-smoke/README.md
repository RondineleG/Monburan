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
