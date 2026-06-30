# Sprint 4 — Item V1: Auditoria de Camera Shake / UI Notification / Juice Manager

> **Tempo estimado**: ~1-2 horas. **Risco**: 🟡 Médio. **Pré-requisitos**: nenhum.
> **Pré-leitura**: `01_padroes.md` — em especial **Padrão 6 (Local-only feedback)**.

## Contexto

Três managers controlam feedback de UX/feel:

1. **`CameraShakeManager`** (ou similar) — balança câmera em eventos de impacto (hit, explosão, ult)
2. **`UINotificationManager`** — exibe notificações flutuantes ("+50 ouro", "Inimigo abatido", "Wave 3 começou")
3. **`JuiceManager`** — efeitos de "feel" gerais (slow-motion em kill, screen flash, hit-stop)

**Por quê isso importa**:
- Camera shake **NUNCA** deve ser broadcast: cada jogador deve sentir shake apenas quando ELE foi hitado, não quando outro jogador foi
- UI notification deve ser **local por evento**: cada cliente sabe quando ganhou ouro, não precisa servidor mandar pacote pra dizer
- JuiceManager: efeitos de tela inteira (slow-motion, color filter) afetam apenas o jogador que viveu o momento. Broadcast é desperdício + UX ruim (todos sentem slow quando você matou alguém)

Errôneamente broadcast desses managers gera:
- **Ruído de rede** (1 RPC por evento × 4 clientes)
- **UX ruim** (jogador A sente shake quando B leva dano = "lag" perceptível)
- **CPU desperdiçado** (clientes processam efeitos que não devem aplicar)

## Objetivo

Auditar como esses 3 managers são chamados:
- Se via ClientRpc broadcast: **refatorar para evento local**
- Se já são locais: **documentar como referência de boas práticas**
- Se há mistura (broadcast em alguns, local em outros): **uniformizar para local**

## Investigação prévia (obrigatória)

### 1. Localizar os 3 managers

```
Glob: pattern="**/CameraShake*.cs"
Glob: pattern="**/UINotification*.cs"
Glob: pattern="**/Juice*.cs"
Glob: pattern="**/CameraShakeManager.cs"
```

Anotar caminhos exatos. Ler cada um:
```
Read: <caminho>/CameraShakeManager.cs
Read: <caminho>/UINotificationManager.cs
Read: <caminho>/JuiceManager.cs
```

### 2. Verificar se herdam NetworkBehaviour

```
Grep: pattern="class CameraShakeManager.*:" path="Assets/Codigo"
Grep: pattern="class UINotificationManager.*:" path="Assets/Codigo"
Grep: pattern="class JuiceManager.*:" path="Assets/Codigo"
```

Se herdam de `NetworkBehaviour`: vermelho — provavelmente são chamados via ClientRpc.
Se herdam de `MonoBehaviour`: verde — são locais.

### 3. Mapear todas as chamadas

```
Grep: pattern="CameraShakeManager.Instance" path="Assets/Codigo" -n
Grep: pattern="UINotificationManager.Instance" path="Assets/Codigo" -n
Grep: pattern="JuiceManager.Instance" path="Assets/Codigo" -n
```

Para cada chamada, identificar contexto:
- É chamada do server (em ServerRpc handler)? → vermelho (broadcast)
- É chamada de ClientRpc? → vermelho (já está sendo broadcast)
- É chamada de Update local sem network context? → verde (local)
- É chamada em listener de NetworkVariable.OnValueChanged? → verde (local + reativo)

### 4. Identificar os trigger events

Para cada caso problemático identificado, encontrar o **evento ou estado de origem**:

| Manager call | Trigger original | Solução local |
|---|---|---|
| `CameraShake` em ClientRpc HitFeedback | dano tomado pelo player | `currentHealth.OnValueChanged` |
| `UINotification "+50 ouro"` em ClientRpc | inimigo morto | `EnemyEvents.OnEnemyKilled` (local event) |
| `JuiceManager.SlowMotion` em ClientRpc UltActivated | ult ativada pelo owner | `localPlayer.OnUltActivated` |

Se não houver evento equivalente, **criar um** — é parte do refactor.

## Plano de mudança

### Padrão de refactor

Para cada chamada problemática:

#### Antes (errado):
```csharp
// Server-side:
private void HandleEnemyKill(Transform killer, Enemy enemy) {
    int reward = enemy.goldReward;
    AddGoldToPlayer(killer, reward);
    NotifyKillRewardClientRpc(killer.GetComponent<NetworkObject>(), reward); // BROADCAST!
}

[ClientRpc]
private void NotifyKillRewardClientRpc(NetworkObjectReference killerRef, int reward) {
    if (UINotificationManager.Instance != null)
        UINotificationManager.Instance.ShowNotification($"+{reward} ouro");
}
```

#### Depois (correto):
```csharp
// Server-side:
private void HandleEnemyKill(Transform killer, Enemy enemy) {
    int reward = enemy.goldReward;
    AddGoldToPlayer(killer, reward);
    // NotifyKillRewardClientRpc removido — cada cliente ouve OnGoldChanged localmente.
}

// Cada cliente:
void OnEnable() {
    playerCurrency.OnValueChanged += OnGoldChanged;
}

private void OnGoldChanged(int oldGold, int newGold) {
    if (newGold > oldGold && UINotificationManager.Instance != null) {
        int delta = newGold - oldGold;
        UINotificationManager.Instance.ShowNotification($"+{delta} ouro");
    }
}
```

**Vantagens**:
- Zero RPCs (NetworkVariable já era atualizada)
- Cada cliente só vê SEU próprio ganho de ouro (não dos outros)
- Cliente "atrasado" (lag) recebe notificação no momento que NetworkVariable chegou — sincronizado com mudança real

### Refactor por caso

#### Caso A — Camera Shake broadcast em hit

**Identificar**: `ClientRpc` que chama `CameraShakeManager.Instance.Shake(...)` em response a damage.

**Refactor**:
1. Remover `[ClientRpc]` em ShakeOnHitClientRpc
2. Remover chamada do servidor
3. Localmente, no `PlayerHealthSystem` (cliente):
```csharp
public override void OnNetworkSpawn() {
    base.OnNetworkSpawn();
    currentHealth.OnValueChanged += OnHealthChangedLocal;
}

private void OnHealthChangedLocal(float oldValue, float newValue) {
    if (!IsOwner) return; // só o owner sente shake
    if (newValue < oldValue && CameraShakeManager.Instance != null) {
        float intensity = Mathf.Clamp01((oldValue - newValue) / characterData.maxHealth);
        CameraShakeManager.Instance.Shake(0.2f, intensity);
    }
}
```

#### Caso B — UI Notification broadcast em kill

**Identificar**: `ClientRpc` que chama `UINotificationManager.Instance.ShowNotification(...)`.

**Refactor**:
1. Cada cliente assina `EnemyEvents.OnEnemyKilled` (deve já existir)
2. Local handler:
```csharp
private void OnEnemyKilledLocal(Enemy enemy, ulong killerClientId) {
    if (killerClientId != NetworkManager.Singleton.LocalClientId) return;
    if (UINotificationManager.Instance != null)
        UINotificationManager.Instance.ShowNotification($"+{enemy.goldReward} ouro");
}
```

#### Caso C — Juice (slow-motion) broadcast

**Identificar**: `ClientRpc` que chama `JuiceManager.Instance.SlowMotion(...)`.

**Refactor**:
- Slow-motion afeta `Time.timeScale` — **NUNCA** deve ser broadcast (afetaria todos os jogadores!)
- Verificar se já é local (esperaria que sim por sanidade)
- Se for ClientRpc broadcast: **bug grave** — refatorar imediatamente para owner-only ou local

```csharp
// Local trigger:
private void OnLocalUltActivated() {
    if (!IsOwner) return;
    if (JuiceManager.Instance != null)
        JuiceManager.Instance.SlowMotion(0.5f, 0.3f); // 50% speed por 0.3s
}
```

#### Caso D — Already-local (nenhuma ação)

Se o manager já é chamado de contexto local (Update do cliente, OnValueChanged listener):
- **Documentar como referência** de boa prática no relatório
- Nenhuma mudança

## Validação

### 1. Build limpo
```powershell
dotnet build PI3D.sln
```

### 2. Validação funcional crítica

**Cenário Camera Shake**:
1. Editor + 1 cliente MPPM
2. Host inicia partida
3. **Cliente toma dano** → cliente sente shake, host NÃO sente
4. **Host toma dano** → host sente shake, cliente NÃO sente
5. Se ambos sentem shake quando um leva dano: **regressão**, voltar refactor

**Cenário UI Notification**:
1. Cliente mata inimigo → cliente vê "+50 ouro", host não vê
2. Host mata inimigo → host vê "+50 ouro", cliente não vê
3. **Importante**: Notificações de WAVE START / WAVE END devem aparecer para AMBOS (são globais)
   - Verificar que essas continuam funcionando
   - Se não funcionar, tem que **manter** broadcast desses casos específicos

**Cenário Slow-motion**:
1. Cliente ativa ult → cliente entra em slow, host NÃO
2. Host ativa ult → host entra em slow, cliente NÃO
3. **Crítico**: nunca os dois ao mesmo tempo

### 3. Validação Network Profiler

Esperar redução significativa em ClientRpcs durante combate ativo (potencial 20-30% se vários estavam errados).

## Critérios de aceitação

- [ ] Build limpo (0 erros)
- [ ] Audit dos 3 managers documentado no relatório
- [ ] Camera shake é local (passou validação cenário)
- [ ] UI notifications de jogador-específicas são locais; globais (wave) continuam broadcast
- [ ] Slow-motion / juice são locais
- [ ] Comentários OPTIMIZATION em cada refactor
- [ ] Sem regressão em UX (validações manuais passam)

## Riscos e mitigação

| Risco | Probabilidade | Mitigação |
|---|---|---|
| Notification global (Wave start) refatorada por engano para local | Média | Validação cenário cobre — se WAVE START não aparecer em todos, manter broadcast desse específico |
| Camera shake já era local — refactor desnecessário | Possível | Audit do passo 1 confirma antes de tocar |
| Cliente perde notificação por race entre NetworkVariable update e listener | Baixa | OnValueChanged é garantido pelo NGO; se houver race, listener filtrar |
| Slow-motion afeta todos por bug pré-existente | Possível | Se descoberto, é fix urgente, não polish |

## Rollback

Listar todos os arquivos modificados no audit:

```powershell
git diff --name-only
```

Para reverter um específico:
```powershell
git checkout <arquivo>
```

## Reportar ao orquestrador (template)

```
Item: V1
Status: completed | partial
Arquivos modificados: <lista>
Build: PASS (0 erros, 52 warnings)
Validacao in-game: PASS (cenarios shake/notification/slow-motion)

# Audit dos 3 managers:

## CameraShakeManager
Estado original: <broadcast | local | misturado>
Mudancas: <descricao>
Validacao: cliente sente shake apenas quando recebe dano — host nao sente

## UINotificationManager
Estado original: <broadcast | local | misturado>
Mudancas: <descricao>
Notificacoes player-especificas: agora locais via OnGoldChanged.OnValueChanged
Notificacoes globais (Wave): continuam broadcast (intencional)

## JuiceManager
Estado original: <broadcast | local | misturado>
Mudancas: <descricao>
Slow-motion: confirmado local; afeta apenas owner que ativou ult

Metrica medida: ClientRpcs de feedback em combate — antes: ~X/s, depois: ~Y/s
Riscos detectados: <lista ou nenhum>
Proximo item liberado: nenhum (Sprint 4 completa)
Notas: <ex: "WaveStartNotification mantido como broadcast intencional — afeta todos os jogadores">
```

## Notas finais

V1 é o item com **maior risco de regressão UX** da Sprint 4. Refatorar com cuidado:
- Cada mudança vale 1 ciclo de validação manual antes de prosseguir para próxima
- Se algo "parece estranho" em playtest mas funciona: documentar e deixar para Sprint 5

**Princípios fundamentais**:
1. **Camera shake = local** (regra de ouro)
2. **Slow-motion = local** (nunca afeta tempo dos outros)
3. **Notificações player-específicas = local via NetworkVariable.OnValueChanged**
4. **Notificações globais (wave start, match end) = broadcast OK**

Após V1, Sprint 4 está completa. **Próximo passo**: profilagem real e decidir Sprint 5.
