# Coletor público de vagas

O coletor Playwright consulta páginas públicas em quatro polos e gera um arquivo JSON para revisão no painel administrativo. Ele não publica vagas sozinho, não entra em contas, não contorna bloqueios e não consulta grupos privados de WhatsApp ou Facebook.

## Execução

```bash
cd tools/job-collector
npm ci
npx playwright install chromium
npm run collect -- --output collected-jobs.json
```

Para limitar a uma região ou fonte:

```bash
npm run collect -- --source Joinville --output joinville.json
npm run collect -- --source itajai-balcao --output itajai.json
```

O limite padrão é 250 itens por execução. `--max-jobs 100` reduz esse limite. As páginas são consultadas sequencialmente com intervalo para reduzir carga nos portais. Uma fonte que falhar é registrada em `failures`; os itens das outras fontes continuam sendo salvos.

## Fontes configuradas

- Ponta Grossa: PG Empregos
- Curitiba: Emprega Curitiba
- Joinville: Joinville Emprega Mais
- Itajaí: Balcão de Empregos de Itajaí e Itajaí Online

O escopo de cidades próximas está listado em `collect.mjs`. A lista é aproximada por municípios e deve ser ajustada quando a equipe validar os limites de 50 km. Itens de grupos de Facebook e WhatsApp devem ser enviados manualmente com o link público e uma captura, usando o fluxo de sugestão de vagas.

## Revisão e publicação

Importe o JSON em `/admin/captacao`. Cada item vira uma sugestão pendente em `/admin/sugestoes`. A origem fica gravada no link da sugestão e é transportada para a vaga externa quando o moderador aprova. Confira se a vaga continua aberta, corrija os dados e defina o canal de candidatura antes de publicar.

A página fica desligada por padrão. Um administrador deve habilitar `captacao-vagas` em `/admin/funcionalidades` para o grupo desejado antes de usar a importação.

O coletor mantém somente um trecho curto para auxiliar a triagem. Ele não importa currículos, telefones ou e-mails de candidatos. Se uma fonte bloquear a coleta ou exigir autenticação, ela deve permanecer fora do coletor até existir uma integração autorizada.
