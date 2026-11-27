# Ensaio cruzado Recruiva → Monburan — MO-MIGRAR

Data UTC: 2026-11-26
Ambiente: PostgreSQL descartável em contêiner local; sem dados reais nem produção.

## Resultado

| Etapa | Resultado |
|---|---:|
| Exportação Recruiva real | 8 registros: 1 usuário, 1 candidato, 1 anunciante, 1 vaga, 1 currículo, 1 arquivo, 1 candidatura e 1 transição |
| Dry-run Monburan | 8 registros reconciliados; 0 problemas; mapa `recruiva-map-v1` |
| Apply Monburan | lote `e721d7d6-0ce6-417a-8e07-c96c8d4de644`; 7 registros persistidos e 1 arquivo copiado; 0 problemas |
| Estado após corte | 6 linhas de domínio legadas importadas nas tabelas Monburan, além do usuário Identity |
| Rollback | 7 registros persistidos removidos e 1 arquivo removido; 0 problemas |
| Estado após rollback | 0 linhas de domínio legadas; arquivo importado ausente |

## Hashes dos artefatos

| Artefato | SHA-256 | Bytes |
|---|---|---:|
| `recruiva-export-cross.json` | `ed714ef89a1540ee193ea078aafd67ac625ee7db76e094280256c7ca221ecfe9` | 2560 |
| `monburan-dryrun-cross.json` | `d54f1d66d1f9c5e420d2941cbe2cca6562c1f45a4f046ac29e4f47194b247390` | 1513 |
| `monburan-apply-cross.json` | `489447769a3b304ce978f992d8f78e70a335300272665414b4b2f26e63f45985` | 2116 |
| `monburan-rollback-cross.json` | `6e3e11fd2d71d32dc6e99be56a43f85ffd8e472aeb0f05cf0dd1723c01b9a306` | 174 |

## Leitura de rollback

O arquivo de rollback é o relatório operacional do desfazimento e usa `removedCounts`/`removedFiles`; o valor `problems` citado na tabela representa a ausência de falhas observadas durante a execução, não um campo serializado.

## Sanitização

Os dados usados foram fixtures sintéticos (`cand@teste.com`, `empresa@teste.com`). Nenhum segredo, dado real ou caminho de produção aparece no relatório.

## Dependências criadas após o corte

A exclusão de currículo pela aplicação é lógica e preserva o vínculo com o candidato. Se o rollback reportar esse bloqueio, reconcilie os currículos com os candidatos que continuarão no destino antes de repetir. O rollback preserva também os registros excluídos logicamente que foram criados após o corte.
