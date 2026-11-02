# Feature flags por grupo de acesso

Funcionalidade nova vai para produção **desligada** e é liberada por grupo: primeiro o time, depois os testadores beta,
por fim todo mundo. Assim o merge e o deploy deixam de ser o momento de risco, e dá para testar em produção, com dados
reais, sem ambiente de staging.

## Conceitos

| Conceito | O que é |
|---|---|
| **Flag** | Nome usado no código (`nova-busca-de-vagas`), descrição, grupos liberados, "liberada para todos" e data para sair do código. Tabela `FeatureFlags`. |
| **Grupo implícito** | Vem do papel e do perfil da conta, sem cadastro: `admin`, `produto`, `candidato` (toda conta de candidato) e `recrutador` (toda conta de empresa). |
| **Grupo atribuído** | Montado à mão, conta por conta: `candidato-beta` e `recrutador-beta` já existem; outros podem ser criados. Tabela `AccessGroups`; a participação é a claim `monburan:grupo` do Identity (`AspNetUserClaims`). |
| **Papel `Produto`** | Quem cuida das funcionalidades sem ser administrador. Abre só `/admin/funcionalidades` (2FA obrigatório). Só administradores concedem. |

Uma flag está ligada para a conta se estiver **liberada para todos** ou se a conta estiver em **pelo menos um** dos grupos
dela. Flag inexistente conta como desligada. Não há liberação por percentual.

A participação é lida do banco com cache de 1 minuto (as flags, 30 segundos), e a tela descarta o cache a cada mudança:
incluir alguém num grupo vale na hora, no site e no app, sem novo login. Se o banco falhar, tudo que está atrás de flag
fica desligado e o resto do site continua.

## Gerenciar

`/admin/funcionalidades` (administradores e equipe de produto):

- **Funcionalidades:** criar, escolher os grupos, liberar para todos, excluir. O nome não muda depois de criado.
- **Grupos de acesso:** criar grupos e incluir ou retirar contas pelo e-mail. Um grupo que ainda libera alguma flag não
  pode ser excluído.
- **Equipe de produto:** só administradores veem esta parte.

Tudo fica na auditoria (`/admin/auditoria`).

## Usar no código

**Página ou componente da web (Blazor):**

```razor
<FeatureGate Feature="nova-busca-de-vagas">
    <NovaBusca />
    <Disabled><BuscaAtual /></Disabled>   @* opcional: o comportamento antigo *@
</FeatureGate>
```

**Endpoint da API** (responde 404, como se não existisse, quando desligada):

```csharp
group.MapPost("/busca-avancada", BuscaAvancada).RequireFeature("nova-busca-de-vagas");
```

**Caso de uso ou serviço:** injete `IFeatureFlags` e chame `IsEnabledAsync("nova-busca-de-vagas", user)`.

**App (MAUI):** o `FeatureFlagsState` busca `GET /api/v1/features` ao abrir o app e a cada login ou logout.

```razor
<FeatureGate Feature="nova-busca-de-vagas"><NovaBusca /></FeatureGate>
```

Ou `FeatureFlagsState.IsEnabled("...")`. Sem internet, o app mantém a última lista; sem nenhuma, tudo atrás de flag fica
desligado. Com isso, a funcionalidade nova do app pode ser liberada sem publicar outro APK.

> Esconder na tela não protege nada. Toda funcionalidade atrás de flag confere **também no servidor** (`RequireFeature`
> ou `IFeatureFlags`).

## Ciclo de vida de uma flag

1. A funcionalidade entra na `dev` e na `main` atrás de uma flag **sem grupos** (desligada). Cadastre a flag na tela
   com a data prevista para sair do código.
2. Libere para `admin` e `produto`: o time testa em produção.
3. Libere para `candidato-beta` e/ou `recrutador-beta`.
4. Libere para `candidato` ou `recrutador` (todo o público daquele lado) ou marque "liberada para todos".
5. Com tudo estável, **tire a flag do código** (o `FeatureGate`, o `RequireFeature` e o caminho antigo) e depois exclua a
   flag na tela. A tela destaca em vermelho as flags com a data de remoção vencida.

Mantenha poucas flags ativas ao mesmo tempo (até umas 10): cada uma dobra os caminhos que precisam funcionar.

## Flag não é plano

Regra de negócio permanente ("Premium tem assistente de IA", "Free publica até 5 vagas") fica nos **planos**
(`SubscriptionPlan`). Flag é temporária e serve para liberar com segurança. Uma funcionalidade nova paga pode usar os
dois: a flag decide se já existe para aquela conta; o plano decide se ela pode usar.

## Banco de dados

Migration **não** fica atrás de flag: ela roda no deploy para todos. Toda mudança de banco precisa funcionar com a flag
ligada e desligada (expandir e depois contrair): primeiro adiciona coluna ou tabela nova, depois o código passa a usar,
e só numa entrega posterior, com a flag removida, o que ficou velho sai. Backup antes do deploy continua obrigatório.
