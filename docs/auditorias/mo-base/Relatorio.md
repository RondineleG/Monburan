# MO-BASE — primeira fatia M05

Base do programa: `775373cef3d7e957a153b71119a861c8290e9619` (`origin/main`). Branch de trabalho: `devin/mo-base`, criada de `origin/dev` `b83f4c099f640d00d68f420607b20a5eabd9402f` conforme a regra do Monburan; PR contra `dev`.

## Escopo executado

Fatia pequena de M05 (candidatura e ATS): ao candidatar-se com currículo, a aplicação passa a persistir uma fotografia serializada do currículo no momento da candidatura. O `ResumeId` continua inalterado para compatibilidade, enquanto `ResumeSnapshot` expõe o conteúdo capturado sem depender de edições posteriores do currículo.

Também foi necessário carregar formação, experiências e idiomas no repositório de currículos para que o snapshot não fique incompleto, e ajustar o teste de migration histórica para inserir a candidatura antiga por SQL, pois o modelo atual agora conhece uma coluna inexistente no ponto intermediário da migration.

## Validação

Comandos com `export PATH="$HOME/.dotnet:$PATH"`:

```bash
dotnet build src/Monburan.Web/Monburan.Web.csproj --configuration Release -v:q
# 0 warnings, 0 errors

dotnet test tests/Monburan.UnitTests/Monburan.UnitTests.csproj --configuration Release -v:q
# 1317 passed, 0 failed

dotnet test tests/Monburan.IntegrationTests/Monburan.IntegrationTests.csproj --configuration Release -v:q
# 64 passed, 0 failed

dotnet test tests/Monburan.ApiTests/Monburan.ApiTests.csproj --configuration Release -v:q
# 277 passed, 0 failed
```

A solução completa não é buildável nesta VM porque `Monburan.Mobile` exige workload `maui-android`; não foi instalado workload novo nem usado serviço externo.

## Compatibilidade e rollback

- Compatibilidade: nenhum campo existente mudou de tipo ou significado; `ResumeSnapshot` é aditivo e clientes antigos podem ignorá-lo.
- Migração: `SnapshotImutavelCurriculoCandidatura` adiciona `Applications.ResumeSnapshotJson` (`jsonb`, nullable). Dados antigos permanecem com snapshot nulo.
- Rollback: remover a migration e o uso do snapshot; `ResumeId` permanece como o caminho anterior.
- Fora de escopo: migração Recruiva, Google for Jobs, integração HiMarshall/M09 e aplicativo Android.
