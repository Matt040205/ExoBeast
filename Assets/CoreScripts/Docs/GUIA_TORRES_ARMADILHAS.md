# Guia de Torres e Armadilhas — ExoBeasts V3

Status: ativo
Público: quem configura, cria ou ajusta torres e armadilhas
Última atualização: 2026-06-30

Guia operacional para entender e montar torres e armadilhas no projeto.

---

## 1. Torres — visão geral

Torres são estruturas construídas durante o jogo. Cada torre tem:
- Um **prefab** (GameObject com os componentes necessários)
- Um **CharacterBase** com `isCommander = false` como data source
- Um sistema de **upgrade em 3 caminhos** com 5 níveis cada

---

## 2. CharacterBase como towerData

O mesmo `CharacterBase.cs` serve para commanders e torres — controlado pelo campo `isCommander`.

### Campos relevantes para torres

| Campo | Descrição |
|-------|-----------|
| `name` | Nome da torre na UI |
| `description` | Texto exibido na tooltip |
| `maxHealth` | Vida da torre |
| `damage` | Dano base por ataque |
| `attackSpeed` | Taxa de ataque (ataques/segundo) |
| `attackRange` | Raio de detecção de inimigos |
| `armor` | Redução de dano recebido |
| `critChance` / `critDamage` | Probabilidade e multiplicador de crítico |
| `armorPenetration` | Penetração de armadura dos inimigos |
| `cost` | Custo em geoditas para construir |
| `towerPrefab` | Prefab da torre construível |
| `upgradePaths` | Caminhos de upgrade (ver Seção 4) |

### Campos ignorados em torres

Os campos abaixo **aparecem no Inspector mas não são usados** quando `isCommander = false`:
`passive`, `ability1`, `ability2`, `ultimate`, `magazineSize`, `ultimateChargePerSecond`, `ultimateChargePerDamage`, `moveSpeed`, `reloadSpeed`, `fireMode`, `meleeAngle`.

---

## 3. Anatomia do prefab de torre

### 3.1 Componentes obrigatórios

| Componente | Descrição |
|-----------|-----------|
| `TowerController` | Script principal. Detecta inimigos, gerencia ataque, aplica upgrades |
| `TowerAbilitySystem` | Gerencia os caminhos de upgrade e instancia `TowerBehavior` |
| `TowerSelectionCircle` | Indicador visual de seleção e range |
| `Collider` | Para detecção de clique e range visual |
| `Animator` | Animações (idle, ataque) |

### 3.2 Componentes adicionais em multiplayer

| Componente | Descrição |
|-----------|-----------|
| `NetworkObject` | Obrigatório na raiz para sincronização NGO |
| `NetworkTransform` | Posição server-authoritative |
| `NetworkedBuilding` | Replica visual de ataque para clientes via ClientRpc |

### 3.3 Campos do TowerController no Inspector

| Campo | Seção | Descrição |
|-------|-------|-----------|
| `towerData` | Referências Principais | Arraste o CharacterBase da torre |
| `partToRotate` | Referências Principais | Transform da parte que rotaciona na direção do inimigo |
| `firePoint` | Referências Principais | Transform de onde saem os projéteis |
| `tempoDeSpawn` | Materialização | Duração da animação de materialização (segundos) |
| `materialHolograma` | Materialização | Material de holograma (durante spawn) |
| `materialToon` | Materialização | Material final da torre |
| `materialOutline` | Materialização | Material de outline quando selecionada |
| `animator` | Visual e Animação | Animator do modelo |
| `shootTrigger` | Visual e Animação | Nome do trigger de ataque no Animator |
| `playAttackAnimation` | Visual e Animação | Se false, não executa animação de ataque |
| `somTiro` | FMOD - Sons | ID do evento FMOD para som de disparo |
| `enemyTag` | Configurações de IA | Tag dos inimigos (padrão "Enemy") |
| `TargetsFlyingEnemies` | Configurações de IA | Se true, ataca inimigos voadores |

---

## 4. Sistema de upgrades

### 4.1 Estrutura da cadeia completa

```
CharacterBase.upgradePaths   ← configurado no SO da torre
  └─► UpgradePath (SO)       ← um por caminho (convenção: 3 caminhos)
        pathName: "Caminho A"
        └─► List<Upgrade>    ← um por nível (convenção: 5 níveis)
              ├── upgradeName, description
              ├── geoditeCost, darkEtherCost
              ├── List<StatModifier>     ← bônus de status
              └── behaviorToUnlock      ← prefab com TowerBehavior (opcional)
```

### 4.2 Criação de assets

| Asset | Menu no Unity |
|-------|---------------|
| Novo caminho de upgrade | Assets > Create > ScriptableObjects > Trilhas > **Caminho** |
| Novo nível de upgrade | Assets > Create > ScriptableObjects > Trilhas > **Nivel** |

### 4.3 StatModifier

Cada `Upgrade` pode ter vários `StatModifier`:

| Campo | Tipo | Valores |
|-------|------|---------|
| `statToModify` | enum StatType | Damage, AttackSpeed, Range, Armor, CritChance, CritDamage, ArmorPenetration |
| `modType` | enum ModificationType | `Additive` (soma flat) ou `Multiplicative` (multiplica pelo valor) |
| `value` | float | Valor do modificador |

**Exemplo:** StatModifier com StatType=Damage, ModType=Additive, value=15 adiciona 15 de dano base.

### 4.4 behaviorToUnlock

O campo `behaviorToUnlock` aceita um **prefab** que contém um script `TowerBehavior`. Quando o upgrade é comprado, o `TowerAbilitySystem` instancia esse prefab como filho da torre e chama `Initialize(towerController)`.

Use esse campo quando o upgrade adiciona comportamento especial além de bônus de status simples — ex: que a torre passe a atacar inimigos voadores, ou adicione efeito de veneno.

### 4.5 Acompanhamento dos níveis

O `TowerController` mantém `currentPathLevels[3]` — um array com o nível atual em cada caminho (0 = não desbloqueado). Útil para condicionar comportamentos a nível mínimo.

---

## 5. TowerBehavior — criando um comportamento especial

### 5.1 Classe base

`TowerBehavior.cs` em `Assets/CoreScripts/Towers/`. É um `MonoBehaviour` abstrato:

```csharp
public abstract class TowerBehavior : MonoBehaviour
{
    public TowerController towerController;

    // Use isso em vez de NetworkBehaviour.IsServer para evitar null se sem NetworkObject
    public bool IsServer => NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;

    public virtual void Initialize(TowerController owner)
    {
        this.towerController = owner;
    }
}
```

### 5.2 Como criar um novo behavior

1. Criar um script herdando de `TowerBehavior`
2. Implementar `Initialize(TowerController owner)` chamando `base.Initialize(owner)` e subscrevendo nos eventos da torre
3. Implementar a lógica em `Update()` ou nos callbacks de evento
4. **Cleanup em `OnNetworkDespawn()` — NUNCA em `OnDestroy()`** (ver Alert #26 do `Guia_Game_Designer.md`)
5. Criar um prefab vazio com esse script
6. Arrastar o prefab para `behaviorToUnlock` no `Upgrade` SO correspondente

### 5.3 Eventos disponíveis no TowerController

| Evento | Assinatura | Quando dispara |
|--------|-----------|----------------|
| `OnTargetDamaged` | `Action<EnemyHealthSystem>` | Após aplicar dano a um inimigo |
| `OnCalculateDamage` | `Func<EnemyHealthSystem, float, float>` | Para interceptar e modificar o dano calculado |
| `OnCriticalHit` | `Action<EnemyHealthSystem>` | Quando um ataque resulta em crítico |
| `OnEnemyKilled` | `Action<EnemyHealthSystem>` | Quando a torre confirma a morte de um inimigo |

**Exemplo:** para adicionar efeito de veneno ao matar, assinar `towerController.OnEnemyKilled += AplicarVeneno`.

---

## 6. TowerBehaviors existentes

Scripts de behavior ficam em `Assets/CoreScripts/Towers/` e `Assets/Personagens/`:

| Script | Efeito |
|--------|--------|
| `ArmorAuraBehavior` | Aura que aumenta armadura de aliados próximos |
| `ArmorShredBehavior` | Ataque reduz armadura do inimigo |
| `HealingAuraBehavior` | Aura de cura para aliados |
| `BleedingBehavior` | Aplica sangramento (dano ao longo do tempo) |
| `MultiShotBehavior` | Ataque atinge múltiplos alvos |
| `PiercingBehavior` | Projéteis perfuram inimigos |
| `DragonPatrolBehavior` | IA especial da Torre Dragão (perseguição + leash) |
| `AssaultBehavior` | Modo de assalto offensivo |
| `FuryStackBehavior` | Acumula stacks de fúria por ataque |

---

## 7. Armadilhas

### 7.1 TrapDataSO — configuração

Script em `Assets/CoreScripts/Towers/TrapDataSO.cs`.
Para criar: **Assets > Create > ScriptableObjects > TrapData** (confirmar o menu exato no Editor).

| Campo | Descrição |
|-------|-----------|
| `prefab` | **Prefab visual** — spawnado no momento do placement (preview e confirmação) |
| `logicPrefab` | **Prefab de lógica** — spawnado pela rede quando a armadilha é ativada. Contém o script real e o `NetworkObject` |
| `geoditeCost` | Custo em geoditas |
| `darkEtherCost` | Custo em éter sombrio |
| `buildLimit` | Máximo de armadilhas deste tipo por time. **0 = ilimitado** (⚠️ Alert #33 — todos os TrapDataSOs estão com 0) |
| `placementType` | Onde pode ser colocada: `OnPath`, `OffPath`, ou `QualquerLugar` |

### 7.2 Por que dois prefabs?

O prefab visual (`prefab`) serve apenas para o preview e feedback imediato ao jogador. O prefab de lógica (`logicPrefab`) é o objeto que tem a lógica de efeito e, em multiplayer, precisa ter `NetworkObject` para ser spawnado pelo servidor.

Essa separação evita que a lógica de rede seja instanciada antes de a armadilha ser confirmada.

### 7.3 Armadilhas disponíveis

Scripts em `Assets/Armadilhas/`. Prefabs em `Assets/Entidades/Armadilhas/` (ou similar).

| Armadilha | Efeito |
|-----------|--------|
| Espinhos | Dano aos inimigos que passam |
| Fogueira | Dano em área (fogo) |
| Teleportador | Teleporta inimigos para trás na rota |
| Broca | Dano perfurante |
| Piche | Lentidão (slow) |

### 7.4 Alerta buildLimit

**Alert #33 (ver `Guia_Game_Designer.md` Seção 5):** todos os `TrapDataSO` estão com `buildLimit = 0` (ilimitado). Configurar valores acima de 0 para balancear o jogo quando necessário.

---

## 8. Multiplayer — regras para torres e armadilhas

- Registrar o `logicPrefab` no `DefaultNetworkPrefabs.asset` (em `Assets/Multiplayer/Setup/`)
- Em `TowerBehavior`, use `this.IsServer` (não `IsServer` herdado de NetworkBehaviour) para evitar o bug de `IsServer` pré-Spawn — ver **`PADROES_NGO.md` — Padrão P1**
- Cleanup de eventos de torre em `OnNetworkDespawn()`, não em `OnDestroy()` — Alert #26
- Torre Dragão: `DragonPatrolBehavior` é server-authoritative, `NavMeshAgent` desativado em clientes — ver sessão 25 Maio 2026 no `Guia_Game_Designer.md`
