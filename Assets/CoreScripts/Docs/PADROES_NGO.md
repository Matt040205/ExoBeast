# Padrões NGO — ExoBeasts V3

Status: ativo
Público: desenvolvedores que mexem em código multiplayer
Última atualização: 2026-06-30

Catálogo de padrões e armadilhas do Netcode for GameObjects descobertos a partir de bugs reais no projeto.
Cada padrão inclui o que é, por que existe, como reconhecer uma violação, e o código correto.

Leia este arquivo **antes de mexer** em qualquer script que herde de `NetworkBehaviour`.

---

## P1 — `IsServer` pré-Spawn é silencioso (a mais traiçoeira)

### O problema

`NetworkBehaviour.IsServer` é definido internamente como `IsSpawned && NetworkManager.IsServer`.

Antes de chamar `netObj.Spawn()`, o objeto ainda não está spawned — então `IsServer` retorna `false` sem nenhum erro ou aviso. Qualquer método com `if (!IsServer) return;` sai silenciosamente e as NetworkVariables ficam nos valores default.

### Por que isso quebra tanto

O padrão mais comum no projeto é pré-configurar NetworkVariables ANTES do Spawn para que os valores corretos entrem no snapshot inicial enviado aos clientes. Se o guard usar `IsServer` herdado, o método inteiro é pulado silenciosamente.

Cascata típica de bug:
1. NetworkVariables ficam em defaults (`-1`, `null`, `0`)
2. `OnNetworkSpawn` → reconciliação falha (ex.: `TrapIndex.Value == -1` → `return`)
3. Registro nunca acontece → `buildLimit` ignorado, HUD zerado, animação quebrada

Isso aconteceu **duas vezes** no projeto com o sistema de armadilhas (sessões 2 Maio e 6 Maio 2026).

### Regra

Em métodos chamados **antes** do `Spawn()` (como `InitializeServer`, `InitializeTowerServer`), substituir:

```csharp
// ❌ ERRADO — retorna false antes do Spawn sem nenhum erro
if (!IsServer) return;
```

por:

```csharp
// ✅ CORRETO — verifica diretamente o NetworkManager
if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
```

Em métodos chamados **depois** do Spawn (`OnNetworkSpawn`, `OnTriggerEnter`, ServerRpcs), `IsServer` herdado funciona normalmente.

### Âncoras no código

Comentários "REGRA DE OURO NGO" marcam todos os pontos críticos:
- `Assets/Multiplayer/Sync/NetworkedTrapVisual.cs:73` — `InitializeServer`
- `Assets/Multiplayer/Sync/NetworkedBuilding.cs:80` — `InitializeTowerServer`

---

## P2 — Rigidbody Kinematic para triggers em jogadores remotos

### O problema

`ClientNetworkTransform` (owner-authoritative) escreve `transform.position` diretamente no servidor para mover o player remoto. O problema: o Unity Physics System rastreia colisões pelo `CharacterController.Move()` — não pelo assignment direto de `transform.position`.

Resultado: triggers como Fogueira (heal), Teleportador, Espinhos, Piche e Broca **não disparam no servidor** quando um player remoto os atravessa.

O player do host funciona normalmente porque é movido via `CharacterController.Move()` no caminho padrão do `PlayerMovement`.

### A solução

`GameSetupManager.EnsureRuntimePlayerNetworkContract` adiciona um `Rigidbody Kinematic` em runtime em todos os players spawnados:

```csharp
var rb = playerObject.AddComponent<Rigidbody>();
rb.isKinematic = true;
rb.useGravity = false;
rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
```

**Por que Kinematic?** O Rigidbody kinematic não responde a forças (PlayerMovement usa `CharacterController`, não `Rigidbody.AddForce`), mas faz o Unity chamar `Physics.SyncTransforms` automaticamente quando `transform.position` muda — o que dispara os triggers corretamente.

### Regra

Nunca remover o `Rigidbody` adicionado por `EnsureRuntimePlayerNetworkContract`. Não depende de configuração manual em cada prefab de personagem — é aplicado automaticamente para cobrir todos os comandantes.

---

## P3 — CharacterController em remoto: SEMPRE habilitado

### O problema

Existe uma intuição errada de que o `CharacterController` do player remoto (server-side) deve ser desabilitado para não competir com o `ClientNetworkTransform`. **Isso é falso e quebra tudo.**

O `CharacterController` é um Collider. Quando desabilitado, `OnTriggerEnter` não dispara. Resultado: nenhuma armadilha detecta o player remoto.

### Por que a confusão existe

A regra antiga dizia "CC desabilitado em remoto evita conflito com NetworkTransform". Mas `PlayerMovement.enabled = false` já é setado em `SetupAsRemotePlayer()` — e isso impede que `characterController.Move()` seja chamado em remotos. O CC fica idle e **não compete** com o `ClientNetworkTransform`.

**Manter o CC habilitado é correto e necessário** para detecção de triggers.

### Regra

`CharacterController` em player remoto no servidor: **sempre habilitado**.

Exceção: pode ser desabilitado temporariamente dentro de `PlayerTeleportService.TeleportInternal` (lógica de teleporte), mas deve ser restaurado ao estado anterior logo em seguida:

```csharp
bool wasEnabled = characterController.enabled;
characterController.enabled = false;
// ... teleporte
characterController.enabled = wasEnabled; // restaurar, não forçar true
```

---

## P4 — Zona protegida do PlayerInput (FinishLocalSetupNextFrame)

### O problema

A coroutine `PlayerNetworkSetup.FinishLocalSetupNextFrame()` faz o setup do input do host. O ciclo de disable→enable do `PlayerInput` é obrigatório para liberar e reconectar o teclado no Input System.

Coroutines do Unity **não têm try/catch implícito**. Se qualquer linha lançar exceção antes de `playerInput.enabled = true`, o PlayerInput fica permanentemente desabilitado e o host não se move.

Isso aconteceu na sessão de 25 Abril 2026: alguém adicionou `BuildManager.ForceBuildMode(false)` ANTES do ciclo do PlayerInput. `ForceBuildMode` aciona uma cadeia `UIManager → TopDownCamera → TutorialManager` — se qualquer um lançar null reference, o ciclo é interrompido.

### A sequência obrigatória (ATÔMICA)

```csharp
// Nada entra dentro ou entre essas linhas
playerInput.enabled = false;
yield return null;                          // único yield permitido aqui
playerInput.enabled = true;
playerInput.SwitchCurrentActionMap("Player");
localInputBridge.enabled = true;           // bridge UP — input pronto

// Só DEPOIS do bridge: side-effects com potencial de exceção
if (BuildManager.Instance != null)
    BuildManager.Instance.ForceBuildMode(false);
// ...UIManager, TopDownCamera, TutorialManager, etc.
```

### Sintomas de violação

Host não se move após spawn:
- Console **não** mostra `[PlayerNetworkSetup] PlayerInput configurado no ActionMap 'Player'.`
- NullReferenceException em `UIManager`, `TopDownCameraManager` ou `BuildManager` no mesmo frame do spawn

### Regra

Qualquer código que toca `UIManager`, `TopDownCameraManager`, `TutorialManager`, `BuildManager.ToggleBuildMode` ou `PauseControl` deve vir **depois** de `localInputBridge.enabled = true`.

---

## P5 — ClientNetworkTransform.SetState() é ignorado para non-host owners

### O problema

`ClientNetworkTransform` é owner-authoritative. O servidor não pode forçar posição diretamente em um player remoto (non-host owner) via `SetState()`. A chamada é silenciosamente ignorada.

### Padrão correto para teleporte

```csharp
// ✅ Para teleportar um player remoto
if (player.OwnerClientId == NetworkManager.ServerClientId)
{
    // Host-owned: SetState funciona
    networkTransform.SetState(newPos, newRot, scale, teleport: true);
}
else
{
    // Cliente remoto: ClientRpc direcionado ao owner
    TeleportOwnerClientRpc(newPos, newRot,
        new ClientRpcParams { Send = { TargetClientIds = new[] { player.OwnerClientId } } });
}
```

O `ClientRpc` owner-targeted faz o owner mover seu próprio player localmente. O `ClientNetworkTransform` então replica essa posição para os outros.

### Referências

- `Assets/Characters/Player/PlayerTeleportService.cs` — `TeleportServerValidated`
- `Assets/Characters/Player/PlayerHealthSystem.cs` — `RespawnClientRpc`

---

## P6 — Prefab Variant e fileID em builds standalone

### O problema

Ao criar um Prefab Variant e arrastá-lo para um ScriptableObject no Inspector, o Unity pode guardar o fileID do prefab original (herdado pelo variant) em vez do fileID real do variant. Esse fileID "virtual" não existe no YAML do arquivo `.prefab` do variant.

No Editor, o AssetDatabase resolve via GUID e não mostra o problema. Em builds standalone, a busca é estrita: a referência retorna null e o objeto não é spawnado.

Isso afetou os inimigos: sumiam em builds mas apareciam normalmente no Editor.

### Regra

Ao criar um Prefab Variant que será referenciado em ScriptableObjects:
1. Criar o variant
2. **Re-arrastar o variant** no campo do Inspector (não reutilizar a referência do prefab original)
3. Fazer build de teste para confirmar o spawn

---

## P7 — NetworkVariable: nunca escrever em Update sem accumulator

### O problema

```csharp
// ❌ ERRADO — gera ~60 mensagens de rede por segundo
void Update()
{
    if (IsServer)
        currentHealth.Value -= regenRate * Time.deltaTime;
}
```

Cada escrita em uma NetworkVariable gera um pacote de rede. Escrever em Update multiplica o tráfego por 60×.

### Padrão correto: accumulator com threshold

```csharp
private float _pendingRegen;

void Update()
{
    if (!IsServer) return;
    _pendingRegen += regenRate * Time.deltaTime;
    if (_pendingRegen >= 1f)
    {
        float toApply = Mathf.Floor(_pendingRegen);
        _pendingRegen -= toApply;
        currentHealth.Value += toApply; // uma escrita por 1 HP ganho
    }
}
```

Reset o accumulator em eventos de invalidação (TakeDamage, morte). Ganho típico: 95–98% de redução de tráfego.

### Referências com esse padrão

- `PlayerHealthSystem._pendingRegenAmount`
- `CommanderAbilityController._pendingPassiveCharge`
- `MatchManager._matchTimeAccumulator`

---

## P8 — ServerRpc com RequireOwnership rejeitada se chamada do servidor

### O problema

Um `[ServerRpc]` com `RequireOwnership = true` (o default) é verificado pelo NGO no sender. Se o NetworkObject pertence ao ClientId 5, mas o código do servidor (ClientId 0) tenta invocar esse ServerRpc, a chamada é rejeitada silenciosamente — sem erro, sem log.

Sintoma típico: cura ou buff "não funciona" quando testado no host, mas funciona quando testado como cliente remoto.

### Causa raiz

Ao executar habilidades: o servidor pode querer chamar um RPC para acionar lógica em um NetworkObject owned por um cliente. Se usar ServerRpc com ownership, o servidor não tem permissão.

### Soluções

**Opção A (preferida):** Rodar diretamente no servidor sem ServerRpc:

```csharp
// Em vez de invocar ServerRpc, rodar diretamente se já estamos no servidor
public void StartEffect(...)
{
    if (!IsServer) return; // guard direto
    // lógica de servidor aqui
}
```

**Opção B:** Usar `RequireOwnership = false` quando o método precisa ser invocável de qualquer lugar:

```csharp
[ServerRpc(RequireOwnership = false)]
public void ApplyEffectServerRpc(...)
{
    if (!IsServer) return;
    // ...
}
```

### Referências

- `NineTailsDanceLogic.StartEffect` — usa Opção A
- `PeaceOfMindLogic.StartEffect` — usa Opção A

---

## Checklist rápido ao adicionar código multiplayer

Antes de commitar qualquer script que herde de `NetworkBehaviour`, verificar:

- [ ] Métodos chamados antes de `Spawn()` usam `NetworkManager.Singleton.IsServer` (não `IsServer` herdado)?
- [ ] Players remotos têm `CharacterController` habilitado no servidor?
- [ ] Qualquer código novo em `FinishLocalSetupNextFrame` vai DEPOIS de `localInputBridge.enabled = true`?
- [ ] Teleporte de player remoto usa ClientRpc direcionado ao owner (não `SetState`)?
- [ ] NetworkVariables escritas em Update usam accumulator com threshold?
- [ ] ServerRpcs em NetworkObjects de outros clientes têm `RequireOwnership = false`?
- [ ] Prefab Variants foram re-arrastados no Inspector após criação?
- [ ] Testar com MPPM (host + cliente) após qualquer mudança nos arquivos listados em `bug_host_client_movement.md`?
