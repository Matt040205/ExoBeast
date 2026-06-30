# Guia de Personagens — ExoBeasts V3

Status: ativo
Público: quem configura, cria ou ajusta personagens (commanders)
Última atualização: 2026-06-30

Guia operacional para entender e montar personagens no projeto.
Para status de migração multiplayer de cada personagem, ver `Guia_Game_Designer.md`.

---

## 1. CharacterBase — o ScriptableObject central

`CharacterBase.cs` fica em `Assets/CoreScripts/Base/`.

É o ScriptableObject usado tanto por **commanders** quanto por **torres** — o campo `isCommander` (seção "Type Settings") é o toggle que define o papel.

Para criar um novo asset: **Assets > Create > ScriptableObjects > Base de Dados > Personagem**.

### 1.1 Seções do Inspector

#### Basic Info
| Campo | Tipo | Descrição |
|-------|------|-----------|
| `name` | string | Nome exibido na UI |
| `description` | string (TextArea) | Texto de lore/descrição |

#### Basic Stats
| Campo | Tipo | Descrição |
|-------|------|-----------|
| `maxHealth` | float | Vida máxima (padrão 100) |
| `damage` | float | Dano base de ataque |
| `moveSpeed` | float | Velocidade de movimento |
| `reloadSpeed` | float | Velocidade de recarga |
| `attackSpeed` | float | Frequência de ataque (ataques/s) |
| `attackRange` | float | Alcance de ataque/melee |
| `armor` | float | Absorção de dano flat |
| `critChance` | float (0–1) | Probabilidade de crítico |
| `critDamage` | float | Multiplicador de dano crítico |
| `armorPenetration` | float (0–1) | Penetração de armadura |

#### Ultimate Settings
| Campo | Tipo | Descrição |
|-------|------|-----------|
| `ultimateChargePerSecond` | float | Carga gerada por segundo passivamente |
| `ultimateChargePerDamage` | float | Carga gerada por unidade de dano causado |

**Usado apenas por commanders.** Em torres, esses campos são ignorados.

#### Combat Settings
| Campo | Tipo | Descrição |
|-------|------|-----------|
| `combatType` | enum | `Ranged` (distância) ou `Melee` (corpo a corpo) |
| `fireMode` | enum | `SemiAuto` (1 disparo/clique) ou `FullAuto` (disparo contínuo) |
| `meleeAngle` | float | Ângulo do cone de ataque melee |

#### Type Settings
| Campo | Tipo | Descrição |
|-------|------|-----------|
| `isCommander` | bool | **true** = commander; **false** = torre |
| `desbloqueadoComandantePadrao` | bool | Se o personagem está disponível sem unlock |
| `characterIcon` | Sprite | Ícone usado na seleção de personagem |
| `commanderPrefab` | GameObject | Prefab do commander (modo jogável) |
| `towerPrefab` | GameObject | Prefab da torre construível |

#### Commander Specifics
**Usado apenas quando `isCommander = true`.** Em torres, esses campos aparecem no Inspector mas são ignorados.

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `magazineSize` | int | Tamanho do pente de munição |
| `passive` | PassivaAbility SO | Habilidade passiva do commander |
| `ability1` | Ability SO | Habilidade Q |
| `ability2` | Ability SO | Habilidade E |
| `ultimate` | Ability SO | Ultimate X |

#### Tower Upgrades
| Campo | Tipo | Descrição |
|-------|------|-----------|
| `upgradePaths` | List\<UpgradePath\> | Caminhos de upgrade (convenção: 3 caminhos, 5 níveis cada) |

**Usado por torres.** Commanders também têm esse campo mas o sistema de Rastros (seção 4) é separado.

#### Tower Specifics
| Campo | Tipo | Descrição |
|-------|------|-----------|
| `cost` | int | Custo em geoditas para construir a torre |

#### Rastros Progress
**Dados de progresso do sistema Rastros — ver Seção 4.** Gravados no SO em tempo de jogo; podem ser resetados via `ResetarRastros()`.

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `pontosRastrosDisponiveis` | int | Pontos ainda não gastos (padrão 10) |
| `pontosRastrosGastos` | int | Total de pontos gastos |
| `pontosPorCaminho` | List\<CaminhoRastrosData\> | Pontos por caminho de upgrade |
| `habilidadesDesbloqueadas` | List\<string\> | IDs das habilidades já desbloqueadas |

---

## 2. Commanders disponíveis

| Commander | Classe | Estilo | Status multiplayer |
|-----------|--------|--------|-------------------|
| Coruja | Arqueira | Ranged, longa distância | Migrado |
| Raposa / Ayame | Samurai | Melee + healing | Migrado |
| Dragão | Tanque | Melee, buffs de aliados | Migrado |
| Polvo | Suporte | Ranged, controle de área | Parcialmente migrado |

Para status detalhado de cada habilidade, ver **`Guia_Game_Designer.md` — Seção 3f**.

Os prefabs ficam em `Assets/Personagens/` (um subdiretório por personagem para Coruja, Raposa, Dragão, Polvo).

---

## 3. Componentes do prefab Commander

### 3.1 Componentes obrigatórios

| Componente | Função |
|-----------|--------|
| `CharacterController` | Colisão e movimento base; **nunca desabilitar em jogadores remotos** (triggers param de funcionar) |
| `PlayerMovement` | Lógica de movimento WASD + salto |
| `PlayerHealthSystem` | Vida, dano recebido, regeneração |
| `PlayerShooting` | Ataque ranged (projéteis) |
| `MeleeCombatSystem` | Ataque melee (cone de dano) |
| `PlayerCombatManager` | Coordena disparo e melee, aplica crits/armor |
| `CommanderAbilityController` | Gerencia habilidades Q/E/X e ultimate |
| `CameraController` | Câmera 3ª pessoa (desabilitado em remotos) |
| `ThirdPersonCamera` | Controle de câmera orbit |
| `PlayerHUD` | UI de vida, barra de ultimate |
| `LocalPlayerInputBridge` | Ponte entre PlayerInput (Unity) e scripts de lógica |
| `VerificadorQueda` | Detecta queda e aplica dano de queda |

### 3.2 Campos que o PlayerNetworkSetup tenta resolver automaticamente

Em multiplayer, `PlayerNetworkSetup.OnNetworkSpawn()` busca automaticamente:
`movement`, `cameraController`, `characterController`, `playerShooting`, `meleeCombat`, `playerCombatManager`, `localInputBridge`, `localOnlyObjects`.

Mantendo os nomes de campo padrão, o setup acontece sem arraste manual.

### 3.3 Componentes adicionados em multiplayer

| Componente | Quando adicionar |
|-----------|-----------------|
| `NetworkObject` | Sempre (raiz do prefab) |
| `ClientNetworkTransform` | Posição do jogador (owner-authoritative) |
| `NetworkAnimator` | Sincronização de animações |
| `PlayerNetworkSetup` | Configura local vs. remoto em spawn |

---

## 4. Habilidades — CommanderAbilityController

Script em `Assets/Personagens/AbilitySystem/CommanderAbilityController.cs`.

### 4.1 Wiring no prefab

O `CommanderAbilityController` lê as habilidades do `CharacterBase`:

```
characterData.ability1  →  tecla Q
characterData.ability2  →  tecla E
characterData.ultimate  →  tecla X (requer carga completa)
characterData.passive   →  ativado automaticamente no OnNetworkSpawn
```

Para atribuir: arraste o CharacterBase do personagem para o campo `characterData` no Inspector do `CommanderAbilityController`.

### 4.2 ScriptableObjects de habilidade

Para criar uma habilidade: **Assets > Create > ScriptableObjects > Habilidades > Ability** (ou PassivaAbility para passivos).

Cada `Ability` SO define:
- `abilityName`, `description`, `icon`
- `cooldown` (em segundos)
- Referência ao script de lógica (normalmente via `GameObject prefabToSpawn` ou link direto no script de Lógica)

Os scripts de lógica ficam em `Assets/Personagens/` junto ao personagem.
Ex.: `AquiNaoLogic.cs` para Dragão, `CacadoraNoturnaLogic.cs` para Coruja.

### 4.3 Ultimate (carga)

A carga da ultimate é uma `NetworkVariable<float>` (`netUltimateCharge`) visível para todos os clientes — o HUD lê diretamente.

Acumula via:
- Tempo passivo: `ultimateChargePerSecond` (do CharacterBase)
- Dano causado: `ultimateChargePerDamage` × dano causado

Threshold padrão: `ultimateChargeThreshold = 100f` (campo no Inspector do componente).

---

## 5. Sistema Rastros (upgrade tree do commander)

O sistema Rastros é a árvore de melhorias do commander — diferente dos upgrades de torre.

### 5.1 Estrutura

```
CharacterBase.upgradePaths
  └─► UpgradePath SO  (ex: "Trilha Predador")
        └─► List<Upgrade>  (ex: Nv 01.asset ... Nv 05.asset)
              ├── upgradeName, description
              ├── geoditeCost, darkEtherCost
              ├── List<StatModifier>  ← bônus de status simples
              └── behaviorToUnlock  ← prefab com TowerBehavior (vazio para commanders; usado em torres)
```

### 5.2 Criação de assets

| Asset | Menu |
|-------|------|
| Novo caminho | Assets > Create > ScriptableObjects > Trilhas > **Caminho** |
| Novo nível | Assets > Create > ScriptableObjects > Trilhas > **Nivel** |

### 5.3 StatModifier

Cada `Upgrade` pode ter uma lista de `StatModifier`:
- `statToModify`: enum `StatType` (Damage, AttackSpeed, Range, Armor, CritChance, CritDamage, ArmorPenetration)
- `modType`: `Additive` (soma flat) ou `Multiplicative` (multiplica)
- `value`: valor do modificador

### 5.4 Progresso e persistência

Os scripts `Rastros.cs` e `RastroUpgrade.cs` em `Assets/CoreScripts/SkillTree/` gerenciam o estado da árvore.

- Limite total: `pontosRastrosDisponiveis` (padrão 10 no CharacterBase)
- O progresso é salvo no próprio SO (campos `pontosRastrosGastos`, `pontosPorCaminho`)
- Reset: `CharacterBase.ResetarRastros()` limpa tudo

---

## 6. Erros comuns ao configurar um personagem

| Sintoma | Causa provável | Fix |
|---------|---------------|-----|
| Commander aparece sem habilidades | `characterData` não atribuído no `CommanderAbilityController` | Arrastar o CharacterBase correto para o campo |
| Habilidade não carrega cooldown no cliente | Ability com `RequireOwnership = true` em ServerRpc chamado de non-owner | Ver `PADROES_NGO.md` — Padrão P8 |
| Câmera rouba o foco do player remoto | `CameraController` não desabilitado em não-owners | `PlayerNetworkSetup` desabilita automaticamente — verificar se está no prefab |
| Triggers (fogueira, TP) não disparam para o cliente | `Rigidbody Kinematic` ausente no player remoto | Ver `PADROES_NGO.md` — Padrão P2 |
| Ultimate não sobe no HUD | `netUltimateCharge` não sincronizando | Verificar se o `CommanderAbilityController` está em objeto com `NetworkObject` |
