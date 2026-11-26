# MO-MIGRAR — fatia inicial de dry-run Recruiva

Base: `origin/dev` do Monburan e `main` `92c583255c680560a6e4144bfa7545d8e257d9ca` do Recruiva. Escopo desta fatia: fundação somente leitura do ensaio, ainda sem corte de dados.

## Entregue

- Dry-run determinístico no Monburan com versão `recruiva-map-v1`, contagens por entidade, hashes SHA-256 por coleção, mapa reproduzível origem→destino e bloqueios por colisão, órfão, arquivo inválido/ausente e permissão.
- Executor de linha de comando `tools/Monburan.RecruivaMigration`, com fixture sintética válida e fixture bloqueada.
- Exportador somente leitura no Recruiva (`tools/Recruiva.MigrationExport`, PR/branch separados) para tenant, identidades, vagas, currículos, candidaturas, transições e logos referenciadas com hash/tamanho.
- Arquivos de empresa (`advertiser`) aceitos como anexos migráveis; arquivos sem dono ou sem hash/tamanho bloqueiam confirmação.

## Validação

```bash
dotnet test tests/Monburan.UnitTests/Monburan.UnitTests.csproj --configuration Release --filter 'FullyQualifiedName~RecruivaMigrationDryRunUseCaseTests' -v:q
# 4 passed, 0 failed

dotnet run --project tools/Monburan.RecruivaMigration/Monburan.RecruivaMigration.csproj --configuration Release -- --input fixtures/mo-migrar/recruiva-export.valid.json --output /tmp/recruiva-report.json
# exit 0; dry-run reconciliado: 7 registros, versão recruiva-map-v1

dotnet run --project tools/Monburan.RecruivaMigration/Monburan.RecruivaMigration.csproj --configuration Release --no-build -- --input fixtures/mo-migrar/recruiva-export.invalid.json --output /tmp/recruiva-report-invalid.json
# exit 2; dry-run bloqueado: 4 problemas; origem e destino não alterados
```

Ensaio PostgreSQL do corte reversível:

```bash
dotnet test tests/Monburan.IntegrationTests/Monburan.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~RecruivaMigrationApplierPostgresTests -v:q
# 1 passed, 0 failed; aplicação e rollback voltaram ao estado anterior
```

No Recruiva, com SDK 10.0.300 porque a VM não tem o SDK 10.0.401 exigido pelo `global.json` (arquivo restaurado sem alteração):

```bash
dotnet test tests/Recruiva.IntegrationTests/Recruiva.IntegrationTests.csproj --configuration Release --filter 'FullyQualifiedName~RecruivaMigrationExporterTests' -v:q
# 2 passed, 0 failed
# warning NU1903 pré-existente em Microsoft.AspNetCore.Components.WebAssembly.Server 10.0.13
```

## Limites desta fatia

- O corte reversível agora tem ensaio PostgreSQL descartável coberto por teste: aplica 6 registros, copia arquivo com hash e rollback remove os 6 registros e o arquivo copiado. Contas de login/Identity ainda não fazem parte do recorte.
- O exportador não adivinha arquivos órfãos em disco: só exporta arquivos referenciados por entidades. Currículos enviados sem referência persistente no banco do Recruiva precisarão de decisão/mapeamento adicional.
- Nenhum dado real ou produção foi usado.
