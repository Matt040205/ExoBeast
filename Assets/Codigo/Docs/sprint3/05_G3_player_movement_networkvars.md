# Sprint 3 — Item G3: PlayerMovement NetworkVariables Redundantes

> **Tempo estimado**: ~2-3 horas + bastante teste in-game.
> **Risco**: 🔴 ALTO. **Pré-requisitos**: A2 + A3 + E5 mergeados.
> **Pré-leitura OBRIGATÓRIA**: `01_padroes.md` + `memory/bug_host_client_movement.md`
> (se não acessível, pedir ao orquestrador um dump do conteúdo).

## ⚠️ AVISO CRÍTICO — zona historicamente frágil

`PlayerMovement.cs` tem histórico de bugs sutis envolvendo:
- Disputa de PlayerInput device-pairing entre prefabs do player e PlayerInput de cena
- Movimento do host quebrando após mudanças aparentemente inócuas
- Cliente não-host com WASD/Jump não respondendo

**Regras de ouro estabelecidas em sessões anteriores** (NÃO violar):

1. **NÃO mexer** em `PlayerNetworkSetup.FinishLocalSetupNextFrame` — está intocado.
2. **NÃO mexer** na ordem de habilitar/desabilitar componentes de input.
3. **NÃO mexer** em `PlayerNetworkSetup.SetupAsRemotePlayer` (já desabilita CC, etc.).
4. Em `FinishLocalSetupNextFrame`, qualquer chamada a `BuildManager`, `UIManager`,
   `TopDownCameraManager`, `TutorialManager` deve vir **DEPOIS** de
   `localInputBridge.enabled = true`.

Se alguma alteração necessária para G3 conflitar com essas regras: **abortar e
perguntar** ao orquestrador.

## Contexto

`PlayerMovement.cs` linhas 77-84 declara 4 `NetworkVariable` privadas:
```csharp
private NetworkVariable<float> netModelYRot = new NetworkVariable<float>(
    0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
private NetworkVariable<bool> netIsGrounded = new NetworkVariable<bool>(
    true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
private NetworkVariable<float> netMovementSpeed = new NetworkVariable<float>(
    0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
private NetworkVariable<float> netYVelocity = new NetworkVariable<float>(
    0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
```

**Análise**:
- `netModelYRot`: provavelmente REDUNDANTE com a rotação que `ClientNetworkTransform`
  já sincroniza para o GameObject pai. Confirmar inspecionando como modelPivot é
  usado e se sua rotação pode ser derivada da rotação do transform pai.
- `netIsGrounded` / `netMovementSpeed` / `netYVelocity`: usados para controlar Animator
  em remotos. Podem ser:
  - Empacotados em uma struct compacta (1 NetworkVariable de struct = 1 stream).
  - Derivados localmente em remotos (Animator anima via diferença de posição
    frame-a-frame, sem precisar de sync explícito).

**Custo atual**: 4 NetworkVariables × 4 jogadores = até 16 streams independentes
sincronizando dados de animação. Cada um é uma escrita de owner que vira pacote.

## Objetivo

Reduzir o número de NetworkVariables em `PlayerMovement` de 4 para 0 ou 1, sem
introduzir regressão de movimento ou animação.

**Estratégia escolhida**: implementação em **3 sub-fases isoladas com teste in-game
entre cada uma**. Cada sub-fase é um commit separado dentro da branch do item G3.

| Sub-fase | Mudança | Risco isolado |
|---|---|---|
| G3.1 | Remover `netModelYRot` (derivar do ClientNetworkTransform) | 🟡 Médio — depende da estrutura de modelPivot vs root |
| G3.2 | Remover `netIsGrounded` (derivar localmente em remotos via raycast) | 🟢 Baixo |
| G3.3 | Empacotar `netMovementSpeed` + `netYVelocity` em struct compacta OU derivar | 🟡 Médio |

**Stop entre sub-fases**: testar in-game com 3 jogadores antes de avançar.

## Investigação prévia (OBRIGATÓRIA — não pular)

### 1. Ler `PlayerMovement.cs` completo

```
Read: Assets/Codigo/Characters/Player/PlayerMovement.cs (sem offset/limit)
```

Anotar:
- Onde cada `netXxx.Value` é escrito (sempre apenas em IsOwner?)
- Onde cada `netXxx.Value` é lido (em remotos? em servidor?)
- Como `LateUpdate` aplica `netModelYRot` em remotos (linhas ~380-385)
- Se há subscriptions a `OnValueChanged` em algum lugar

### 2. Ler `ClientNetworkTransform.cs` e arquitetura de modelPivot

```
Read: Assets/Codigo/Multiplayer/Sync/ClientNetworkTransform.cs
```

Confirmar: ClientNetworkTransform sincroniza posição **e rotação** do TRANSFORM
do NetworkObject (raiz do prefab do player). O `modelPivot` é child desse root.

**Pergunta-chave**: a rotação do `modelPivot` (Y do modelo, controlado por
input WASD ou aim) é a MESMA que a rotação do root, ou é independente?

Se for INDEPENDENTE (modelPivot tem rotação local separada), `netModelYRot` é
necessário porque ClientNetworkTransform não sincroniza filhos.

Se modelPivot tem rotação local (offsetada) ao root, e o root é o que rotaciona,
então `netModelYRot` é redundante. Verificar inspecionando o prefab do player:

```
mcp__UnityMCP__manage_prefabs action="get_hierarchy" prefab_path="Assets/Modelos/PreFab/Entidades/Dragao.prefab"
```

Procurar onde `modelPivot` está e qual sua rotação local em design-time.

### 3. Ler consumers da rotação do modelPivot

```
Grep: pattern="modelPivot" path="Assets/Codigo"
```

Anotar onde `modelPivot.rotation` ou `modelPivot.eulerAngles` é lido, especialmente:
- `MeleeCombatSystem` — usa para `attackPoint`?
- `PlayerShooting` — usa para mira?
- Outros sistemas dependentes de orientação visual

### 4. Investigar Animator parameters

```
Grep: pattern="MovementSpeed|isGrounded|YVelocity" path="Assets/Codigo"
```

Confirmar:
- `Animator.SetFloat("MovementSpeed", ...)` é chamado APENAS no owner ou também em remotos?
- Existe lógica em `PlayerMovement.LateUpdate` para aplicar `netMovementSpeed.Value`
  ao `animator.SetFloat` em remotos?
- Se SIM (animator atualizado em remotos via NetworkVariable), removê-lo significa
  perder animação em remotos — caso em que precisa de derivação local.

### 5. Reportar achados antes de implementar

Postar para o orquestrador:
```
Investigação G3 concluída. Estado atual:
- modelPivot vs root: [INDEPENDENTE | offset-fixo | mesmo-transform]
- Consumers de modelPivot.rotation: [lista]
- Animator em remotos atualizado via netMovementSpeed: [SIM | NÃO]
- netIsGrounded usado em quê em remotos: [...]
- Plano sub-fase G3.1 ajustado: [...]
- Plano sub-fase G3.2 ajustado: [...]
- Plano sub-fase G3.3 ajustado: [...]
Solicito confirmação antes de implementar.
```

**Aguardar confirmação do orquestrador** antes de prosseguir.

## Plano de mudança — sub-fase G3.1 (`netModelYRot`)

> Aplicar APENAS após investigação confirmar que modelPivot pode derivar do root.

### Caso 1 — modelPivot tem rotação igual ao root (e independente seria zero)

Remover `netModelYRot` totalmente. Em `LateUpdate` para remotos, modelPivot
herda rotação do root (já sincronizado por ClientNetworkTransform).

```csharp
// Antes (linhas ~380-385):
if (!IsOwner)
{
    if (modelPivot != null)
        modelPivot.rotation = Quaternion.Euler(0f, netModelYRot.Value, 0f);
    return;
}

// Depois:
if (!IsOwner)
{
    // OPTIMIZATION (Sprint 3 / Item G3.1 - 2026-05-XX): netModelYRot removido — modelPivot
    // herda rotacao do root, que ja eh sincronizado pelo ClientNetworkTransform.
    return;
}
```

E remover a linha `netModelYRot.Value = angle;` na escrita do owner (~linha 400).

### Caso 2 — modelPivot tem rotação local offsetada

Manter `netModelYRot` por enquanto. Sub-fase G3.1 não aplicável; pular para G3.2.
Documentar achado.

### Caso 3 — outros sistemas dependem de modelPivot.rotation servida via netModelYRot

(ex: PlayerShooting precisa modelPivot apontando exato pra Cacadora Noturna em remotos)
— mais complexo. Reportar e perguntar.

### Validação intermediária — antes de continuar

**Teste obrigatório** (com 1 host + 2 MPPM clientes):

1. Cada player se move em direções diferentes (WASD).
2. **Validar nos 3 jogadores**: cada player VÊ os outros 2 olhando na direção
   correta enquanto se movem. Não há jitter, snap, ou rotação errada.
3. Cada player atira/ataca olhando em uma direção. **Validar**: ataque sai na
   direção correta nos 3 clientes (não há offset visível entre o que cada cliente
   vê do mesmo player).
4. Player local rotaciona câmera 360° lentamente. Outros clientes vêem rotação
   suave (sem teleporte de yaw).

Se algum teste falhar: rollback G3.1 e reportar.

## Plano de mudança — sub-fase G3.2 (`netIsGrounded`)

> Aplicar APENAS após G3.1 testado e mergeado.

`netIsGrounded` é provavelmente lido em remotos para Animator (transição
ground/air). Em vez de sincronizar via NetworkVariable, **derivar localmente**:

```csharp
// Em remotos, em LateUpdate ou Update:
if (!IsOwner)
{
    // OPTIMIZATION (Sprint 3 / Item G3.2 - 2026-05-XX): netIsGrounded removido. Cada cliente
    // calcula isGrounded local via raycast (CharacterController esta DESABILITADO em remotos
    // pois ClientNetworkTransform controla posicao, mas raycast nao precisa do CC).
    bool localGrounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.3f, groundMask);
    if (animator != null) animator.SetBool("isGrounded", localGrounded);
    return;
}
```

Importante:
- `groundMask` já existe como `[SerializeField] LayerMask groundMask;` no PlayerMovement.
- Raycast a 30Hz (LateUpdate) é barato.
- Owner continua escrevendo `animator.SetBool("isGrounded", ...)` no próprio caminho.

### Remoção do NetworkVariable

```csharp
// REMOVER:
private NetworkVariable<bool> netIsGrounded = new NetworkVariable<bool>(...);

// REMOVER toda escrita: netIsGrounded.Value = isGrounded;
```

### Validação intermediária

1. Player local pula/cai. Outros clientes vêem animação correta de pulo/queda.
2. Não há "saltinho" visual ao aterrissar (transição ground/air suave nos remotos).

## Plano de mudança — sub-fase G3.3 (`netMovementSpeed` + `netYVelocity`)

> Aplicar APENAS após G3.1 + G3.2 testados e mergeados.

Duas opções, escolher uma:

### Opção A — empacotar em struct compacta

```csharp
[System.Serializable]
public struct PlayerAnimState : INetworkSerializable
{
    public byte movementSpeedHalf; // 0..255 mapeado para 0..1
    public byte yVelocityHalf;     // 0..255 mapeado para -10..+10
    public bool isGrounded;        // 1 byte

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref movementSpeedHalf);
        serializer.SerializeValue(ref yVelocityHalf);
        serializer.SerializeValue(ref isGrounded);
    }
}

private NetworkVariable<PlayerAnimState> netAnimState = new NetworkVariable<PlayerAnimState>(
    default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
```

Reduz 4 streams para 1 stream + payload compacto. **Mas já estamos removendo isGrounded
em G3.2** — então struct fica só com 2 bytes.

### Opção B — derivar localmente (preferida se viável)

Em remotos, derivar `MovementSpeed` da diferença de posição entre frames:

```csharp
private Vector3 _previousPosition;
private float _smoothedSpeed;

void LateUpdate()
{
    if (!IsOwner)
    {
        // OPTIMIZATION (Sprint 3 / Item G3.3): derivar movement speed local em vez de sync.
        Vector3 delta = transform.position - _previousPosition;
        delta.y = 0f;
        float instantSpeed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        // Mapeia velocidade real para o range 0..1 que o Animator espera (igual owner).
        // Maximo ~runSpeed (8 m/s) → 1.0
        float normalizedSpeed = Mathf.Clamp01(instantSpeed / runSpeed);
        _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, normalizedSpeed, 10f * Time.deltaTime);
        if (animator != null) animator.SetFloat("MovementSpeed", _smoothedSpeed);
        _previousPosition = transform.position;
        return;
    }
    // ... resto do método para owner
}
```

`netYVelocity` provavelmente não precisa em remotos (cliente já faz raycast/animação
local — Animator de remoto não usa Y velocity para nada visível na maioria dos jogos).
**Confirmar na investigação se é usado em algum BlendTree**.

### Recomendação

**Tentar Opção B primeiro**. Se animação ficar ruim (jitter, atraso visível),
fallback para Opção A.

### Validação final

Cenário completo (após G3.1 + G3.2 + G3.3):
1. 3 jogadores movendo. Animação de corrida nos 3 clientes para todos os outros.
2. Aim + tiro: ataque vai na direção certa.
3. Pulo: animação correta nos 3 clientes.
4. Habilidades funcionam (Q/E/X em cada classe).
5. Não há jitter, snap, ou animação travada.

## Validação geral — checklist obrigatório

Após cada sub-fase E após o item completo:

### Build
- [ ] `dotnet build PI3D.sln` retorna 0 erros.
- [ ] 52 warnings, sem novos warnings.

### Validação in-game (3 jogadores MPPM)
- [ ] Movimento WASD funciona em host E em clientes.
- [ ] Pulo funciona em host E em clientes.
- [ ] Mira/aim funciona corretamente.
- [ ] Animação de corrida visível nos remotos.
- [ ] Animação de pulo visível nos remotos.
- [ ] Direção do ataque (melee + ranged) corresponde à direção visível em todos os clientes.
- [ ] Habilidades Q/E/X funcionam em cada classe (regression test contra fixes anteriores).
- [ ] Tiros do Coruja/Polvo continuam saindo na direção correta.

### Network Profiler
- [ ] Bytes/s outbound do host em movimento ativo: comparar com baseline.
- [ ] Esperar: redução proporcional ao número de NetworkVariables removidos.

## Riscos e mitigação

| Risco | Probabilidade | Mitigação |
|---|---|---|
| Movimento do host quebra | Média (zona frágil) | Não tocar em PlayerNetworkSetup.FinishLocalSetupNextFrame. Testar APÓS cada sub-fase. |
| Animação em remotos fica ruim (jitter) | Média | Smoothing com Lerp/SmoothDamp. Se persistir, fallback para Opção A. |
| Tiro/ataque vai na direção errada em remotos | Alta se modelPivot não corresponder | Investigação prévia OBRIGATÓRIA. Se Caso 3 da G3.1, NÃO remover netModelYRot. |
| Cliente não-host não consegue se mover (regressão histórica) | Média | Não mexer em SetupOwnerInputFallback. Validar com MPPM cliente após cada sub-fase. |

## Rollback

Cada sub-fase é um commit. Para reverter G3.X:
```powershell
git log --oneline    # achar SHA da sub-fase
git revert <sha>
```

Se quiser reverter G3 inteiro:
```powershell
git checkout main
```

## Reportar ao orquestrador (template — um por sub-fase)

```
Item: G3.<sub-fase, ex: G3.1>
Status: completed | aborted
Arquivos modificados: <lista>
Sub-fase: G3.1 / G3.2 / G3.3
Build: PASS (0 erros)
Validação in-game: PASS (todos os checks marcados) | FAIL (<qual check>)
Métrica medida (esta sub-fase): NetworkVariables removidas: <X>, streams sync por player: <antes/depois>
Riscos detectados: <lista>
Próxima sub-fase liberada: true | false (motivo)
Item G3 completo: false | true (após G3.3)
```

Final report após G3.3:
```
Item: G3 (todas as sub-fases)
Status: completed
Arquivos modificados: <lista total>
Build: PASS
Validação in-game: PASS
Métrica medida: 4 NetworkVariables → <0 | 1>; Bytes/s outbound em movimento: <antes> → <depois>
Riscos detectados: <lista>
Próximo item liberado: true (E3p2)
```
