# ux-mobile-preview — o app (MAUI Blazor) no navegador

Harness isolado (fora da solução): compila os mesmos componentes Razor de `src/Monburan.Mobile/Components`
(por link, sem cópia) num host Blazor Server e serve o `wwwroot` do app. A API é a do site — em geral o host InMemory do
`tests/ux-smoke`. Serve para revisão visual e captura sem o workload MAUI.

```bash
dotnet build
dotnet run -- --api http://127.0.0.1:5095/ --urls http://127.0.0.1:5096
```

- APIs de plataforma são substituídas em `Preview/MauiStubs.cs` (tema nativo sem efeito, seletor de arquivo sempre
  cancelado); a sessão fica em memória (`Preview/MemoryTokenStore.cs`), uma pessoa por processo.
- O que só o aparelho valida: barras do sistema, WKWebView (iOS), gestos, teclado nativo, envio de PDF.

A esteira executa a verificação dos canais externos sem servidor, navegador nem emulador:

```bash
dotnet run --configuration Release -- --verify-external-applications
```

São 12 cenários com as telas reais: site, WhatsApp, e-mail e presencial, no detalhe com e sem sessão e no acesso direto
à candidatura com sessão. O HTTP falso recusa chamadas para carregar currículos ou enviar candidaturas internas.
