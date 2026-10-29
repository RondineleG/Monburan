# Plano: banco de dados e cache (Valkey) — PAUSADO

> **Situação (2026-10-29):** análise feita, decisão inclinada a **manter tudo na VPS** por enquanto, trabalho pausado a
> pedido. Esta branch (`infra/banco-gerenciado`) guarda a mudança para PostgreSQL gerenciado pronta, caso a decisão mude.
> Nada daqui foi para a `dev`.

## Contexto

- Produção hoje: PostgreSQL 16 em contêiner **na própria VPS**, junto do app. A VPS fica em **Buffalo, NY (EUA)**.
- Extensão usada: só `pg_trgm` (índices de trigramas da busca de vagas).
- Cache hoje: `IMemoryCache` (checagem de conta ativa e feature flags). Limites de requisição, de sugestões e de uso da IA
  ficam na memória do processo: funcionam com **uma** instância do app.
- O commit desta branch (`43f09fe`) tira o PostgreSQL do compose e passa a ler `DB_CONNECTION` (banco na nuvem). Foi
  escrito pensando no Neon, mas vale para qualquer provedor PostgreSQL.

## Opções avaliadas

### Layerbase (página de preços consultada em 2026-10-29)

| Plano | Preço | Pontos |
|---|---|---|
| Free | US$ 0 | 2 bancos, 5 GB; **dorme após 15 min sem uso**; sem backup agendado; 20 conexões. Serve para dev/homologação. |
| Pro | US$ 15/mês | Até 10 bancos, 25 GB; PostgreSQL **e Valkey** inclusos; backup diário, 30 dias; mTLS no PostgreSQL; conjunto sempre ligado de 1,5 GB RAM / 1 vCPU (banco não fixado dorme após 6 h). |
| Dedicado | a partir de US$ 35/mês | Servidor exclusivo, backup por hora; **único com São Paulo**. |

- Free, Solo e Pro rodam **só em US East (Virgínia)**. Da VPS em Buffalo, isso dá algo como 10–15 ms por consulta:
  aceitável. Para uma VPS no Brasil seria ~130 ms por consulta: inviável.
- A página não menciona recuperação a um instante, lista de IPs permitidos nem SLA.
- Fontes: <https://layerbase.com/pricing>, <https://layerbase.com/blog/managed-databases-in-brazil>.

### Neon

- Cerca de US$ 19–25/mês em produção, só PostgreSQL. O banco de homologação usado antes estava em `sa-east-1` (São Paulo),
  longe da VPS atual.

### Manter na VPS

| | Tudo na VPS | Banco na Layerbase Pro |
|---|---|---|
| Latência por consulta | ~0,1 ms | ~10–15 ms |
| Custo | US$ 0 a mais | US$ 15/mês |
| VPS invadida ou perdida | Banco vai junto: restaurar do backup externo | Banco continua |
| Atualizações do PostgreSQL, disco | Nossa responsabilidade | Do provedor |
| Recursos | Divide CPU/RAM com o app | Separados |

## Decisão provisória

**Manter o banco na VPS**, com a condição de deixar o backup externo funcionando de verdade (itens 🔴 de
`docs/pre-prod.md` §4.4): `BACKUP_REMOTE` com rclone, chave `age` fora da VPS, primeiro backup + timer diário e
`restore-test.sh --latest-remote` executado.

Reavaliar a ida para a Layerbase Pro (ou outro gerenciado) quando: faltar RAM/CPU na VPS, for preciso mais de uma
instância do app, não quisermos mais cuidar do PostgreSQL, ou os dados exigirem recuperação a um instante. Antes de
assinar, medir de dentro da VPS no plano grátis:

```bash
psql "<url do banco>" -c '\timing' -c 'select 1' -c 'select 1' -c 'select 1'   # esperar ~10-20 ms depois da 1ª
```

## Valkey

Com o banco na VPS e **uma** instância do app, o Valkey agrega pouco agora: o `HybridCache` do .NET em memória dá quase
o mesmo ganho. Ele passa a valer com mais de uma instância (cache, limites e invalidação compartilhados) ou para o cache
sobreviver a deploys. O `HybridCache` aceita o Valkey como segunda camada mudando só a configuração.

Quando entrar: **contêiner na VPS**, rede interna do Docker, senha, `maxmemory 128mb`, `allkeys-lru`, sem persistência,
e o app seguindo sem cache se ele cair (`abortConnect=false`). O Valkey da Layerbase também serve se faltar RAM na VPS,
desde que tenha TLS (a página não confirma).

## Próximos passos quando retomar

1. **Fase 0 — medir:** tempo das páginas principais (busca e detalhe de vagas, home, sitemap) e log de consultas acima
   de 200 ms. Só otimizar o que a medição mostrar.
2. **Consultas:** índices e ajustes onde a medição apontar.
3. **Cache em memória (`HybridCache`)** nas leituras públicas: busca de vagas (60 s), detalhe (5 min), sitemap, planos e
   categorias (10–30 min), com invalidação por etiqueta quando uma vaga muda e interruptor `Cache:Enabled`. Nada de dados
   pessoais ou dependentes de permissão no cache.
4. **Valkey** só se houver mais de uma instância ou necessidade de cache entre deploys.

## Connection strings com segurança (vale para qualquer escolha)

1. Nunca no repositório, no `appsettings.json` nem no GitHub: o deploy lê do servidor.
2. Preferir **um arquivo por segredo** (Docker `secrets:` em `/run/secrets`, lido com
   `builder.Configuration.AddKeyPerFile("/run/secrets", optional: true)`) em vez de variável de ambiente, que aparece em
   `docker inspect`. Arquivos com `chmod 600` e, como o contêiner roda sem root, dono UID 1654.
3. Banco remoto: `SSL Mode=VerifyFull` com o certificado da autoridade do provedor (`Root Certificate=...`), não só
   `Require`. No Layerbase Pro, mTLS (`SSL Certificate=` e `SSL Key=`).
4. Usuários de banco separados: dono (migrations), app (só dados) e backup (só leitura). Exige tirar as migrations do
   boot e rodá-las no `deploy.sh`.
5. Valkey remoto só com TLS (`rediss://`) e senha.
6. Trocar as credenciais que circularam em conversa: senha do banco de homologação do Neon e do FTP do GearHost.

## O que há nesta branch

- `43f09fe`: produção com PostgreSQL gerenciado (`DB_CONNECTION`), backup com `pg_dump` em contêiner descartável
  apontando para a nuvem, `restore-test.sh` ajustado e `.gitattributes` forçando LF em `.sh` e `.example`.
- O `.gitattributes` vale independente do banco: se a decisão for manter na VPS, vale levar só ele para a `dev`.
