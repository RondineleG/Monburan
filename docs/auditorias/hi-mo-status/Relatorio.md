# HI-MO-STATUS — avaliação de sincronização de status

Data UTC: 2026-11-26

## Decisão

`N/A` para produtor/consumidor de `ApplicationStatusChanged.v1` nesta fatia.

## Fundamento

A consulta consentida implementada em HI-MO cobre a jornada autorizada: HiMarshall recebe somente campos públicos mínimos da vaga e encaminha a candidatura ao Monburan pelo link de destino. Nenhuma funcionalidade autorizada do HiMarshall consome hoje o status da candidatura.

Emitir eventos agora criaria dados e acoplamento sem consumidor legítimo. Também faltaria uma decisão explícita de vínculo entre o consentimento local do HiMarshall e a candidatura do Monburan; o token `mbi_` autentica o titular, mas não foi concebido como identificador de consentimento transversal.

## Verificação operacional

- `GET /api/v1/integrations/jobs` respondeu 200 no ensaio local somente com `id`, `title`, `company`, `location` e `applyUrl`.
- O titular revogável/inativo é bloqueado antes da consulta.
- Nenhum evento `ApplicationStatusChanged` foi emitido e nenhuma projeção derivada existe para limpar.

## Rollback

Não aplicável por ausência de produtor/consumidor. Se a fatia for autorizada futuramente, o rollback será parar o consumidor e limpar apenas dados derivados conforme retenção.

## Reabertura

Reabrir somente com requisito explícito de acompanhamento de candidatura na jornada. A implementação deve incluir consentId transversal, escopo `applications.status.read`, dedup, rejeição de versão fora de ordem e revogação terminal antes de qualquer projeção.
