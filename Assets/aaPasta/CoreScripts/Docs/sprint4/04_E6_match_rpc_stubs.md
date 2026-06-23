# Sprint 4 — Item E6: MatchManager ClientRpc Stubs Vazios

> **Tempo estimado**: ~5 minutos. **Risco**: 🟢 Baixo. **Pré-requisitos**: nenhum.
> **Pré-leitura**: `01_padroes.md`.

## Contexto

`MatchManager.cs` declara dois ClientRpcs que disparam pacotes pela rede mas têm **corpos vazios**:

```csharp
// Linha ~163
[ClientRpc]
private void OnMatchStartingClientRpc()
{
    // Stub: adicionar countdown visual ou SFX aqui
}

// Linha ~169
[ClientRpc]
private void OnMatchEndedClientRpc(bool victory)
{
    // Stub: adicionar efeito visual/sonoro de fim de partida aqui
}
```

E são chamados em:
```csharp
// Linha ~123: OnMatchStartingClientRpc();
// Linha ~152: OnMatchEndedClientRpc(true);  // vitória
// Linha ~159: OnMatchEndedClientRpc(false); // derrota
```

**Por quê isso importa**:
- Cada ClientRpc, mesmo com corpo vazio, gera um pacote de rede para todos os clientes (~24 bytes header + 1 byte payload).
- 4 clientes × 1 chamada = ~96 bytes de pure waste.
- Custo absoluto é trivial — esse item é **mais sobre limpeza de código** do que economia de banda.
- **Mas**: ClientRpcs vazios poluem Network Profiler logs e dão impressão errada do que está acontecendo no jogo.

## Objetivo

Decidir: **remover** os stubs (e suas chamadas) OU **implementar** countdown/efeitos. Esta sprint não tem assets prontos para implementação visual — recomenda-se **remover**.

## Investigação prévia (obrigatória)

### 1. Ler o método e confirmar stubs

```
Read: Assets/Codigo/Multiplayer/GameServer/MatchManager.cs (offset 115, limit 75)
```

Confirmar:
- Os dois ClientRpcs estão genuinamente vazios (só comentário)
- As chamadas vêm de `StartMatch`, `EndMatchVictory`, `EndMatchDefeat`

### 2. Verificar se algum cliente DEPENDE deles

```
Grep: pattern="OnMatchStartingClientRpc\|OnMatchEndedClientRpc" path="Assets/Codigo"
```

Esperar encontrar **apenas** as definições e chamadas em `MatchManager.cs`. Se aparecer em outro arquivo:
- Pode haver `partial class` ou herança que estende
- **Abortar e reportar** — pode haver implementação em arquivo separado

### 3. Verificar se há listener de evento equivalente

```
Grep: pattern="OnMatchStarted\|OnMatchEnded\|OnMatchStarting" path="Assets/Codigo"
```

Pode haver eventos `Action<>` que clientes assinam para reagir ao estado. Se sim, esses eventos cobrem o que os stubs poderiam fazer (e provavelmente são o caminho certo de UX).

Cross-check com:
```
Grep: pattern="CurrentMatchState.OnValueChanged" path="Assets/Codigo"
```

Se o estado é via NetworkVariable (`CurrentMatchState`) com `OnValueChanged`, então os ClientRpcs são **completamente redundantes** — clientes já reagem ao mudança da NetworkVariable.

## Plano de mudança (Opção Recomendada)

### Mudança em `MatchManager.cs`

**Remover os dois ClientRpcs e suas 3 chamadas**.

#### 1. Localizar e remover os métodos (linhas ~162-172)

**Antes**:
```csharp
[ClientRpc]
private void OnMatchStartingClientRpc()
{
    // Stub: adicionar countdown visual ou SFX aqui
}

[ClientRpc]
private void OnMatchEndedClientRpc(bool victory)
{
    // Stub: adicionar efeito visual/sonoro de fim de partida aqui
}
```

**Depois**:
```csharp
// OPTIMIZATION (Sprint 4 / Item E6 - 2026-MM-DD): ClientRpc stubs removidos.
// Antes: 3 ClientRpcs vazios disparados por partida (start + end victory/defeat) -
// pacotes inuteis no Network Profiler.
// Agora: clientes reagem a CurrentMatchState.OnValueChanged em OnMatchStateChanged.
// Sem isso: ruido em logs + ~96 bytes/partida desperdicados.
// Para reintroduzir countdown/SFX no futuro, assinar OnValueChanged em vez de criar ClientRpc:
//   CurrentMatchState.OnValueChanged += (oldState, newState) => { if (newState == MatchState.Playing) ShowCountdown(); }
```

#### 2. Remover as 3 chamadas

Localizar e remover:
- Linha ~123: `OnMatchStartingClientRpc();`
- Linha ~152: `OnMatchEndedClientRpc(true);`
- Linha ~159: `OnMatchEndedClientRpc(false);`

**Substituir por nada** (apenas remover a linha). O `CurrentMatchState.Value = ...` que está acima já dispara `OnValueChanged` em todos os clientes.

#### 3. Garantir que `OnMatchStateChanged` está assinado

```
Read: Assets/Codigo/Multiplayer/GameServer/MatchManager.cs (offset 170, limit 40)
```

Confirmar que `OnMatchStateChanged` está conectado:
```csharp
public override void OnNetworkSpawn() {
    base.OnNetworkSpawn();
    CurrentMatchState.OnValueChanged += OnMatchStateChanged;
}
```

Se NÃO estiver assinado:
- **Adicionar** a assinatura em OnNetworkSpawn + remoção em OnNetworkDespawn
- Caso contrário, mudanças de estado em clientes não serão notificadas (regressão)

```csharp
public override void OnNetworkSpawn()
{
    base.OnNetworkSpawn();
    CurrentMatchState.OnValueChanged += OnMatchStateChanged;
}

public override void OnNetworkDespawn()
{
    CurrentMatchState.OnValueChanged -= OnMatchStateChanged;
    base.OnNetworkDespawn();
}
```

Verificar se o método `OnMatchStateChanged` (linha 174 baseado no que já vimos) já existe e está vazio:
```csharp
private void OnMatchStateChanged(MatchState oldState, MatchState newState) { }
```

Se sim, deixar assim (pode ser ponto de extensão futura) OU remover se não for necessário.

## Plano de mudança (Opção Alternativa — Implementar de verdade)

NÃO recomendado nesta sprint pois requer assets visuais (UI, SFX). Se for feito:

```csharp
[ClientRpc]
private void OnMatchStartingClientRpc()
{
    // 3-2-1 countdown overlay
    if (CountdownUIManager.Instance != null)
        CountdownUIManager.Instance.StartCountdown(3);

    // Som de "match starting"
    if (UIAudioManager.Instance != null)
        UIAudioManager.Instance.PlayMatchStarting();
}
```

Mas: `CountdownUIManager` e `UIAudioManager` precisam existir e estar em scene. Verificar antes.

## Validação

### 1. Build limpo
```powershell
dotnet build PI3D.sln
```
Esperar `0 Erro(s)`.

### 2. Validação funcional

1. Editor → host inicia partida → cliente conecta
2. Iniciar partida → confirmar **estado de partida muda em ambos** (HUD/UI deve refletir)
3. Match terminar (vitória ou derrota) → confirmar tela de fim aparece em ambos
4. **Importante**: se algum cliente fica preso em estado "Lobby" mesmo após `CurrentMatchState.Value = Playing`, indica que:
   - `OnValueChanged` não está assinado
   - OU clientes não recebem o NetworkVariable (problema de NetworkObject)

Cross-check com `Grep: pattern="MatchState.Playing" path="Assets/Codigo"` — onde o estado é consumido por UI/sistemas? Eles reagem a NetworkVariable change ou esperavam o ClientRpc?

Se algum sistema dependia EXCLUSIVAMENTE do ClientRpc para reagir a fim de partida:
- **Substituir** por listener de `OnValueChanged`
- Reportar no relatório

### 3. Validação Network Profiler (se Unity Editor disponível)

- Antes do fix: 3 ClientRpcs visíveis em runs de partida (start + end)
- Depois do fix: 0 ClientRpcs de MatchManager (NetworkVariable change ainda visível, mas é dado real)

## Critérios de aceitação

- [ ] Build limpo (0 erros)
- [ ] 2 ClientRpc stubs removidos
- [ ] 3 chamadas de ClientRpcs removidas
- [ ] OnMatchStateChanged assinado em OnNetworkSpawn / desassinado em OnNetworkDespawn
- [ ] Estado de partida sincroniza corretamente entre host e cliente
- [ ] Comentário OPTIMIZATION presente

## Riscos e mitigação

| Risco | Probabilidade | Mitigação |
|---|---|---|
| Algum sistema externo dependia do ClientRpc | Baixa | Grep no item 2 da investigação cobre isso |
| `OnMatchStateChanged` não está assinado em algum cliente | Possível | Validação manual do passo 2 cobre |
| Future feature precisará dos ClientRpcs | Baixíssima | Comentário documenta como reintroduzir via OnValueChanged |

## Rollback

```powershell
git checkout Assets/Codigo/Multiplayer/GameServer/MatchManager.cs
```

## Reportar ao orquestrador (template)

```
Item: E6
Status: completed
Arquivos modificados: Assets/Codigo/Multiplayer/GameServer/MatchManager.cs
Build: PASS (0 erros, 52 warnings)
Validacao in-game: PASS (estado de partida sync OK em host+cliente) | NOT_RUN
Metrica medida: ClientRpcs por partida no MatchManager — antes: 3, depois: 0
Riscos detectados: nenhum
Proximo item liberado: true (A6 e a proxima onda)
Notas: OnMatchStateChanged ja estava assinado/desassinado corretamente nos OnNetworkSpawn/Despawn. Nenhum sistema externo dependia dos ClientRpcs (cross-check via Grep MatchState.Playing).
```

## Notas finais

Item mais rápido da Sprint 4. Se o agente levar mais de 15 minutos: provavelmente está tentando implementar countdown — **abortar e voltar à recomendação de remover**.

Princípio aplicado: **NetworkVariable + OnValueChanged é melhor que ClientRpc** quando o objetivo é "reagir a mudança de estado". RPCs são para **comandos pontuais** (ex: "instanciar VFX agora").
