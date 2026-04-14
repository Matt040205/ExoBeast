# Sessão — 14 Abril 2026

## Contexto

Sessão de correção de bugs multiplayer e reorganização de documentação.
Branch: `main`

Origem: relatório do game designer (`message.txt`) descrevendo falhas identificadas em
testes multiplayer com 2 jogadores (MPPM). A causa raiz unificadora foi o uso incorreto
de `if (!IsOwner) return;` dentro de Logic scripts que são executados **no servidor**
via `RequestActivateAbilityServerRpc` — o servidor nunca é owner do jogador remoto,
então o Jogador 2 nunca executava suas habilidades.

---

## Bugs Corrigidos

### Bug 1 — Animação de Curar (PeaceOfMind) não aparecia em nenhum cliente

**Arquivo:** `Assets/Codigo/Characters/Raposa/PeaceOfMindLogic.cs`

**Causa raiz (dupla):**
1. `StartEffect()` tinha `if (!IsOwner) return;` — Jogador 2 bloqueado completamente antes de chegar ao ServerRpc.
2. Dentro de `RequestPeaceOfMindServerRpc()`, `netAnim.SetTrigger("Heal")` era chamado no servidor. O NGO só propaga `NetworkAnimator.SetTrigger()` quando chamado pelo **owner** — chamadas do servidor disparam localmente no host mas não replicam para clientes.

**Fix aplicado:**
- Removido `if (!IsOwner) return;` de `StartEffect()`.
- Removido `netAnim.SetTrigger("Heal")` do ServerRpc.
- Adicionado `PlayHealAnimationClientRpc()` que só dispara o trigger se `IsOwner` — a propagação automática do NetworkAnimator cuida dos remotos a partir daí.

```csharp
// ANTES (quebrado)
public void StartEffect(float totalHeal, float duration, Ability sourceAbility)
{
    if (!IsOwner) return; // bloqueava jogador 2
    RequestPeaceOfMindServerRpc(totalHeal, duration);
}

[ServerRpc]
private void RequestPeaceOfMindServerRpc(float totalHeal, float duration)
{
    var netAnim = GetComponent<NetworkAnimator>() ?? GetComponentInChildren<NetworkAnimator>();
    if (netAnim != null) netAnim.SetTrigger("Heal"); // não propaga para clientes
    ...
}

// DEPOIS (correto)
public void StartEffect(float totalHeal, float duration, Ability sourceAbility)
{
    RequestPeaceOfMindServerRpc(totalHeal, duration); // sem guard
}

[ServerRpc]
private void RequestPeaceOfMindServerRpc(float totalHeal, float duration)
{
    PlayHealAnimationClientRpc(); // owner recebe → propaga via NetworkAnimator
    PlayHealSFXClientRpc();
    StartCoroutine(HealCoroutine(totalHeal, duration));
}

[ClientRpc]
private void PlayHealAnimationClientRpc()
{
    if (!IsOwner) return; // só owner dispara; NGO propaga para remotos
    var netAnim = GetComponent<NetworkAnimator>() ?? GetComponentInChildren<NetworkAnimator>();
    if (netAnim != null) netAnim.SetTrigger("Heal");
}
```

---

### Bug 2a — Dash (CuttingBlade / Q da Raposa) não funcionava para Jogador 2

**Arquivo:** `Assets/Codigo/Characters/Raposa/CuttingBladeLogic.cs`

**Causa raiz:**
`StartDash()` tinha `if (!IsOwner) return;` e usava `CharacterController` e `PlayerMovement`
diretamente — componentes desabilitados no servidor para jogadores remotos (via
`PlayerNetworkSetup.OnNetworkSpawn()`). Mesmo sem o guard, rodar o dash no servidor
para o Jogador 2 seria incorreto.

**Fix aplicado:**
Servidor detecta que não é owner → envia ClientRpc exclusivamente para o owner usando
`ClientRpcParams` com `TargetClientIds = {OwnerClientId}`. O owner executa o dash
localmente com seus próprios componentes habilitados.

```csharp
public void StartDash(GameObject quemUsou, CharacterController cont, Transform pivot,
    float dist, float dmg, string som, CommanderAbilityController abCont, Ability ability, bool resetOnKill)
{
    dashDistance = dist; damage = dmg; eventoDash = som;
    abilityController = abCont; sourceAbility = ability;
    resetCooldownOnKill = resetOnKill; modelPivot = pivot;

    if (IsOwner)
    {
        controller = cont;
        playerMovement = quemUsou.GetComponent<PlayerMovement>();
        abilityController.SetAbilityUsage(sourceAbility, true);
        StartCoroutine(DashCoroutine(quemUsou));
    }
    else if (IsServer)
    {
        // Delega ao owner — ele tem CharacterController e PlayerMovement habilitados
        var p = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
        };
        ExecuteDashOnOwnerClientRpc(dist, dmg, som, resetOnKill, p);
    }
}

[ClientRpc]
private void ExecuteDashOnOwnerClientRpc(float dist, float dmg, string som, bool resetOnKill,
    ClientRpcParams _ = default)
{
    dashDistance = dist; damage = dmg; eventoDash = som;
    resetCooldownOnKill = resetOnKill; modelPivot = transform;
    controller = GetComponent<CharacterController>();
    playerMovement = GetComponent<PlayerMovement>();
    abilityController = GetComponent<CommanderAbilityController>();
    StartCoroutine(DashCoroutine(gameObject));
}
```

---

### Bug 2b — Ultimate NineTailsDance (X da Raposa) não funcionava para Jogador 2

**Arquivo:** `Assets/Codigo/Characters/Raposa/NineTailsDanceLogic.cs`

**Causa raiz:**
- `StartEffect()` tinha `if (!IsOwner) return;` — bloqueava Jogador 2.
- `UltimateTimerCoroutine` usava `if (IsOwner)` para encerrar a ultimate — quando rodando
  no servidor para o Jogador 2, `IsOwner` é `false` então a ultimate nunca terminava.

**Fix aplicado:**
Trocado `if (!IsOwner) return;` por `if (!IsServer) return;`. Como `StartEffect` já
é chamado pelo servidor, atribuições às `NetworkVariables` são feitas diretamente
(sem necessidade do ServerRpc interno). O timer também usa `if (IsServer)`.

```csharp
// ANTES (quebrado)
public void StartEffect(float duration)
{
    if (!IsOwner) return;           // bloqueava jogador 2
    SetUltimateStateServerRpc(true);
    StartCoroutine(UltimateTimerCoroutine(duration));
}

private IEnumerator UltimateTimerCoroutine(float duration)
{
    yield return new WaitForSeconds(duration);
    if (IsOwner) SetUltimateStateServerRpc(false); // nunca executava no servidor para J2
}

// DEPOIS (correto)
public void StartEffect(float duration)
{
    if (!IsServer) return;          // só roda no servidor (correto)
    netIsUltimateActive.Value = true; // atribuição direta — já estamos no servidor
    if (combatManager != null)
    {
        previousCombatType = combatManager.netCombatType.Value;
        combatManager.netCombatType.Value = CombatType.Melee;
    }
    StartCoroutine(UltimateTimerCoroutine(duration));
}

private IEnumerator UltimateTimerCoroutine(float duration)
{
    yield return new WaitForSeconds(duration);
    if (IsServer)                   // funciona para ambos os jogadores
    {
        netIsUltimateActive.Value = false;
        if (combatManager != null)
            combatManager.netCombatType.Value = previousCombatType;
    }
}
```

---

### Bug 2c — Habilidade Voo Gracioso (Q da Coruja) não funcionava para Jogador 2

**Arquivos:**
- `Assets/Codigo/Characters/Coruja/HabilidadeVooGracioso.cs`
- `Assets/Codigo/Characters/Coruja/VooGraciosoLogic.cs`

**Causa raiz:**
`HabilidadeVooGracioso.Activate()` manipulava `PlayerMovement` e `PlayerShooting`
diretamente no servidor — mas ambos estão **desabilitados** no servidor para jogadores
remotos (comportamento de `PlayerNetworkSetup`). Além disso, `VooGraciosoLogic` (um
NetworkBehaviour correto com NetworkVariables) existia no projeto mas **nunca era usado**
por `HabilidadeVooGracioso`.

Adicionalmente, `VooGraciosoLogic.StartEffect()` chamava `serverShooting.SetNextShotBonus()`
no servidor — mesmo problema de componente desabilitado.

**Fix em `HabilidadeVooGracioso.cs`:**
```csharp
// ANTES: manipulação direta (quebrado para J2)
movement.isFloating = true;
movement.floatDuration = staticAimDuration;
shooting.SetNextShotBonus(bonusDamageMultiplier, bonusExplosionRadius);

// DEPOIS: delega ao NetworkBehaviour
VooGraciosoLogic logic = quemUsou.GetComponent<VooGraciosoLogic>();
logic.StartEffect(quemUsou, jumpHeightModifier, staticAimDuration,
    bonusDamageMultiplier, bonusExplosionRadius, null, this);
```

**Fix em `VooGraciosoLogic.cs`:**
`SetNextShotBonus` movido do `StartEffect()` (servidor) para `OnNetworkSpawn()` (owner),
onde `PlayerShooting` está habilitado:

```csharp
// StartEffect: apenas seta NetworkVariables, sem tocar em componentes desabilitados
public void StartEffect(...) {
    if (!IsServer) return;
    netBonusDamage.Value = bonusDamage;
    netBonusRadius.Value = bonusRadius;
    // NÃO: serverShooting.SetNextShotBonus() — desabilitado no servidor para remotos
}

// OnNetworkSpawn: owner aplica o bônus localmente (componente habilitado aqui)
public override void OnNetworkSpawn() {
    if (netOwner.Value.TryGet(out NetworkObject ownerNO) && ownerNO.IsOwner) {
        playerMovement.isFloating = true;
        playerShooting?.SetNextShotBonus(netBonusDamage.Value, netBonusRadius.Value); // NOVO
    }
}
```

> **Pré-requisito Editor:** `VooGraciosoLogic` deve ser componente no prefab do jogador.

---

### Bug 3 — Animação de tiro duplicada para jogadores remotos

**Arquivo:** `Assets/Codigo/Characters/Player/PlayerShooting.cs`

**Causa raiz:**
`ExecuteShootVisual()` chamava `networkAnimator.SetTrigger("Shoot")` incondicionalmente.
O owner dispara o trigger localmente → NGO NetworkAnimator propaga automaticamente para
todos os clientes remotos. Paralelamente, `ShootVisualClientRpc` chegava nos remotos e
eles chamavam `SetTrigger` novamente → animação duplicada.

**Fix aplicado (1 linha):**
```csharp
// ANTES
if (networkAnimator != null) networkAnimator.SetTrigger("Shoot");

// DEPOIS
// Trigger apenas pelo owner — NGO propaga para remotos automaticamente
if (isOwnerShot && networkAnimator != null) networkAnimator.SetTrigger("Shoot");
```

---

## Bugs Fora de Escopo (documentados, não corrigidos)

### Bug 4 — Armadilhas invisíveis para Jogador 2
**Causa:** Prefabs de armadilha não registrados na `DefaultNetworkPrefabs.asset`.
**Ação necessária:** No Unity Editor, adicionar manualmente os prefabs de armadilha ao
asset `Assets/DefaultNetworkPrefabs.asset`. Não pode ser feito via script.

### Bug 5 — Armadilhas sem efeito (nenhum jogador)
**Causa:** Não existem implementações concretas de `TrapLogicBase` no projeto
(ex: `DamageTrap`, `SlowTrap`). É uma feature ausente, não um bug de sincronização.

### Bug 6 — Timer desincronizado
**Status:** Aparentemente já corrigido em sessão anterior. `UIManager` já lê
`HordeManager.currentMatchTime` com predição entre ticks + `ForceTimerSync()` chamado
pelo `MatchManager.OnNetworkSpawn()`. Verificar em teste se `HordeManager` expõe
`currentMatchTime` como `NetworkVariable<float>`.

---

## Reorganização de Documentação

Eliminado ruído de arquivos `.md` soltos. Todos centralizados em `Assets/Codigo/Docs/`.

| Arquivo | Origem | Destino |
|---------|--------|---------|
| `SESSAO_10ABR2026.md` | Raiz | **Deletado** (info extraída para MEMORY.md) |
| `SESSAO_11ABR2026.md` | Raiz | **Deletado** (info extraída para MEMORY.md) |
| `GIT_WORKFLOW.md` | Raiz | `Assets/Codigo/Docs/GIT_WORKFLOW.md` |
| `Assets/CLAUDE.md` | `Assets/` | `Assets/Codigo/Docs/CLAUDE_MULTIPLAYER_GUIDE.md` |
| `parallel-enchanting-harp.md` | `Assets/Codigo/Multiplayer/` | `Assets/Codigo/Docs/parallel-enchanting-harp.md` |

Estado atual da raiz: **zero arquivos `.md`** (apenas `message.txt` não rastreado).

---

## Padrão NGO — Lição desta Sessão

O erro sistemático encontrado em múltiplos scripts:

```
// ERRADO: Ability.Activate() roda no servidor via RequestActivateAbilityServerRpc.
//         No servidor, IsOwner é FALSE para o jogador 2.
//         → A habilidade do jogador 2 nunca executa.
public void StartEffect(...)
{
    if (!IsOwner) return; // ← bug
}
```

**Regra correta:**
| Contexto | Guard correto |
|----------|--------------|
| Input / detecção de tecla | `if (!IsOwner) return;` |
| Aplicar efeito / estado | `if (!IsServer) return;` |
| Visual / som local | `if (!IsOwner) return;` dentro de ClientRpc |
| Componentes locais (CharacterController) | ClientRpc com `TargetClientIds = {OwnerClientId}` |

`NetworkAnimator.SetTrigger()` só propaga quando chamado pelo **owner**. Para disparar
animação após lógica de servidor: enviar ClientRpc ao owner, que dispara o trigger.

---

## Arquivos Modificados

| Arquivo | Tipo |
|---------|------|
| `Assets/Codigo/Characters/Raposa/PeaceOfMindLogic.cs` | Fix bug 1 + 2 |
| `Assets/Codigo/Characters/Raposa/CuttingBladeLogic.cs` | Fix bug 2a |
| `Assets/Codigo/Characters/Raposa/NineTailsDanceLogic.cs` | Fix bug 2b |
| `Assets/Codigo/Characters/Coruja/HabilidadeVooGracioso.cs` | Fix bug 2c |
| `Assets/Codigo/Characters/Coruja/VooGraciosoLogic.cs` | Fix bug 2c |
| `Assets/Codigo/Characters/Player/PlayerShooting.cs` | Fix bug 3 |

---

## Checklist de Verificação (MPPM — 2 jogadores)

- [ ] Compilar Unity Editor sem erros
- [ ] Jogador 1 (Raposa) — Q: dash visível nas duas telas
- [ ] Jogador 2 (Raposa) — Q: dash funciona para o próprio Jogador 2
- [ ] Jogador 1 (Raposa) — E: cura com animação visível nas duas telas
- [ ] Jogador 2 (Raposa) — E: cura funciona + animação visível nas duas telas
- [ ] Jogador 1 (Raposa) — X: ult ativa modo melee (animação + stats)
- [ ] Jogador 2 (Raposa) — X: mesma coisa
- [ ] Qualquer jogador (Coruja) — Q: flutuação no ar + próximo tiro com bônus
- [ ] Tiro da Coruja: animação aparece **uma vez** nas telas remotas (sem duplicata)
- [ ] Editor: adicionar prefabs de armadilha ao `DefaultNetworkPrefabs.asset`
- [ ] Timer: ambas as telas mostram o mesmo tempo
