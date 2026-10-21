# ux-mobile-preview — o app (MAUI Blazor) no navegador

Harness isolado (fora da solução e da esteira): compila os mesmos componentes Razor de `src/Monburan.Mobile/Components`
(por link, sem cópia) num host Blazor Server e serve o `wwwroot` do app. A API é a do site — em geral o host InMemory do
`tests/ux-smoke`. Serve para revisão visual e captura sem o workload MAUI.

```bash
dotnet build
dotnet run -- --api http://127.0.0.1:5095/ --urls http://127.0.0.1:5096
```

- APIs de plataforma são substituídas em `Preview/MauiStubs.cs` (tema nativo sem efeito, seletor de arquivo sempre
  cancelado); a sessão fica em memória (`Preview/MemoryTokenStore.cs`), uma pessoa por processo.
- O que só o aparelho valida: barras do sistema, WKWebView (iOS), gestos, teclado nativo, envio de PDF.
