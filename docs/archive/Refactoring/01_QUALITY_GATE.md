# 01 — Quality Gate (critérios completos, expandidos e autocontidos)

> Este documento expande todos os critérios do Quality Gate de forma autônoma — **você não precisa abrir o repositório `mestre_darmas` para entendê-los**.
> Critérios baseados em:
> - `mestre_darmas/hotel-fazenda-system/.claude/skills/quality-gate/SKILL.md`
> - `mestre_darmas/hotel-fazenda-system/.claude/skills/quality-gate/references/philosophy.md`
> - `mestre_darmas/hotel-fazenda-system/.claude/skills/quality-gate/references/quality-rules.md`
> - `mestre_darmas/hotel-fazenda-system/.claude/skills/quality-gate/references/unity-extension.md`
> - `mestre_darmas/hotel-fazenda-system/docs/CODE_QUALITY_CRITERIA.md`
> - `mestre_darmas/hotel-fazenda-system/docs/QUALITY_GATE.md`

---

## 1. Filosofia (princípio fundamental)

### 1.1 Ratchet — "Não piora"

**Conceito.** O código pode estar imperfeito hoje. O objetivo do gate não é forçar perfeição imediata. O objetivo é: **nenhuma mudança piora o estado atual**.

- **Métrica pode melhorar livremente.**
- **Métrica não pode regredir.**
- O baseline aceito hoje vira o piso. PRs novos comparam-se contra esse piso.

**Como verificar (PASS).**
- Você abre um PR. O Quality Gate compara o estado atual com o baseline. Se cobertura, duplicação, número de arquivos oversized e contagem de violações de complexidade **forem iguais ou melhores**, passa.

**Como verificar (FAIL).**
- Você abre um PR que adiciona um arquivo de 900 LOC — **bloqueia** (arquivo novo > limite).
- Você abre um PR que sobe duplicação de 6% para 7% — **bloqueia** (ratchet).
- Você abre um PR que aumenta lint errors de 0 para 1 — **bloqueia** (lint errors sempre bloqueiam).

**Aplicação ao PI3D.**
- `LobbyManager.cs` tem 1626 LOC hoje. Ele é o baseline. Após cada sprint, o LOC **só pode descer** — nunca subir.
- A duplicação atual entre `LobbySceneUI/LobbyUIManager/LobbyPlaceholderUI/MenuLobbyPanel` é o teto. Cada sprint que remove um deles abaixa o piso.

**Consequência de violação.**
- PR rejeitado. O agente refaz o trabalho até atingir paridade ou melhoria com o baseline. **Nunca** atualizar o baseline para fazer o PR passar.

### 1.2 Baseline é sagrado

**Conceito.** O arquivo `quality/baseline.json` é a memória do estado aceito de `main`. Mudar este arquivo é uma decisão deliberada, feita em PR isolado, com mensagem de commit explicando o que foi aceito.

**Transições permitidas.**
- Inicialização (uma vez na adoção do gate).
- Após melhoria aceita: rodar `quality:baseline` em `main`, commit isolado.
- Após regressão aceita: rara, em PR humano dedicado.

**Transições proibidas.**
- Atualizar baseline em PR de feature para "fazer o gate passar".
- Atualizar baseline silenciosamente no mesmo commit que introduz a regressão.
- Atualizar baseline para esconder uma queda de cobertura.

**Aplicação ao PI3D.**
- Não há baseline para `.cs` automatizado ainda (Quality Gate ainda é primariamente JS/TS). O **file-size collector já roda em `.cs`**, então se rodarmos o gate apontando para `Assets/Codigo/Multiplayer/**/*.cs`, ele gera o primeiro baseline.
- **Recomendação:** o primeiro PR a fechar uma sprint executa `quality:report` apontando para o namespace Multiplayer, e o resultado é commitado em `quality/baseline.cs.json` (separado) **na branch `main` após o merge**, não no PR.

**Consequência de violação.**
- Se um agente edita o baseline em PR feature, o PR é fechado sem merge e a sprint é marcada para refazer.

### 1.3 AI é advisory, não decisor

**Conceito.** Modelos de IA podem explicar resultados do gate, sugerir patches mínimos, apontar arquivos de risco. **Não podem aprovar PRs nem decidir merge.** O gate determinístico é a autoridade.

**Aplicação ao PI3D.** Os agentes que executam as sprints **não decidem se passa**. O orquestrador interpreta logs + gate output e decide. O agente humano (você, dono do projeto) é o aprovador final do merge.

---

## 2. Critérios de tamanho de arquivo

### 2.1 Arquivos C# > 500 LOC (ideal)

**Origem.** `CODE_QUALITY_CRITERIA.md` — "Arquivos devem permanecer coesos: ideal abaixo de 500 linhas".

**Verificação.**
```powershell
Get-ChildItem -Path Assets -Recurse -Filter *.cs |
  ForEach-Object {
    $lines = (Get-Content $_.FullName | Measure-Object -Line).Lines
    if ($lines -gt 500) { "{0,5}  {1}" -f $lines, $_.FullName }
  }
```

**Aplicação multiplayer.** Hoje (inventário 2026-05-20): 5 arquivos passam — `LobbyManager` (1626), `LobbySceneUI` (731), `LobbyPlaceholderUI` (586), `LobbyUIManager` (547), `MenuLobbyPanel` (516).

**Consequência.** Cada arquivo > 500 LOC é uma flag amarela. Refatoração planejada nas sprints.

### 2.2 MonoBehaviour > 400 LOC = warning (Unity-specific)

**Origem.** `unity-extension.md` — "New `MonoBehaviour` > 400 lines → warning".

**Por quê 400 e não 500?** MonoBehaviours têm overhead implícito (lifecycle, serialização, Inspector). Acima de 400 LOC sinalizam responsabilidades múltiplas.

**PASS exemplo.**
```csharp
public class NetworkedTrapVisual : NetworkBehaviour
{
    // 218 LOC — responsabilidade única: visual da trap server-authoritative.
}
```

**FAIL exemplo.**
```csharp
public class LobbyManager : MonoBehaviour
{
    // 1626 LOC — mistura EOS + NGO + Relay + Scene + ConnectionApproval + IP + Members.
}
```

**Aplicação multiplayer.** 5 arquivos violam (mesmos da §2.1).

**Consequência.** Warning hoje. Quando o gate Unity for ativado, vira blocking acima de 700.

### 2.3 MonoBehaviour > 700 LOC = blocking (Unity-specific)

**Origem.** `unity-extension.md` — "New `MonoBehaviour` > 700 lines → blocking".

**Aplicação multiplayer.** `LobbyManager` (1626) e `LobbySceneUI` (731) violam **agora**.

**Consequência.** Quando o Quality Gate Unity for ativado, PR que mantenha esses arquivos acima de 700 LOC reprova. Refatoração para abaixar é a única saída.

### 2.4 Arquivos novos > 800 LOC = blocking

**Origem.** `QUALITY_GATE.md` — "Arquivo novo > 800 linhas bloqueia".

**Verificação.** Arquivos novos são detectados via `git diff --diff-filter=A main...HEAD`.

**PASS.** Sprint cria `MatchSessionLauncher.cs` com 480 LOC.

**FAIL.** Sprint cria `MegaLobbyManager.cs` com 850 LOC.

**Aplicação.** Ao extrair classes de `LobbyManager`, cada nova classe **deve ficar abaixo de 500 LOC**.

**Consequência.** Se você precisaria de > 500 LOC, está extraindo errado. Quebrar em duas classes.

### 2.5 Arquivos existentes oversized não podem crescer

**Origem.** `QUALITY_GATE.md` — "arquivo oversized não pode crescer".

**Verificação.** Para cada arquivo no inventário > 500 LOC, comparar LOC atual vs baseline. Se `current > baseline`, **bloqueia**.

**PASS.** Sprint extrai 500 LOC do `LobbyManager`. LobbyManager passa de 1626 → 1126. Aprovado.

**FAIL.** Sprint adiciona 50 LOC ao `LobbyManager` para "guard adicional". LobbyManager passa de 1626 → 1676. **Rejeitado.**

**Aplicação.** Esta é a regra **mais importante** durante refatoração: o LOC do `LobbyManager` **só pode diminuir**.

---

## 3. Critérios de complexidade e função

### 3.1 Funções < 80 LOC

**Origem.** `CODE_QUALITY_CRITERIA.md` — "funções abaixo de 80 linhas".

**Por quê 80?** Acima disso, função geralmente faz mais de uma coisa. Cabe em uma tela = revisor consegue ver inteira.

**Verificação.** Manualmente; ou via análise AST (Roslyn) quando o coletor Unity estiver pronto.

**PASS.**
```csharp
public bool ApplyDamageServer(float damage, ...) // 20 LOC
{
    if (!IsServer || IsDead.Value) return false;
    localHealth.ApplyAuthoritativeDamageDetailed(...);
    return finalDamage > 0f;
}
```

**FAIL.**
```csharp
private IEnumerator StartMatchCoroutine(...) // ~200 LOC
{
    // Guard sessão anterior, configurar Relay, fallback IP, retry,
    // publicar atributos, WaitForAllClients, LoadScene, ...
    // (faz 7 coisas distintas em uma única coroutine)
}
```

**Aplicação multiplayer.** Funções >80 LOC concentram-se em `LobbyManager.StartMatchCoroutine` (~200), `ConnectClientViaRelayCoroutine` (~80), e em algumas chamadas EOS com callbacks aninhados.

**Consequência.** Refatoração: quebrar em métodos privados nomeados (mesmo dentro da mesma classe), depois extrair para classe separada quando agrupados por responsabilidade.

### 3.2 Complexidade ciclomática baixa

**Origem.** `quality-rules.md` — "complexity violations". Limite default: 15 para função; 4 para profundidade de aninhamento.

**Por quê?** Complexidade alta = muitos caminhos de execução = bug latente.

**Verificação.** ESLint com regra `complexity` (para JS/TS). Para C#, Roslyn analyzer (planejado).

**PASS.**
```csharp
private void OnLobbyCreated(LobbyInfo lobby)
{
    _lobbyId = lobby.lobbyId;
    _state = State.Sala;
    AtualizarInfoSala();
}
```

**FAIL.**
```csharp
private void ProcessLobbyAttributes(LobbyDetails details)
{
    if (details == null) return;
    if (mode != Multiplayer) { Cancel(); return; }
    if (uid == hostUid) return;
    if (IsClient && !IsHost && IsConnected) return;
    var stateAttr = details.CopyAttributeByKey(...);
    if (result == Success && stateAttr.HasValue)
        if (state != InGame && state != "Starting")
            return;
    var relayAttr = details.CopyAttributeByKey(...);
    if (relayResult == Success && relayAttr.HasValue)
        if (IsUsable(relayCode))
            // conecta...
        else if (!string.IsNullOrEmpty(relayCode))
            // fallback...
    // ... continua com nesting 4
}
```
Complexidade ciclomática ~14, depth 4. No limite.

**Aplicação multiplayer.** `LobbyManager.ProcessLobbyAttributes`, `LobbyManager.StartMatchCoroutine`, `LobbyManager.OnMemberAttributeChanged` estão próximos do limite. Refatoração via early returns + extract method.

### 3.3 Profundidade de aninhamento ≤ 4

**Origem.** `quality-rules.md` — `maxDepth: 4`.

**PASS.**
```csharp
if (a) {                // depth 1
    if (b) {            // depth 2
        DoX();
        return;
    }
}
```

**FAIL.**
```csharp
if (a) {                            // 1
    if (b) {                        // 2
        if (c) {                    // 3
            if (d) {                // 4
                if (e) DoX();       // 5 — VIOLA
            }
        }
    }
}
```

**Aplicação multiplayer.** Aparece em callbacks EOS aninhados (`Find(... (info) => { ForEach(... (item) => { try { ... if (...) { ... }}})})`). Refatoração: extrair callbacks para métodos nomeados.

---

## 4. Critérios de duplicação

### 4.1 Duplicação não pode aumentar (ratchet)

**Origem.** `quality-rules.md` — `duplication.allowIncrease: false`.

**Verificação.** Ferramenta `jscpd` mede % de código duplicado. Compara contra baseline.

**PASS.** Após sprint, % duplicação atual ≤ baseline.

**FAIL.** Sprint copia bloco de código sem extrair helper. % sobe. Bloqueia.

**Aplicação multiplayer.** `jscpd` não roda em `.cs` hoje. **Detecção manual** durante sprints:
- 4 implementações de UI de lobby têm:
  - `SubscribeToEvents()` / `UnsubscribeFromEvents()` quase idênticos
  - `OnLobbyCreated/Joined/Left/MemberJoined/MemberLeft/MemberUpdated/Error` handlers
  - `_charNames = { "Coruja", "Samurai" }` em 3 lugares
  - Estado `_isReady`, `_currentLobbyId`, `_currentLobbyName` em 4 lugares
- Cada sprint que **remove** uma das UIs faz a duplicação cair sem precisar de tooling.

**Consequência.** Sprints de remoção (Sprint 1 e 6) precisam medir manualmente: contar handlers duplicados antes/depois.

### 4.2 Duplicação acima do máximo configurado (3% default)

**Origem.** `quality-rules.md` — `duplication.maximum.percentage: 3.0`, severity warning.

**Aplicação multiplayer.** Sem tooling automatizado, o critério manual é: **nenhum bloco de >15 linhas pode ser copiado idêntico entre dois arquivos**.

---

## 5. Critérios de hot-path (Unity-specific)

### 5.1 `FindObjectOfType<>()` em `Update` = blocking

**Origem.** `unity-extension.md` — "Catastrophic perf".

**Por quê?** `FindObjectOfType` percorre toda a hierarchy. Em `Update`, é O(n) por frame — em cena grande, custa milissegundos por frame.

**PASS.**
```csharp
private MyComponent _cached;

private void Awake()
{
    _cached = FindObjectOfType<MyComponent>(); // OK em init
}

private void Update()
{
    if (_cached != null) _cached.DoWork();
}
```

**FAIL.**
```csharp
private void Update()
{
    var c = FindObjectOfType<MyComponent>(); // NUNCA — varre cada frame
    c?.DoWork();
}
```

**Aplicação multiplayer.** **Já está limpo** — varredura em 2026-05-20 encontrou só uso em `EOSAuthTest.cs:45,82` (init) e `EOSManagerWrapper:131` (com rate-limit de 1s + cache via `_lastPewSearchTime`).

**Consequência.** Qualquer agente que adicione `FindObjectOfType` em `Update` reprova a sprint.

### 5.2 `GetComponent<>()` repetido em hot path = warning/blocking

**Origem.** `unity-extension.md` — "Repeated GetComponent without caching".

**PASS.**
```csharp
private Rigidbody _rb;
private void Awake() { _rb = GetComponent<Rigidbody>(); }
private void FixedUpdate() { _rb.AddForce(...); }
```

**FAIL.**
```csharp
private void FixedUpdate()
{
    GetComponent<Rigidbody>().AddForce(...); // busca a cada frame
}
```

**Aplicação multiplayer.** Hoje, **49 ocorrências de `GetComponent<>` em 12 arquivos**, todas em `Awake`, `OnNetworkSpawn`, ou métodos de inicialização. Nenhuma em hot path. Estado: OK.

**Consequência.** Sprint que adicionar `GetComponent` em `Update` precisa migrar para cache em `Awake`.

### 5.3 `Instantiate` / `Destroy` em loops sem pooling = warning/blocking

**Origem.** `unity-extension.md`.

**PASS.**
```csharp
foreach (Transform t in lobbyListContent) Destroy(t.gameObject); // ok: 1x por trigger
```

**FAIL.**
```csharp
private void Update()
{
    foreach (var enemy in enemies)
        Destroy(Instantiate(particlePrefab)); // VFX por frame sem pool
}
```

**Aplicação multiplayer.** Limpo. `GlobalVFXPool` é usado em `NetworkedEnemy.OnEnemyDiedClientRpc` para evitar alocação.

### 5.4 `new List<>()`, string concat, LINQ em `Update` = warning

**Origem.** `unity-extension.md` — alocações por frame disparam GC.

**PASS.**
```csharp
private readonly List<Transform> _scratch = new List<Transform>();
private void Update()
{
    _scratch.Clear();
    PlayerRegistry.CollectValidPlayerTransforms(_scratch);
    // usa _scratch
}
```

**FAIL.**
```csharp
private void Update()
{
    var nearby = players.Where(p => Vector3.Distance(transform.position, p.transform.position) < 10f).ToList();
    Debug.Log("Nearby: " + nearby.Count);  // string concat + LINQ + new List
}
```

**Aplicação multiplayer.** Hot paths atuais (`MatchManager.Update`, `EOSManagerWrapper.Update`, `NetworkedCubeMovement.Update`) **não alocam por frame**.

### 5.5 `Resources.Load` em runtime-critical paths = warning

**Origem.** `unity-extension.md`.

**Aplicação multiplayer.** `EOSManagerWrapper.EnsureEOSConfigLoaded` faz `Resources.Load<EOSConfig>("EOSConfig_Main")` — mas só em init. OK.

### 5.6 `using UnityEditor;` em scripts não-Editor = blocking

**Origem.** `unity-extension.md` — quebra standalone builds.

**Verificação.**
```powershell
Get-ChildItem -Recurse -Filter *.cs -Path Assets |
  Where-Object { $_.FullName -notmatch '\\Editor\\' } |
  Select-String -Pattern '^using UnityEditor' |
  Select-Object Path
```

**Aplicação multiplayer.** Zero violações hoje.

**Consequência.** Build standalone quebra silenciosamente. Bloqueia merge.

---

## 6. Critérios de qualidade NGO (project-specific)

Estes critérios não estão no Quality Gate base, mas são **regras internas do projeto** para evitar regressões documentadas.

### 6.1 "REGRA DE OURO NGO": `IsServer` herdado vs `NetworkManager.Singleton.IsServer`

**Conceito.** Em `NetworkBehaviour`, a property `IsServer` é `IsSpawned && NetworkManager.Singleton.IsServer`. Antes de `Spawn()`, retorna **false** mesmo no servidor.

**PASS.**
```csharp
public void InitializeTowerServer(ulong builderClientId, int charIndex, int cost)
{
    // Método chamado ANTES de Spawn() — usar NetworkManager.Singleton.IsServer
    if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
    BuilderClientId.Value = builderClientId;  // NetworkVariable write OK pré-Spawn
    // ...
}
```

**FAIL.**
```csharp
public void InitializeTowerServer(ulong builderClientId, ...)
{
    if (!IsServer) return; // FALSE no servidor pré-Spawn → falso negativo
    BuilderClientId.Value = builderClientId; // nunca executa
    // resultado: tower nasce com defaults zerados em todos clientes
}
```

**Aplicação multiplayer.** Documentado em `NetworkedTrapVisual.cs:73-78` e `NetworkedBuilding.cs:84-87` com comentário "REGRA DE OURO NGO".

**Consequência.** Reintrodução do bug de "torre/trap com defaults zerados em cliente". Rollback imediato.

### 6.2 Subscribe / Unsubscribe simétricos em NetworkVariable hooks

**PASS.**
```csharp
public override void OnNetworkSpawn()
{
    NetworkHealth.OnValueChanged += OnHealthChanged;
}

public override void OnNetworkDespawn()
{
    NetworkHealth.OnValueChanged -= OnHealthChanged;
    base.OnNetworkDespawn();
}
```

**FAIL.**
```csharp
public override void OnNetworkSpawn()
{
    NetworkHealth.OnValueChanged += OnHealthChanged;
}
// ❌ Sem OnNetworkDespawn → handler vaza, GC trava NetworkObject
```

**Aplicação multiplayer.** Convenção seguida em todos os `Networked*.cs`. Pattern: cada Subscribe tem seu Unsubscribe correspondente.

### 6.3 EOS LobbyDetails handle requer `Release()`

**Conceito.** EOS é SDK C nativo via P/Invoke. Handles vazam memória nativa se não forem liberados.

**PASS.**
```csharp
var copyResult = lobbyInterface.CopyLobbyDetailsHandle(ref opts, out var details);
try
{
    ProcessLobbyAttributes(details);
}
finally
{
    details.Release();
}
```

**FAIL.**
```csharp
var copyResult = lobbyInterface.CopyLobbyDetailsHandle(ref opts, out var details);
ProcessLobbyAttributes(details); // se lançar exception, details vaza
details.Release();
```

**Aplicação multiplayer.** Convenção rigorosa em `LobbyManager.cs`. Os comentários `A1 audit` documentam guards anti-leak.

### 6.4 ServerRpc com `RequireOwnership = false` para mensagens cross-client

**Conceito.** Por padrão, ServerRpc só pode ser chamado pelo owner. Para "qualquer cliente atacar este inimigo", precisa de `RequireOwnership = false`.

**PASS.**
```csharp
[ServerRpc(RequireOwnership = false)]
public void TakeDamageServerRpc(float damage, ulong attackerId, ServerRpcParams rpcParams = default)
{
    // qualquer cliente pode chamar; o servidor valida pelo SenderClientId
}
```

**FAIL.**
```csharp
[ServerRpc]
public void TakeDamageServerRpc(float damage, ...) { ... }
// Cliente atacando inimigo (que não é "owned") recebe warning + RPC ignorado
```

**Aplicação multiplayer.** Seguido em `NetworkedEnemy.TakeDamageServerRpc`, `NetworkedBuilding.RequestUpgradeServerRpc`, `NetworkedTrapVisual.RequestSellServerRpc`.

### 6.5 `Debug.Log` em hot path sob `#if UNITY_EDITOR`

**Conceito.** `Debug.Log` em produção:
- Aloca string GC.
- Escreve no Player.log (I/O).
- Em build, polui o log do usuário sem benefício.

**PASS.**
```csharp
private void OnHealthChanged(float oldValue, float newValue)
{
#if UNITY_EDITOR
    Debug.Log($"[NetworkedPlayerController] Vida: {oldValue} -> {newValue}");
#endif
}
```

**FAIL.**
```csharp
private void OnHealthChanged(float oldValue, float newValue)
{
    Debug.Log($"[NetworkedPlayerController] Vida: {oldValue} -> {newValue}"); // toda mudança em build
}
```

**Aplicação multiplayer.** Padrão consistente em `NetworkedPlayerController.cs:42-95`. `MatchManager.cs:82-98` usa o mesmo padrão para timer logs.

---

## 7. Critérios de arquitetura

### 7.1 Responsabilidades separadas (Single Responsibility Principle)

**Origem.** `CODE_QUALITY_CRITERIA.md` — "responsabilidades separadas".

**PASS.**
- `PlayerIdentityBridge.cs` — 83 LOC, faz uma coisa: bridge NGO ↔ EOS via ServerRpc.
- `CharacterChoiceCache.cs` — 57 LOC, faz uma coisa: cache estático de char index.

**FAIL.**
- `LobbyManager.cs` — 1626 LOC, 12 responsabilidades distintas.

**Aplicação multiplayer.** Sprints 3, 4, 5 quebram `LobbyManager` em classes coesas.

### 7.2 Reusar padrões existentes antes de criar abstrações

**Origem.** `CODE_QUALITY_CRITERIA.md` — "Reusar padrões existentes do projeto antes de criar novas abstrações."

**PASS.** Antes de criar uma classe nova, busque:
- Já existe um helper estático? (ex.: `NetworkGameplayResolver`, `PartySlotLayout`)
- Já existe um singleton com a responsabilidade adjacente?
- A mesma lógica já está implementada em outro lugar? (ex.: bootstrap NGO em 4 lugares)

**FAIL.** Sprint cria `MyNewHelper` quando `NetworkGameplayResolver` já cobre o caso.

**Aplicação multiplayer.** Esta regra é o motivo das sprints de consolidação. Antes de extrair `MatchSessionLauncher`, **leia o que já existe** em `NetworkBootstrap` e `HostManager` para não duplicar.

### 7.3 Singleton com auto-create ≠ recomendado

**Conceito.** O pattern atual no projeto:
```csharp
public static MyManager Instance
{
    get
    {
        if (_instance == null)
        {
            var go = new GameObject("MyManager");
            _instance = go.AddComponent<MyManager>();
        }
        return _instance;
    }
}
```

**Problema:** mascara erros de ordem de execução. Em teste, um cenário com `Instance` chamado antes de scene-setup cria um GameObject "fantasma" sem dependências corretas.

**PASS (sugestão futura).**
```csharp
public static MyManager Instance => _instance; // pode ser null
public static MyManager TryGetExistingInstance() => _instance;
public static bool HasInstance => _instance != null;

// Configuração: GameObject "MultiplayerCore" com [DefaultExecutionOrder(-100)] + DDOL
```

**Aplicação multiplayer.** Sprint 8 (opcional) propõe migrar `LobbyManager`, `EOSAuthenticator`, `EOSManagerWrapper`, `SessionManager`. Risco médio.

---

## 8. Critérios de robustez

### 8.1 Idempotência em fluxos críticos

**Origem.** `CODE_QUALITY_CRITERIA.md` — "Fluxos críticos precisam ser idempotentes, transacionais quando há escrita composta e explícitos sobre conflitos de negócio".

**PASS.**
```csharp
public void CriarSala() {
    if (_isCreatingLobby) {
        Debug.LogError("...");
        return;
    }
    _isCreatingLobby = true;
    // ...
}

public void LoginWithDeviceId() {
    if (isLoggedIn || _loginInProgress) {
        Debug.LogWarning("...");
        return;
    }
    _loginInProgress = true;
    // ...
}
```

**FAIL.**
```csharp
public void CriarSala() {
    _lobby.CreateLobby(...); // chamado 2x rapidamente → 2 lobbies criados
}
```

**Aplicação multiplayer.** `LobbyManager.SetMemberAttribute` e `LobbySceneUI.CriarSala` têm guards de re-entrada. **Manter.**

### 8.2 Mensagens de erro propagadas (sem perda silenciosa)

**Origem.** `CODE_QUALITY_CRITERIA.md` — "sem perda silenciosa de dados".

**PASS.**
```csharp
if (lobbyInterface == null)
{
    Debug.LogWarning("[LobbyManager] SetMemberAttribute abortado: EOS nao inicializado");
    OnError?.Invoke($"Nao foi possivel sincronizar '{key}': EOS nao inicializado"); // UI vê
    return;
}
```

**FAIL.**
```csharp
if (lobbyInterface == null) return; // usuário não fica sabendo
```

**Aplicação multiplayer.** Documentado em comentário `A5 audit` no `LobbyManager.cs`: "todos os caminhos de falha agora propagam OnError para UI".

### 8.3 Audit trail em operações sensíveis

**Origem.** `CODE_QUALITY_CRITERIA.md` — "Operações financeiras, estoque, reservas e sync devem preservar trilha de auditoria".

**Aplicação multiplayer (operações sensíveis):**
- `CreateLobby/JoinLobby/LeaveLobby` — todos com `Debug.Log` por etapa.
- `StartMatch` — `Debug.Log` em cada decisão (Relay vs IP, esperando clientes).
- `SetMemberAttribute` — log do par key=value.
- `OnNgoConnectionApproval` — log de `clientId | payloadSize | charIndex`.

**Consequência se ausente.** Bug fica indebogável. Sempre logar transições de estado.

### 8.4 Try/finally em handles nativos / coroutines

**Origem.** Boa prática para SDK C-based (EOS, Relay) e para guard anti-leak em fluxos com Coroutines.

**PASS.** Já demonstrado em §6.3.

---

## 9. Critérios de testes e cobertura

### 9.1 Cobertura — ratchet

**Origem.** `quality-rules.md` — coverage não pode regredir contra baseline.

**Aplicação multiplayer.** **Não há testes C# automatizados** no projeto hoje. Coverage do `.cs` é 0%. Baseline aceita 0%.

**Consequência.**
- Sprint não precisa adicionar testes para passar no gate.
- Mas sprint que **remove** algum smoke test pré-existente (não há) reprovaria.
- **Recomendação fora de escopo:** PlayMode tests para fluxo end-to-end (separado desta rodada de refatoração).

### 9.2 Smoke test manual obrigatório

**Conceito.** Embora não automatizado, **smoke test em MPPM é critério de aceitação manual** de cada sprint.

**Aplicação multiplayer.** Definido em §1.2 do `00_LEIA_PRIMEIRO.md`. Cada sprint termina com smoke test verde.

---

## 10. Critérios de segurança e dependências

### 10.1 Vulnerabilidades críticas em dependências = blocking

**Origem.** `quality-rules.md` — npm audit.

**Aplicação multiplayer.** Não rodamos `npm audit` no PI3D (não é projeto Node). Equivalente seria validar `Packages/manifest.json` em busca de CVE conhecidas — não automatizado.

### 10.2 Não commitar secrets

**Aplicação multiplayer.** Critério crítico após refactor de 13 Maio 2026:
- `EOSConfig.cs` usa `[NonSerialized]` em campos de credencial.
- Credenciais leem de env vars (`EOS_CLIENT_ID`, `EOS_CLIENT_SECRET`, `EOS_PRODUCT_ID`).
- `EOSCredentials.json` está no `.gitignore`.

**PASS.** Field initialization preservada.
**FAIL.** Remover `[NonSerialized]` ou comitar `EOSCredentials.json`. Rollback imediato + invalidação de credenciais.

### 10.3 Não dar autoridade de merge a IA

**Origem.** `SKILL.md` — "Do not give AI authority to merge, approve, or reject PRs."

**Aplicação multiplayer.** O orquestrador (humano) é o único que faz merge. Agentes preparam PR; humano revisa e aprova.

---

## 11. Critérios de lint

### 11.1 Lint errors não podem aumentar

**Origem.** `quality-rules.md` — sempre bloqueia.

**Aplicação multiplayer.** Não há ESLint para `.cs`. Equivalente: **warnings de compilação do Unity**.

**Verificação.** Build do Editor → checar console. Warnings novos = reprova.

**PASS.** Build sem warnings novos após mudança.

**FAIL.** Refatoração introduz `CS0414: campo não usado` ou `CS0649: campo nunca atribuído`.

### 11.2 Lint warnings — warning (não bloqueia, mas ratchet)

**Aplicação multiplayer.** Mesma regra para warnings do Unity. Não devem aumentar.

---

## 12. Critérios de organização do projeto

### 12.1 Docs ativos vs históricos

**Origem.** `CODE_QUALITY_CRITERIA.md` — "Documentação ativa fica em `docs/`; histórico de sprints fica em `docs/sprints/`."

**Aplicação multiplayer.**
- Docs ativos: `Assets/Codigo/Docs/Estado_Atual_Multiplayer.md`, `Assets/Codigo/Multiplayer/README.md`, `docs/Refactoring/*.md` (esta pasta).
- Histórico: `docs/development/archive/*.md` (já existente).
- **Esta refatoração** adiciona em `docs/Refactoring/` — separada de `docs/development/`.

### 12.2 Raiz limpa

**Origem.** `CODE_QUALITY_CRITERIA.md` — "A raiz deve mostrar uma estrutura limpa".

**Aplicação multiplayer.** Não criar arquivos `.md` na raiz do PI3D. Tudo em `docs/Refactoring/` ou em `Assets/Codigo/Docs/`.

---

## 13. Resumo: o que pode bloquear vs avisar

### Bloqueia merge (severity: blocking)

- Build do Unity falha (compilation error).
- Smoke test em MPPM falha (host+client não conectam, ou cena não carrega, ou avatares não aparecem).
- Arquivo novo > 800 LOC.
- Arquivo existente oversized cresceu.
- Nova ocorrência de `FindObjectOfType` em hot path.
- Novo `using UnityEditor;` em script não-Editor.
- Nova violação da "REGRA DE OURO NGO".
- Vulnerabilidade crítica em dependência.
- Lint error novo (warning de compilação Unity).
- Baseline atualizado em PR feature.
- Secret commitado (`.json` com credenciais).
- Quebra de contrato de interface (ver `04_CONTRATOS_INTERFACE.md`).
- Reintrodução de bug histórico listado em `00_LEIA_PRIMEIRO.md` §2.3.

### Aviso (severity: warning)

- Novo arquivo > 500 LOC.
- Função > 80 LOC.
- Complexidade ciclomática > 12 em função nova.
- `GetComponent<>` em método com dúvida sobre se é hot path.
- `Debug.Log` em hot path sem `#if UNITY_EDITOR`.
- Coroutine sem `try/finally` em torno de handle nativo.

### Info (sem ação requerida)

- Arquivo > 500 LOC pré-existente que não cresceu (legacy stable).
- Comentário com tag `audit` / `OPTIMIZATION` adicionado para preservar contexto histórico.

---

## 14. Como ativar o Quality Gate em `.cs` (futuro)

> Esta seção é informativa. **Não execute durante refatoração.** Quem ativa o gate é decisão fora de escopo.

O coletor de file-size do Quality Gate **já aceita** `*.cs` (config em `quality-gate.config.cjs` inclui `Assets/**/*.cs`). Para gerar o primeiro baseline:

```powershell
cd C:\Users\zegil\Documents\GitHub\mestre_darmas\hotel-fazenda-system

# Gerar relatório file-size apontando para o PI3D
node scripts/quality/quality-gate.js `
  --files-only `
  --include "../../ExoBeasts_V3/PI3D/Assets/Codigo/Multiplayer/**/*.cs"

# Inspecionar reports/quality-gate.json
# Após aceitação, persistir como baseline:
node scripts/quality/quality-gate.js --baseline ...
```

Mas, repete-se: **não fazer isso na refatoração**. O agente de refatoração só consome os critérios qualitativos deste documento.

---

**Fim do `01_QUALITY_GATE.md`.**
