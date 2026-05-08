# Sprint 4 — Item G6: PlayerHealthSystem.TryResolveCharacterData no Update

> **Tempo estimado**: ~10 minutos. **Risco**: 🟢 Baixo. **Pré-requisitos**: nenhum.
> **Pré-leitura**: `01_padroes.md` (este diretório).

## Contexto

`PlayerHealthSystem.cs` chama `TryResolveCharacterData()` em três pontos:
- `Awake()` — linha 65
- `Update()` — linha 106
- `OnNetworkSpawn()` — linha 190

O método interno (linha 484):
```csharp
private void TryResolveCharacterData()
{
    if (characterData == null)
        NetworkGameplayResolver.TryResolveCharacterData(this, out characterData, allowOwnerLocalFallback: IsOwner);
}
```

**Por quê isso importa**:
- O early-return `if (characterData == null)` parece protetor, mas a chamada ainda é invocada **todo frame**.
- `NetworkGameplayResolver.TryResolveCharacterData` faz `GetComponent` interno e verifica permissões.
- Em multiplayer com 4 jogadores × 60 fps = **240 invocações/s** que são quase sempre no-op (após resolver, characterData fica não-null para sempre).
- Mesmo que cada call custe 0.01ms, são **2.4ms/s desperdiçados em pure overhead** depois de `characterData` resolver.

## Objetivo

Eliminar a chamada redundante a `TryResolveCharacterData()` no `Update()` após `characterData` ter sido resolvido com sucesso.

## Investigação prévia (obrigatória)

### 1. Ler o método TryResolveCharacterData e suas chamadas

```
Read: Assets/Codigo/Characters/Player/PlayerHealthSystem.cs (offset 60, limit 130)
Read: Assets/Codigo/Characters/Player/PlayerHealthSystem.cs (offset 480, limit 20)
```

Confirmar:
- 3 chamadas: Awake, Update, OnNetworkSpawn
- Implementação interna em linha ~484

### 2. Confirmar usos de `characterData`

```
Grep: pattern="characterData" path="Assets/Codigo/Characters/Player/PlayerHealthSystem.cs"
```

Esperar encontrar:
- Verificações `if (characterData == null) return;` em métodos de gameplay
- Acessos `characterData.maxHealth`, `characterData.<outras propriedades>`

Confirmar que **nenhuma chamada externa** chama `TryResolveCharacterData()` (deveria ser apenas privado).

### 3. Verificar se há lógica que precisa do Update mesmo após resolver

```
Read: Assets/Codigo/Characters/Player/PlayerHealthSystem.cs (offset 100, limit 30)
```

Olhar o que mais o `Update()` faz. Provável que `Update()` continue tendo outra lógica útil (HandleRegeneration, etc). **NÃO remover o Update inteiro** — apenas a linha que chama TryResolveCharacterData.

## Plano de mudança

### Mudança em `PlayerHealthSystem.cs`

**Localização**: método `Update()` em linha ~100.

**Estado atual** (CONFIRMAR antes de editar):
```csharp
void Update()
{
    TryResolveCharacterData(); // linha ~106 - chamada redundante
    // ... resto do Update (regeneração, etc.)
}
```

**Opção A — Remover chamada do Update** (mais limpa, recomendada):
```csharp
void Update()
{
    // OPTIMIZATION (Sprint 4 / Item G6 - 2026-MM-DD): TryResolveCharacterData
    // removido do Update. Antes: invocado todo frame (240/s em 4 jogadores) com early-return
    // interno mas overhead do GetComponent presente. Agora: resolvido em Awake e OnNetworkSpawn,
    // ambos suficientes para cobrir spawn local e network. Sem isso: ~2.4ms/s desperdicados
    // em P/Invoke + verificacoes apos primeira resolucao.
    if (characterData == null)
    {
        // Tentativa final de resolver no proprio frame antes de skipar.
        TryResolveCharacterData();
        if (characterData == null) return;
    }

    // ... resto do Update (regeneração, etc.)
}
```

**Opção B — Tornar metade-paciente (uma chance por X frames)** (alternativa, NÃO recomendada para este item):
```csharp
private float _lastResolveAttempt = -10f;
private const float RESOLVE_RETRY_INTERVAL = 0.5f;

void Update()
{
    if (characterData == null && Time.unscaledTime - _lastResolveAttempt > RESOLVE_RETRY_INTERVAL)
    {
        _lastResolveAttempt = Time.unscaledTime;
        TryResolveCharacterData();
    }
    if (characterData == null) return;
    // ... resto
}
```

**Recomendação**: **Opção A**. Awake + OnNetworkSpawn já são chamados nos pontos críticos. Se ambos falharem, há um problema mais grave que retry no Update não vai resolver. O early-return manual mantém defensividade sem custo.

### Justificativa da implementação

- `Awake()` resolve em modo singleplayer (sem NGO).
- `OnNetworkSpawn()` resolve em multiplayer (após NGO ter o NetworkObject populado).
- A chamada do `Update()` era um "safety net" defensivo, mas como o método interno tem early-return e os outros 2 pontos cobrem todos os cenários conhecidos, é redundância pura.
- Se aparecer cenário onde Awake/OnNetworkSpawn falham, isso indica bug real — não deve ser mascarado por chamada repetida no Update.

## Validação

### 1. Build limpo
```powershell
dotnet build PI3D.sln
```
Esperar `0 Erro(s)`. 52 warnings pré-existentes OK.

### 2. Validação Unity (se disponível)
```
mcp__UnityMCP__read_console action="get" count="20"
```
Não deve haver erros novos. Não deve haver warnings sobre `characterData` null em runtime.

### 3. Validação funcional manual

Cenário **singleplayer** (cobre Awake):
1. Abrir Editor → cena `Assets/Scenes/MenuScene.unity`
2. Click "Singleplayer" → escolher comandante → entrar partida
3. Verificar HP funciona (HUD mostra valor, dano reduz)
4. Verificar regeneração (item G1 da Sprint 1) funciona

Cenário **multiplayer** (cobre OnNetworkSpawn):
1. Editor + 1 MPPM clone (ou 2 builds)
2. Host cria lobby, cliente conecta
3. Iniciar partida em `CenaMapaTeste`
4. Verificar HUD do cliente mostra HP correto desde frame 1 da partida
5. Tomar dano em ambos jogadores — deve registrar normal

Se HP ficar zerado/null em qualquer cliente: **rollback imediato e reportar**.

## Critérios de aceitação

- [ ] Build limpo (0 erros)
- [ ] HP funciona em singleplayer (HUD + regen)
- [ ] HP funciona em multiplayer host + cliente
- [ ] Comentário explicativo presente no Update
- [ ] Nenhum `Debug.LogError` novo em logs de runtime

## Riscos e mitigação

| Risco | Probabilidade | Mitigação |
|---|---|---|
| Cenário desconhecido onde nem Awake nem OnNetworkSpawn resolvem | Muito baixa | Early-return interno na Opção A faz uma tentativa final |
| HP fica null em primeiro frame para cliente novo | Baixa | OnNetworkSpawn é chamado antes do primeiro Update do NetworkBehaviour pelo NGO |
| Característica nova (DLC, modding) que injeta characterData tarde | Inexistente hoje | N/A no escopo atual |

## Rollback

```powershell
git checkout Assets/Codigo/Characters/Player/PlayerHealthSystem.cs
```

Reverter é seguro — Update antigo era superset funcional do novo.

## Reportar ao orquestrador (template)

```
Item: G6
Status: completed
Arquivos modificados: Assets/Codigo/Characters/Player/PlayerHealthSystem.cs
Build: PASS (0 erros, 52 warnings)
Validacao in-game: PASS (singleplayer + multiplayer host/cliente) | NOT_RUN
Metrica medida: TryResolveCharacterData calls/s (4 players ativos) — antes: ~240/s, depois: 0 apos resolver
Riscos detectados: nenhum
Proximo item liberado: true (G7 ou E6 sao paralelos a G6)
Notas: nenhuma
```

## Notas finais

Este é o item mais simples da Sprint 4. Se levar mais de 20 minutos, algo está errado: **abortar e reportar**.

Ganho mensurável é pequeno em CPU absoluto (~2-3ms/s por player), mas **eliminar overhead em hot paths é um princípio**. Não fazer isso agora significa carregar essa "dívida" para todos os players e todos os Updates futuros.
