# MO-BASE — fatias M04/M05 implementadas

Base do programa: `775373cef3d7e957a153b71119a861c8290e9619` (`origin/main`). Branch de trabalho: `devin/mo-base`, criada de `origin/dev` `b83f4c099f640d00d68f420607b20a5eabd9402f` conforme a regra do Monburan; PR contra `dev`.

## Escopo executado

Fatia pequena de M05 (candidatura e ATS): ao candidatar-se com currículo, a aplicação passa a persistir uma fotografia serializada do currículo no momento da candidatura. O `ResumeId` continua inalterado para compatibilidade, enquanto `ResumeSnapshot` expõe o conteúdo capturado sem depender de edições posteriores do currículo. A captura fica atrás da flag `snapshot-curriculo-candidatura`, desligada até ser cadastrada/liberada em `/admin/funcionalidades`; a gaveta do recrutador usa a fotografia e o currículo ao vivo só permanece como fallback de candidaturas antigas.

Também foi necessário carregar formação, experiências e idiomas no repositório de currículos para que o snapshot não fique incompleto, e ajustar o teste de migration histórica para inserir a candidatura antiga por SQL, pois o modelo atual agora conhece uma coluna inexistente no ponto intermediário da migration.

Segunda fatia: candidatura passa a ser idempotente pela chave natural `CandidateId + JobId`, inclusive quando o currículo anexado foi removido depois da candidatura original. Repetir o mesmo payload (`ResumeId` e `Notes`) recupera a candidatura existente sem incrementar contador, notificar ou gravar histórico novo; repetir a mesma chave com payload divergente retorna 409. Vaga inativa também retorna 409. O ATS passa a rejeitar transições regressivas/terminais inválidas com 409 sem histórico novo, e repetição do mesmo status é no-op idempotente, sem update, notificação ou histórico duplicado. Cada transição grava versão sequencial e identidade autenticada do executor; `Applications.Version` usa concorrência otimista para impedir que duas transições lidas da mesma versão se sobrescrevam. A gaveta do recrutador só oferece ações compatíveis com a transição vigente.

Terceira fatia, de M04: a recomendação passa a calcular não só a pontuação, mas os fatores objetivos que a compõem (cargo, categoria, localização, modalidade, salário e habilidades encontradas). Os fatores só são expostos quando a flag `recomendacao-explicavel` está ligada para o grupo do usuário; desligada, o contrato permanece sem os campos novos. Quando não há perfil nem habilidades úteis, o endpoint usa ranking básico determinístico por recência em vez de depender de fallback implícito da interface. Empates de pontuação usam data de criação e id como desempate estável.

Quarta fatia, de M06: alertas de vagas passam a carregar `JobAlertId` e `JobId` na notificação, com índice único parcial para deduplicar regra/vaga mesmo sob repetição ou concorrência. A avaliação consulta a deduplicação antes de criar `NewJob`; vaga inativa/expirada continua sem gerar alerta.

Quinta fatia, de M02: o ciclo de vida do anunciante ganha `Closed` como encerramento terminal, separado de pausa, expiração e exclusão. Vaga ativa, pausada ou expirada pode ser encerrada; encerrada sai da busca e não pode ser reativada. A tela Minhas vagas passa a ter aba de encerradas e ação confirmada de encerramento.

Sexta fatia, ainda de M02: criação, edição e mudança de situação passam a carimbar autoria do anunciante em `CreatedBy`/`UpdatedBy`. O encerramento ficou atrás da flag `encerramento-vaga`, a administração não reabre vaga encerrada e uma edição concorrente não desfaz o encerramento no repositório.

Sétima fatia, de M02: a vaga ganha `Draft` atrás da flag `rascunho-vaga`. Rascunho não consome vaga ativa nem dispara alertas; a publicação posterior passa pelo limite do plano e então avalia alertas. Minhas vagas ganha aba de rascunhos e ação de publicação, e a API recebe `PUT /jobs/{id}/status` para o mesmo ciclo no mobile. O repositório protege encerramento e moderação em atualização condicional no PostgreSQL, preservando autoria e evitando confirmação falsa quando a moderação vence a corrida. A publicação passou a ter data própria (`PublishedAt`), edição e mudança de situação foram separadas para não se sobrescreverem, rascunho só é publicado pelo dono com rollout, e a API de status tem rollout próprio. Criação, edição, mudança de situação e moderação gravam revisões imutáveis com snapshot jsonb e autoria traduzida por papel na API e na tela de edição, ambas protegidas por `historico-vaga` com checagem também no caso de uso.

## Validação

Comandos com `export PATH="$HOME/.dotnet:$PATH"`:

```bash
dotnet build src/Monburan.Web/Monburan.Web.csproj --configuration Release -v:q
# 0 warnings, 0 errors

dotnet test tests/Monburan.UnitTests/Monburan.UnitTests.csproj --configuration Release -v:q
# 1348 passed, 0 failed

dotnet test tests/Monburan.IntegrationTests/Monburan.IntegrationTests.csproj --configuration Release -v:q
# 71 passed, 0 failed

dotnet test tests/Monburan.ApiTests/Monburan.ApiTests.csproj --configuration Release -v:q
# 281 passed, 0 failed; inclui round-trip API com flag ligada, reenvio idempotente, transição inválida sem histórico e recomendação explicável
```

A solução completa não é buildável nesta VM porque `Monburan.Mobile` exige workload `maui-android`; não foi instalado workload novo nem usado serviço externo.


Validação dirigida da segunda revisão:

```bash
dotnet test tests/Monburan.UnitTests/Monburan.UnitTests.csproj --configuration Release --filter 'FullyQualifiedName~CreateApplicationUseCaseTests|FullyQualifiedName~UpdateApplicationStatusUseCaseTests' -v:q
# 17 passed, 0 failed

dotnet test tests/Monburan.IntegrationTests/Monburan.IntegrationTests.csproj --configuration Release --filter 'FullyQualifiedName~ApplicationRepositoryPostgresTests' -v:q
# 7 passed, 0 failed; inclui concorrência otimista da transição

dotnet test tests/Monburan.ApiTests/Monburan.ApiTests.csproj --configuration Release --filter 'FullyQualifiedName~ApplicationsApiTests|FullyQualifiedName~ApplicationHistoryApiTests' -v:q
# 14 passed, 0 failed
```

## Compatibilidade e rollback

- Compatibilidade: nenhum campo existente mudou de tipo ou significado; `ResumeSnapshot` é aditivo e clientes antigos podem ignorá-lo. `POST /applications` passa a responder 200 somente quando a repetição idempotente recupera exatamente a mesma candidatura; payload divergente continua erro (409).
- Migração: `SnapshotImutavelCurriculoCandidatura` adiciona `Applications.ResumeSnapshotJson` (`jsonb`, nullable). Dados antigos permanecem com snapshot nulo.
- Rollback: desligar `snapshot-curriculo-candidatura` interrompe novas capturas; remover a migration e o uso do snapshot restaura o caminho anterior. `ResumeId` permanece como compatibilidade. Para a segunda fatia, reverter este commit restaura o comportamento anterior de duplicidade/ATS. Para M04, desligar `recomendacao-explicavel` oculta score/fatores sem alterar a ordenação existente.
- Fora de escopo: migração Recruiva, Google for Jobs, integração HiMarshall/M09 e aplicativo Android.
