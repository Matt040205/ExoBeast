# Sprint 4 — Padrões Compartilhados (LEITURA OBRIGATÓRIA)

> Este documento DEVE ser lido **integralmente** antes de iniciar QUALQUER item da Sprint 4.
> Tempo de leitura: ~10 minutos.

## Sumário do projeto

**ExoBeasts V3** — Tower Defense 3D + Rogue-like. P2P Host (host = server + client). Max 4 jogadores.

- **Engine**: Unity 6 (6000.0.52f1)
- **Networking**: Unity NGO 1.12.0
- **Transport**: Unity Transport 2.4.0
- **Lobby**: Epic Online Services (PlayEveryWare package)
- **Dev branch**: `main`
- **Solution**: `PI3D.sln` na raiz `C:/Users/zegil/Documents/GitHub/ExoBeasts_V3/PI3D/`

## Regras de ouro do projeto

### Arquivos FRÁGEIS — extrema cautela
Mexer nestes arquivos pode quebrar funcionalidades já estabilizadas. Em Sprint 4, **apenas A6 toca um destes**:

1. **`PlayerNetworkSetup.cs`** — sequência de habilitação de componentes em spawn. ❌ Não tocar nesta sprint.
2. **`PlayerMovement.cs`** (especificamente `FinishLocalSetupNextFrame`) — host bug recorrente. ❌ Não tocar nesta sprint.
3. **`LobbyManager.cs`** — bug histórico `StartHost falha com IsClient=True` (Abril 2026). 🟡 A6 toca APENAS `SetMemberAttribute` — método independente. Não tocar `StartMatchCoroutine` ou `OnLobbyAttributeUpdated`.
4. **`EOSManagerWrapper.cs`** — já modificado em Sprint 3. ❌ Não tocar nesta sprint.

### Padrões de comentário
Todo bloco modificado deve ter:
```csharp
// OPTIMIZATION (Sprint 4 / Item XX - 2026-MM-DD): <intencao em uma linha>
// Antes: <comportamento anterior>
// Agora: <novo comportamento>
// Sem isso: <consequencia que evitamos>
```

Exemplo real (de Sprint 3 / A2 que já está no código):
```csharp
// OPTIMIZATION (Sprint 3 / Item A2 - 2026-05-07): detectar se o PlayEveryWare EOSManager
// MonoBehaviour esta ativo + enabled. Se sim, ele ja chama Tick() no proprio Update.
// Antes: tick incondicional sempre que isInitialized.
// Agora: tick apenas quando o PEW esta ausente ou desabilitado.
// Sem isso: cada frame processava callbacks EOS duas vezes.
```

### Convenções de código

- **Acentos em comentários**: evitar (compatibilidade UTF-16/CRLF). Usar "execucao" em vez de "execução".
- **Verbosidade de log**: hot paths NUNCA com `Debug.Log` em release. Usar `#if UNITY_EDITOR`.
- **Alocações em hot paths**: zero. Usar buffers estáticos / cache de listas.
- **Acesso a singletons**: cachear referência local quando usado mais de 1x na função.
- **Early-return**: preferir cedo a aninhamento profundo.
- **Spans/structs em RPC**: para A6 (debounce), preferir `Coroutine` simples a `Task.Delay` (Unity threading).

## Padrões NGO usados no projeto

### Padrão 1 — Owner-only NetworkVariable
```csharp
private NetworkVariable<int> netAmmo = new NetworkVariable<int>(
    0,
    NetworkVariableReadPermission.Owner,    // só o dono recebe deltas
    NetworkVariableWritePermission.Server); // só servidor escreve
```
Aplicado em Sprint 1 (G4 — `NetworkAmmo`). UI de munição é exclusiva do owner.

### Padrão 2 — Acumulador local + threshold
Para variáveis que mudam continuamente (timer, regen, charge):
```csharp
private float _pendingDelta;

void Update() {
    if (!IsServer) return;
    _pendingDelta += rate * Time.deltaTime;
    if (_pendingDelta >= 1f) {
        float toApply = Mathf.Floor(_pendingDelta);
        _pendingDelta -= toApply;
        netVar.Value += toApply; // delta de rede só quando muda 1+ unidade
    }
}
```
Aplicado em Sprint 1 (G1, G2, A1).

### Padrão 3 — Debounce em coroutine (RELEVANTE PARA A6)
Para inputs de UI que disparam network calls:
```csharp
private Coroutine _debounceCoroutine;
private string _pendingValue;
private const float DEBOUNCE_DELAY = 0.25f;

public void RequestSetValue(string value) {
    _pendingValue = value;
    if (_debounceCoroutine != null) StopCoroutine(_debounceCoroutine);
    _debounceCoroutine = StartCoroutine(SubmitAfterDelay());
}

private IEnumerator SubmitAfterDelay() {
    yield return new WaitForSeconds(DEBOUNCE_DELAY);
    DoActualNetworkCall(_pendingValue);
    _debounceCoroutine = null;
}
```

### Padrão 4 — Cache de lista para iteração segura (RELEVANTE PARA G7)
Quando precisar iterar Dictionary modificando seu conteúdo:
```csharp
// ❌ Errado (alocação por frame):
foreach (var key in new List<TKey>(dict.Keys)) { ... }

// ✅ Correto (cache reutilizado):
private readonly List<TKey> _keysCache = new List<TKey>(8);

void Update() {
    _keysCache.Clear();
    _keysCache.AddRange(dict.Keys);
    foreach (var key in _keysCache) { ... }
}
```

### Padrão 5 — Owner-Proxy (referência, não vai ser usado em Sprint 4)
Já documentado em Sprint 3 / G3. Resumo: para abilities que `Instantiate` sem `Spawn()`, o owner-cliente roda proxy via `ClientRpcParams.Send.TargetClientIds = {OwnerClientId}`.

### Padrão 6 — Local-only feedback (RELEVANTE PARA E7 e V1)
Camera shake, partículas de hit, sfx de "+50 ouro" devem ser **locais por evento**:
```csharp
// ❌ Errado:
[ClientRpc] private void ShowDamageEffectClientRpc() { ... }

// ✅ Correto:
private void OnLocalDamageDealt(float dmg, Transform target) {
    // cada cliente assina o evento e dispara seu próprio shake/VFX
    CameraShake.Trigger(0.2f, 0.1f);
}
```

## Ferramentas disponíveis

### Build (terminal)
```powershell
dotnet build PI3D.sln
```
Esperar `0 Erro(s)`. 52 warnings pré-existentes são OK.

### Unity MCP (se disponível)
Verificar com:
```
mcp__UnityMCP__read_console action="get" count="10"
```

Se Unity Editor estiver rodando, usar:
- `mcp__UnityMCP__find_in_file` — buscar padrões em arquivos
- `mcp__UnityMCP__refresh_unity` — forçar reimport
- `mcp__UnityMCP__manage_editor action="play"` — entrar em play mode
- `mcp__UnityMCP__read_console` — ler logs após teste

### Network Profiler (se Unity Editor disponível)
1. Window → Analysis → Profiler
2. Aba "Network" (NGO instala automaticamente)
3. Comparar bytes/s antes/depois da mudança

## Estrutura de pastas relevante

```
Assets/Codigo/
├── Characters/
│   ├── AbilitySystem/CommanderAbilityController.cs    [G7]
│   ├── Player/PlayerHealthSystem.cs                   [G6]
│   ├── Coruja/                                        [E7 audit]
│   ├── Raposa/                                        [E7 audit]
│   ├── Dragao/                                        [E7 audit]
│   └── Polvo/                                         [E7 audit]
├── Multiplayer/
│   ├── GameServer/MatchManager.cs                     [E6]
│   └── Lobby/LobbyManager.cs                          [A6]
├── Managers/
│   ├── CameraShakeManager.cs                          [V1 audit]
│   ├── UINotificationManager.cs                       [V1 audit]
│   └── JuiceManager.cs                                [V1 audit]
└── Docs/sprint4/                                      [este diretório]
```

## Comportamento esperado em build sem erros

Se o build falhar com `dotnet build PI3D.sln`:
1. **Não tente continuar**. Reporte para o orquestrador.
2. Possíveis causas comuns:
   - Acento que virou caractere quebrado (CRLF/UTF-16)
   - Symbol renomeado por engano
   - `using` faltando (ex: `System.Collections` para `Coroutine`)
3. Rollback: `git checkout <arquivo>` no(s) arquivo(s) do seu item
4. Reportar o erro completo, não tentar fixes especulativos

## Como reportar conclusão

Cada agente ao terminar:

```
Item: G6 (exemplo)
Status: completed
Arquivos modificados: Assets/Codigo/Characters/Player/PlayerHealthSystem.cs
Build: PASS (0 erros, 52 warnings)
Validacao in-game: NOT_RUN (Unity Editor nao disponivel)
Metrica medida: TryResolveCharacterData chamadas/s — antes: ~60/jogador, depois: 0 apos resolver
Riscos detectados: nenhum
Proximo item liberado: true (G7 ou E6 - paralelos)
Notas: characterData resolveu em 1 frame em ambos os testes locais
```

## Lista de bugs corrigidos antes desta sprint (referência histórica)

Toda Sprint 4 pressupõe que estes bugs foram corrigidos e validados:
- Bug 1 (TemorSismico não replicava parâmetros para clientes)
- Bug 2 (Coruja Q/X não mostrava marcado em clientes)
- Bug 3 (PeaceOfMind ServerRpc rejeitado por ownership)
- Bug 4+5 (aggro indicator e enemy detection não chegavam em clientes)
- Bug 6 (loading screen prendia cliente em 2ª partida)
- Bug 7 (caminhos de torre)
- Bug 8 (efeito rosa de spawn em Coruja/Raposa)
- Bug 9 (Dragão atacava só na direção do spawn)

Ver `MEMORY.md` para detalhes históricos.
