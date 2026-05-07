# Padrões Compartilhados — Sprint 3

> **LEITURA OBRIGATÓRIA antes de iniciar qualquer item.** Este arquivo carrega o
> contexto completo necessário para agentes Opus 4.7 trabalharem sem assumir
> conhecimento da conversa orquestradora.

## Sumário do projeto (estado atual)

- Unity 6 (6000.0.52f1), NGO 1.12.0, Unity Transport 2.4.0
- Modelo P2P com Host (max 4 jogadores), EOS para matchmaking, Unity Relay
- Branch principal: `main`
- Path do projeto: `C:\Users\zegil\Documents\GitHub\ExoBeasts_V3\PI3D\`
- Solution: `PI3D.sln` (na raiz do projeto)

## Como compilar e validar build

```powershell
# Da raiz do projeto:
dotnet build PI3D.sln
```

**Critério**: `0 Erro(s)`. 52 `Aviso(s)` são pré-existentes (FMOD obsolete, Object.FindObjectOfType obsolete, hide warnings de OnDestroy, unused fields). NÃO introduzir warnings novos.

## Como validar in-game (quando aplicável)

1. Abrir Unity Editor (já está rodando se Unity MCP responde).
2. Abrir cena `Assets/Scenes/CenaMapaTeste.unity`.
3. Iniciar Multiplayer Play Mode (MPPM) com 2-3 instâncias virtuais.
4. Network Profiler: `Window → Analysis → Profiler → Network`.
5. Para itens de gameplay: spawnar wave de inimigos e medir.

## Ferramentas disponíveis para o agente

### Para edição de código
- `Read` / `Edit` / `Write` / `Grep` / `Glob` (padrão)
- `Bash` — para `dotnet build` e operações de filesystem

### Para inspecionar Unity (se Unity Editor está rodando)
- `mcp__UnityMCP__read_console` — checar erros/warnings de compilação Unity
- `mcp__UnityMCP__manage_asset` — buscar/inspecionar prefabs, materiais, etc.
- `mcp__UnityMCP__manage_prefabs` — modificar campos serializados de componentes em prefabs
- `mcp__UnityMCP__validate_script` — validação de script offline
- `mcp__UnityMCP__refresh_unity` — forçar recompilação Unity
- (Lista completa em `ToolSearch query="select:mcp__UnityMCP__..."`)

### Como saber se Unity está conectado
```
mcp__UnityMCP__read_console action="get" count="3"
```
Se retorna logs, Unity está rodando. Se retorna "No Unity Editor instances found",
Unity não está rodando — usar apenas ferramentas de filesystem (Read/Edit/Bash).

## Padrões NGO consolidados (USAR estes; não inventar novos)

### Padrão 1 — Acumulador local + threshold para NetworkVariable contínuo
Estabelecido em Sprints 1+2. Não escrever `netVar.Value += rate * Time.deltaTime`
em loop de Update. Acumular em campo local, publicar só em delta significativo.
Exemplos: `PlayerHealthSystem._pendingRegenAmount`, `MatchManager._matchTimeAccumulator`.

### Padrão 2 — Buffer estático para Physics NonAlloc
```csharp
private static readonly Collider[] _buffer = new Collider[64];
int count = Physics.OverlapSphereNonAlloc(pos, radius, _buffer);
for (int i = 0; i < count; i++) { /* ... */ }
```
Estático é seguro porque Unity Physics é single-threaded.
Exemplos: `EnemyController._targetingBuffer`, `TowerController._targetingBuffer`.

### Padrão 3 — Visual prediction + Server authority
Sistemas com presença visual em todos os clientes (torres atirando, projéteis cosméticos):
gate **apenas o caminho de dano** com `HasCombatAuthority()` ou `IsServer`. Manter
animator + tracer rodando localmente para suavidade visual. Exemplo: `TowerController.Shoot`.

### Padrão 4 — Spawn de NetworkObject com config replicada
Quando habilidade instancia NetworkObject + Spawn() em runtime:
- Setup() roda só no servidor — params NÃO chegam aos clientes via wire.
- **Soluções**: (a) `[SerializeField]` no PREFAB do logic — preenchido design-time;
  (b) `NetworkVariable<T>` para primitives + `OnValueChanged`; (c) `ClientRpc` broadcast
  pelo servidor pós-Spawn().
- Para `GameObject` references, só (a) funciona.

### Padrão 5 — ServerRpc chamado de dentro do servidor
- ServerRpc com `RequireOwnership = true` (default) **é REJEITADA** se chamada do
  servidor (clientId 0) em NetworkObject owned por cliente diferente.
- **Fix preferido**: rodar direto no servidor sem ServerRpc — `if (!IsServer) return;` no início.
- **Alternativa**: `[ServerRpc(RequireOwnership = false)]`.

### Padrão 6 — Visual server-controlled em entidades
Estado visual (`SetActive`, troca de material) chamado server-side NÃO propaga
automaticamente. **Padrão**: split em método server-only (lógica autoritativa) +
método público local (`ApplyXxxVisualLocal`) + ClientRpc broadcast em todos os clientes.
Exemplos: `EnemyController.SetAggroVisualLocal` + `NetworkedEnemy.SetAggroVisualClientRpc`.

### Padrão 7 — Dead-reckoning para timers
`UIManager.Update` extrapola `gameTime += Time.deltaTime` entre snapshots autoritativos
e corrige quando chega novo. Permite reduzir tick rate sem afetar UX.

### Padrão 8 — Owner-Proxy para habilidades sem NetworkObject
Quando `Ability.Activate()` usa `Instantiate` sem `Spawn()` (GameObject server-only),
o owner-cliente não vê VFX/SFX. Fix: `controller?.StartLocalXxxOwnerProxy(params)`
em `CommanderAbilityController.cs`. Ver implementações existentes (Mergulho Tinta,
Aqui Não, Postura Baluarte, Bomba Spray).

## Convenções de código (OBRIGATÓRIAS)

### Comentários em mudanças de fix/optimization
Todo bloco modificado deve ter um header curto explicando POR QUÊ. Formato:
```csharp
// OPTIMIZATION (Sprint 3 / Item <ID> - <data>): <explicacao curta>
// Antes: <comportamento antigo>
// Agora: <comportamento novo>
// Sem isso: <consequencia que estava acontecendo>
```

Exemplo real do Sprint 1:
```csharp
// Acumulador local de regen — só escreve currentHealth.Value quando acumular >=1 HP.
// Sem isso, escrever todo frame em NetworkVariable enviava ~30-60 deltas/s por jogador
// durante toda a regeneracao (dezenas de KB/s desperdiçados em variacao invisível).
private float _pendingRegenAmount;
```

### Nomenclatura
- Campos privados: `_camelCase` (com underscore prefixo) — convenção do projeto
- Métodos públicos: `PascalCase`
- NetworkVariable público: `PascalCase` (ex: `MatchTime`, `NetworkHealth`)
- Coroutines: `XxxCoroutine` ou `XxxRoutine`
- ClientRpc: `XxxClientRpc` (NGO obriga sufixo `ClientRpc`)
- ServerRpc: `XxxServerRpc` (NGO obriga sufixo `ServerRpc`)

### Sem emojis em código (preservar comportamento existente do projeto)
O projeto usa caracteres Unicode em alguns comentários (ex: `▸`, `─`) mas evita emojis
modernos. Não introduzir.

### Localização (idioma)
Comentários novos em **português brasileiro** (consistente com o projeto).
Identificadores em inglês (já é a convenção). Mensagens de Debug.Log podem ser em
português (já é a convenção).

## Arquivos frágeis — INTOCÁVEIS sem aprovação explícita do orquestrador

Modificar estes arquivos requer confirmação porque têm histórico de bugs sutis:

| Arquivo | Motivo |
|---|---|
| `Assets/Codigo/Multiplayer/Sync/PlayerNetworkSetup.cs` | Múltiplos bugs de host/client movement (ver `memory/bug_host_client_movement.md`). NÃO mexer na ordem de habilitar componentes em `FinishLocalSetupNextFrame`. |
| `Assets/Codigo/Multiplayer/Sync/ClientNetworkTransform.cs` | Owner-authoritative; mexer aqui pode quebrar movimento de todos os clientes. |
| `Assets/Codigo/Multiplayer/Lobby/LobbyManager.cs` (StartMatchCoroutine, OnLobbyAttributeUpdated) | Bug de StartHost/IsClient=True corrigido em Abril 2026 — esses dois métodos têm guards delicados. Mudanças permitidas: A3 (cooldown SearchLobbies). |
| `Assets/Codigo/Towers/BuildManager.cs` (placement de armadilhas + activeTrapCounts) | TOCTOU race corrigida em Maio 2026. |
| `Assets/Codigo/Multiplayer/Sync/NetworkedTrapVisual.cs` (InitializeServer) | Bug raiz IsServer pré-Spawn corrigido. |

Se item exigir mexer em arquivo dessa lista, **parar e perguntar** ao orquestrador.

## Diretiva geral de mudanças

- **Não mudar funcionalidade**, só performance/CPU/banda.
- **Preservar APIs públicas** (assinaturas de métodos, nomes de campos públicos)
  porque outros scripts/prefabs podem depender delas.
- **Adicionar, não remover** quando ambíguo: `[Obsolete]` em vez de delete.
- **Comentário > código limpo demais**: prefira deixar comentado quem chamou,
  por que removeu, etc. Outros agentes vão revisitar.

## Validação in-game — protocolo

Para itens que afetam gameplay (G3, E5, E3p2):

1. **Cenário base**: cena `CenaMapaTeste`, 1 host + 2 MPPM clones (3 jogadores total).
2. **Wave**: ajustar `HordeManager.enemiesPerHordeMax = 30` temporariamente (REVERTER após teste).
3. **Setup**: cada player constrói pelo menos 2 torres + 1 armadilha.
4. **Combate**: 60s de combate ativo. Capturar:
   - Network Profiler: bytes/s outbound do host
   - CPU Profiler: ms/frame em "Server Tick" do host
   - Snapshot de comportamento: jogadores se movem normalmente, inimigos detectam, abilities funcionam
5. **Repetir** após mudança e comparar.

Se Unity não está disponível para validação in-game (Unity Editor não está rodando),
marcar item como `Validação in-game: NOT_RUN` no sumário ao orquestrador. O orquestrador
fará a validação posteriormente.

## Como reportar dúvidas ao orquestrador

Antes de inventar uma solução: pergunte.

Casos típicos:
- "Existe ambiguidade no spec do item — qual interpretação correta?"
- "Encontrei outro bug enquanto investigava — devo corrigir nesta PR ou abrir TODO?"
- "Este refactor ficaria mais limpo se eu mexer em arquivo X (que está na lista de frágeis) — autoriza?"

Não use a tool `mcp__ccd_session__spawn_task` para isso (essa é para tarefas paralelas
desligadas). Use o canal de chat de orquestração.
