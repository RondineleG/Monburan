# MO-BASE — fatias M05 implementadas

Base do programa: `775373cef3d7e957a153b71119a861c8290e9619` (`origin/main`). Branch de trabalho: `devin/mo-base`, criada de `origin/dev` `b83f4c099f640d00d68f420607b20a5eabd9402f` conforme a regra do Monburan; PR contra `dev`.

## Escopo executado

Fatia pequena de M05 (candidatura e ATS): ao candidatar-se com currículo, a aplicação passa a persistir uma fotografia serializada do currículo no momento da candidatura. O `ResumeId` continua inalterado para compatibilidade, enquanto `ResumeSnapshot` expõe o conteúdo capturado sem depender de edições posteriores do currículo. A captura fica atrás da flag `snapshot-curriculo-candidatura`, desligada até ser cadastrada/liberada em `/admin/funcionalidades`; a gaveta do recrutador usa a fotografia e o currículo ao vivo só permanece como fallback de candidaturas antigas.

Também foi necessário carregar formação, experiências e idiomas no repositório de currículos para que o snapshot não fique incompleto, e ajustar o teste de migration histórica para inserir a candidatura antiga por SQL, pois o modelo atual agora conhece uma coluna inexistente no ponto intermediário da migration.

Segunda fatia: candidatura passa a ser idempotente pela chave natural `CandidateId + JobId`. Repetir o mesmo payload (`ResumeId` e `Notes`) recupera a candidatura existente sem incrementar contador, notificar ou gravar histórico novo; repetir a mesma chave com payload divergente retorna 409. Vaga inativa também retorna 409. O ATS passa a rejeitar transições regressivas/terminais inválidas com 409 sem histórico novo, e repetição do mesmo status é no-op idempotente, sem update, notificação ou histórico duplicado.

## Validação

Comandos com `export PATH="$HOME/.dotnet:$PATH"`:

```bash
dotnet build src/Monburan.Web/Monburan.Web.csproj --configuration Release -v:q
# 0 warnings, 0 errors

dotnet test tests/Monburan.UnitTests/Monburan.UnitTests.csproj --configuration Release -v:q
# 1322 passed, 0 failed

dotnet test tests/Monburan.IntegrationTests/Monburan.IntegrationTests.csproj --configuration Release -v:q
# 66 passed, 0 failed

dotnet test tests/Monburan.ApiTests/Monburan.ApiTests.csproj --configuration Release -v:q
# 280 passed, 0 failed; inclui round-trip API com flag ligada, reenvio idempotente e transição inválida sem histórico
```

A solução completa não é buildável nesta VM porque `Monburan.Mobile` exige workload `maui-android`; não foi instalado workload novo nem usado serviço externo.

## Compatibilidade e rollback

- Compatibilidade: nenhum campo existente mudou de tipo ou significado; `ResumeSnapshot` é aditivo e clientes antigos podem ignorá-lo. `POST /applications` passa a responder 200 somente quando a repetição idempotente recupera exatamente a mesma candidatura; payload divergente continua erro (409).
- Migração: `SnapshotImutavelCurriculoCandidatura` adiciona `Applications.ResumeSnapshotJson` (`jsonb`, nullable). Dados antigos permanecem com snapshot nulo.
- Rollback: desligar `snapshot-curriculo-candidatura` interrompe novas capturas; remover a migration e o uso do snapshot restaura o caminho anterior. `ResumeId` permanece como compatibilidade. Para a segunda fatia, reverter este commit restaura o comportamento anterior de duplicidade/ATS.
- Fora de escopo: migração Recruiva, Google for Jobs, integração HiMarshall/M09 e aplicativo Android.
