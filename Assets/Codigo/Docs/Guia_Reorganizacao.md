# Guia de Reorganizacao de Pastas — ExoBeasts V3
# Tutorial: nova estrutura, .meta perdidos e como corrigir no Inspector

Ultima atualizacao: 2026-03-25

> Status: historico.
> A documentacao ativa agora inclui `Assets/Diretrizes_Multiagente.md`,
> `Assets/Claude.md`, `Assets/Gemini.md`, `Assets/Codex.md` e
> `Assets/Codigo/Docs/Estado_Atual_Multiplayer.md`.
> Use este arquivo apenas como contexto da reorganizacao antiga.

---

## 1. O que foi feito

A estrutura de pastas do projeto foi reorganizada para agrupar scripts por dominio, eliminando problemas como:

- **Espacos em nomes de pasta** ("Char scripts", "Tower scripts") — causavam problemas em terminais e CI
- **Nome criptico "JP/"** — renomeado para `AbilitySystem/` (contem Ability.cs, CommanderAbilityController.cs)
- **Managers/ inchado** — tinha 17 scripts misturando core game, UI, audio e skill tree; agora tem 7
- **Scripts soltos** na raiz de `Codigo/` — agora organizados em pastas tematicas
- **Documentacao .md espalhada** na raiz de `Assets/` — consolidada em `Codigo/Docs/`

### Mapa Visual: Antes vs Depois

```
ANTES                                    DEPOIS
─────                                    ──────
Assets/Codigo/                           Assets/Codigo/
├── Char scripts/     ← ESPACOS!         ├── Characters/        ← SEM ESPACOS
│   ├── JP/           ← CRIPTICO         │   ├── AbilitySystem/  ← NOME CLARO
│   ├── CharacterBase.cs  ← SOLTO        │   ├── Base/           ← ORGANIZADO
│   ├── ProjectileVisual.cs               │   ├── Player/
│   ├── Player/                           │   ├── Coruja/
│   ├── Coruja/                           │   ├── Raposa/
│   ├── Dragao/                           │   ├── Dragao/
│   ├── Polvo/                            │   └── Polvo/
│   └── Raposa/                           │
├── Tower scripts/    ← ESPACOS!         ├── Towers/            ← SEM ESPACOS
├── Enemy/                                ├── Enemy/             (sem mudanca)
├── Managers/         ← 17 SCRIPTS       ├── Managers/          ← 7 SCRIPTS
│   ├── UIManager.cs                      ├── Audio/             ← NOVO
│   ├── BotaoHabilidade.cs                ├── UI/                ← NOVO
│   ├── Rastros.cs                        ├── SkillTree/         ← NOVO
│   ├── GerenciadorDeSomGlobal.cs         ├── VFX/               ← NOVO
│   └── ...                               ├── Utility/           ← NOVO
├── Multiplayer/                          ├── Multiplayer/        (sem mudanca)
├── MusicManager.cs   ← SOLTO            └── Docs/              ← NOVO
├── WinSound.cs       ← SOLTO
├── DamagePopup.cs    ← SOLTO
├── MagicStar.cs      ← SOLTO
└── CursorOn.cs       ← SOLTO
```

---

## 2. Por que os .meta importam

### Como o Unity vincula scripts a GameObjects

O Unity **NAO** usa o caminho do arquivo para vincular um script a um componente no Inspector. Ele usa um **GUID** (identificador unico) que fica dentro do arquivo `.meta`.

```
Exemplo: PlayerMovement.cs.meta contem:

  fileFormatVersion: 2
  guid: a1b2c3d4e5f6...    ← ESTE GUID e o que o Unity usa
  MonoImporter: ...
```

Quando voce arrasta `PlayerMovement.cs` para um GameObject, o Unity grava o GUID `a1b2c3d4e5f6...` no prefab/cena — **nao** o caminho `Characters/Player/PlayerMovement.cs`.

### O que significa para a reorganizacao

- **Mover .cs + .meta juntos** = Unity encontra o script normalmente (GUID nao mudou)
- **Mover .cs SEM o .meta** = Unity perde a referencia ("Missing Mono Script")
- **git mv** move ambos automaticamente = seguro

**Resumo:** Como usamos `git mv` para todas as mudancas, os GUIDs foram preservados. Na maioria dos casos, o Unity deve encontrar tudo automaticamente apos reimportar.

---

## 3. O que pode aparecer quebrado no Inspector

### Sintoma: "Missing (Mono Script)"

Ao abrir o Unity apos a reorganizacao, voce pode ver componentes com o texto **"Missing (Mono Script)"** no Inspector. Isso aparece como:

```
┌─────────────────────────────────────────┐
│  Inspector                              │
│  ─────────                              │
│  [!] Missing (Mono Script)              │
│      Script: None (Mono Script)         │
│                                         │
│  [!] Missing (Mono Script)              │
│      Script: None (Mono Script)         │
└─────────────────────────────────────────┘
```

### Causas possiveis

1. **Reimportacao incompleta** — Unity ainda nao reprocessou todos os .meta
2. **.meta de diretorio orfao** — git mv gerou .meta duplicados para pastas (inofensivo)
3. **Script deletado intencionalmente** — ex: `Fals.cs` (template vazio removido)
4. **Script renomeado** — ex: `EnemyControler.cs` → `EnemyController.cs` (nome antigo nao existe mais)

---

## 4. Tutorial: Como corrigir referencias perdidas

### Passo 1 — Reimportar todos os Assets

1. Abra o Unity e espere a barra de progresso terminar
2. Va em **Assets** (menu superior) > **Reimport All**
3. Espere o processo completar (pode levar alguns minutos)
4. Verifique a **Console** (Window > Console) — deve estar limpa

Se apos o Reimport All nao houver erros de compilacao e nenhum "Missing" no Inspector, a reorganizacao foi bem-sucedida e voce pode pular para a secao 8 (Checklist).

### Passo 2 — Identificar componentes "Missing"

Se ainda houver "Missing (Mono Script)":

1. **Abra o prefab** afetado (duplo-clique no Project window)
2. **No Inspector**, procure por componentes com icone de warning amarelo
3. **Anote** qual posicao o componente "Missing" ocupa na lista
4. **Compare** com a tabela abaixo para saber qual script deveria estar ali

#### Scripts mais provaveis de ter referencia perdida:

| Prefab/Objeto | Scripts que devem estar presentes |
|---------------|----------------------------------|
| Player (qualquer personagem) | PlayerMovement, PlayerHealthSystem, PlayerShooting, MeleeCombatSystem, PlayerCombatManager, CommanderAbilityController, CameraController, ThirdPersonCamera, PlayerHUD, VerificadorQueda, ProjectilePool |
| Enemy | EnemyController, EnemyHealthSystem, EnemyCombatSystem |
| Torre | TowerController, TowerAbilitySystem, TowerSelectionCircle |
| Armadilha | TrapLogicBase |
| Cena de jogo | HordeManager, BuildManager, CurrencyManager, ObjectiveHealthSystem, UIManager, GameModeManager, SpawnPath, PauseControl |
| Cena Win/Lose | WinSound / LoseSound |

### Passo 3 — Reassociar o script

1. **Clique no circulo** ao lado do campo "Script" no componente "Missing"
2. Na janela que abre, **busque pelo nome** do script (ex: "PlayerMovement")
3. **Selecione** o script correto
4. **Alternativa:** Arraste o arquivo .cs diretamente do **Project window** para o slot "Script"

```
┌─────────────────────────────────────────┐
│  Inspector                              │
│  ─────────                              │
│  [!] Missing (Mono Script)              │
│      Script: [O] None (Mono Script)     │
│               ▲                         │
│               │                         │
│      Clique aqui e busque o script      │
└─────────────────────────────────────────┘
```

### Passo 4 — Verificar campos serializados

Apos reassociar o script, os campos `[SerializeField]` podem estar vazios (null). Verifique e preencha:

#### Campos mais comuns por script:

**PlayerMovement.cs:**
- `characterData` — arrastar o ScriptableObject do personagem (ex: Ansgar.asset)
- `cameraTransform` — referencia a Camera principal

**PlayerHealthSystem.cs:**
- `characterData` — mesmo ScriptableObject
- `hpBarFill` — referencia ao Image da barra de vida (se houver)

**PlayerShooting.cs:**
- `characterData` — mesmo ScriptableObject
- `bulletSpawnPoint` — Transform de onde sai o projetil
- `projectilePrefab` — prefab do projetil visual

**CameraController.cs / ThirdPersonCamera.cs:**
- `playerTransform` — Transform do jogador (geralmente auto-referenciado)

**CommanderAbilityController.cs:**
- `abilities[]` — array de Ability ScriptableObjects (Q, E, X)
- `passiveAbility` — PassivaAbility ScriptableObject

**TowerController.cs:**
- `towerData` — ScriptableObject da torre
- `projectileSpawnPoint` — Transform
- `projectilePrefab` — prefab

**HordeManager.cs:**
- `spawnPaths[]` — array de SpawnPath
- `enemyPrefab` — prefab do inimigo

**ObjectiveHealthSystem.cs:**
- `maxHealth` — valor float (vida maxima do Core)

---

## 5. .meta de diretorio orfaos

### O que sao

Quando `git mv` move uma pasta inteira (ex: "Char scripts" → "Characters"), ele move todo o conteudo mas pode deixar .meta de subdiretorios orfaos. Isso e normal e **nao causa problemas de compilacao**.

### Quantos foram encontrados

Foram identificados **40+ .meta de diretorio orfaos**, concentrados na arvore `Characters/`:

- `Characters/Coruja/Caminhos/*.meta` (diretorios de caminhos)
- `Characters/Raposa/caminhos/*.meta`
- `Characters/Dragao/Caminhos/*.meta`
- `Characters/Polvo/Caminhos/*.meta`
- `Characters/Player/*.meta`
- Raiz de `Characters/*.meta`

### O que fazer

**Na maioria dos casos: nada.** O Unity regenera .meta de diretorio automaticamente ao reabrir o editor.

Se algum .meta orfao causar warning na Console:
1. Identifique o arquivo .meta na pasta (ex: `Characters/JP.meta` — pasta JP nao existe mais)
2. Delete o .meta orfao manualmente
3. Unity vai regenerar se necessario

---

## 6. Arquivos deletados (nao sao bug)

Alguns arquivos foram intencionalmente removidos ou renomeados. Se voce encontrar "Missing" referenciando esses nomes, **remova o componente** do Inspector (Right-click > Remove Component):

| Arquivo antigo | O que aconteceu |
|----------------|-----------------|
| `EnemyControler.cs` | **Renomeado** para `EnemyController.cs` (correcao de typo na Sprint 3) |
| `NetworkedCurrency.cs` | **Removido** — funcionalidade unificada no `CurrencyManager.cs` |
| `NetworkedHorde.cs` | **Removido** — funcionalidade unificada no `HordeManager.cs` |
| `Managers/Fals.cs` | **Deletado** — era template vazio do Unity sem nenhuma logica |

---

## 7. Mapa de Mudancas Completo

### Pastas renomeadas

| Antes | Depois |
|-------|--------|
| `Codigo/Char scripts/` | `Codigo/Characters/` |
| `Codigo/Tower scripts/` | `Codigo/Towers/` |
| `Characters/JP/` | `Characters/AbilitySystem/` |

### Pastas novas criadas

| Pasta | Conteudo |
|-------|----------|
| `Codigo/Audio/` | 6 scripts de audio consolidados |
| `Codigo/Characters/AbilitySystem/` | 3 scripts do sistema de habilidades |
| `Codigo/Characters/Base/` | 2 scripts compartilhados entre personagens |
| `Codigo/UI/` | 7 scripts de interface extraidos do Managers/ |
| `Codigo/UI/FrascosPoder/` | 2 scripts + assets de frascos |
| `Codigo/SkillTree/` | 3 scripts da arvore de habilidades |
| `Codigo/VFX/` | 1 script de efeito visual |
| `Codigo/Utility/` | 1 script utilitario |
| `Codigo/Docs/` | 9 documentos .md consolidados |

### Scripts movidos — Audio/ (6 scripts)

| Origem | Destino |
|--------|---------|
| `Codigo/MusicManager.cs` | `Audio/MusicManager.cs` |
| `Codigo/VolumeManager.cs` | `Audio/VolumeManager.cs` |
| `Codigo/WinSound.cs` | `Audio/WinSound.cs` |
| `Codigo/LoseSound.cs` | `Audio/LoseSound.cs` |
| `Codigo/WindSound.cs` | `Audio/WindSound.cs` |
| `Managers/GerenciadorDeSomGlobal.cs` | `Audio/GerenciadorDeSomGlobal.cs` |

### Scripts movidos — AbilitySystem/ (3 scripts)

| Origem | Destino |
|--------|---------|
| `Characters/JP/Ability.cs` | `Characters/AbilitySystem/Ability.cs` |
| `Characters/JP/CommanderAbilityController.cs` | `Characters/AbilitySystem/CommanderAbilityController.cs` |
| `Characters/JP/passivaAbility.cs` | `Characters/AbilitySystem/PassivaAbility.cs` (renomeado PascalCase) |

### Scripts movidos — Base/ (2 scripts)

| Origem | Destino |
|--------|---------|
| `Characters/CharacterBase.cs` | `Characters/Base/CharacterBase.cs` |
| `Characters/ProjectileVisual.cs` | `Characters/Base/ProjectileVisual.cs` |

### Scripts movidos — UI/ (8 scripts)

| Origem | Destino |
|--------|---------|
| `Managers/UIManager.cs` | `UI/UIManager.cs` |
| `Managers/BotaoHabilidade.cs` | `UI/BotaoHabilidade.cs` |
| `Managers/BuildButtonUI.cs` | `UI/BuildButtonUI.cs` |
| `Managers/BuildTooltipTrigger.cs` | `UI/BuildTooltipTrigger.cs` |
| `Managers/UpgradeTooltip.cs` | `UI/UpgradeTooltip.cs` |
| `Codigo/DamagePopup.cs` | `UI/DamagePopup.cs` |
| `Managers/FrascosPoder/AtributoFrasco.cs` | `UI/FrascosPoder/AtributoFrasco.cs` |
| `Managers/FrascosPoder/InventarioFrascos.cs` | `UI/FrascosPoder/InventarioFrascos.cs` |

### Scripts movidos — SkillTree/ (3 scripts)

| Origem | Destino |
|--------|---------|
| `Managers/Rastros.cs` | `SkillTree/Rastros.cs` |
| `Managers/RastroUpgrade.cs` | `SkillTree/RastroUpgrade.cs` |
| `Managers/StatIconDatabase.cs` | `SkillTree/StatIconDatabase.cs` |

### Scripts movidos — VFX/ e Utility/ (2 scripts)

| Origem | Destino |
|--------|---------|
| `Codigo/MagicStar.cs` | `VFX/MagicStar.cs` |
| `Codigo/CursorOn.cs` | `Utility/CursorOn.cs` |

### Scripts movidos — Towers/ (12 scripts)

Todos os scripts de `Tower scripts/` foram movidos para `Towers/` sem mudanca de nome:
BuildManager, GridPlacement, TopDownCameraManager, TowerAbilitySystem, TowerBehavior, TowerController, TowerSelectionCircle, TowerSelectionManager, TurretController, Upgrade, UpgradePanelUI, UpgradePath, TrapDataSO, TrapLogicBase.

### Scripts movidos — Characters/ (renomeacao de pasta)

Todos os scripts dentro de `Char scripts/Player/`, `Char scripts/Coruja/`, `Char scripts/Raposa/`, `Char scripts/Dragao/`, `Char scripts/Polvo/` foram movidos para `Characters/Player/`, `Characters/Coruja/`, etc. Nenhum script mudou de nome dentro dessas subpastas.

### Arquivo deletado

| Arquivo | Motivo |
|---------|--------|
| `Managers/Fals.cs` | Template vazio do Unity sem logica alguma |

### Documentos movidos para Docs/

| Origem (Assets/ raiz) | Destino |
|------------------------|---------|
| `Alertas_Migracao.md` | `Codigo/Docs/Alertas_Migracao.md` |
| `Documentacao_Sprint_6_Coruja.md` | `Codigo/Docs/Documentacao_Sprint_6_Coruja.md` |
| `Documentacao_Sprint_6_Raposa.md` | `Codigo/Docs/Documentacao_Sprint_6_Raposa.md` |
| `Documentacao_Sprint_7_8_Integracao.md` | `Codigo/Docs/Documentacao_Sprint_7_8_Integracao.md` |
| `Gemini.md` | `Codigo/Docs/Gemini.md` |
| `PlanoMigracao.md` | `Codigo/Docs/PlanoMigracao.md` |
| `Plano_Sprint_6_Coruja.md` | `Codigo/Docs/Plano_Sprint_6_Coruja.md` |
| `Relatorio_Sprint_5.md` | `Codigo/Docs/Relatorio_Sprint_5.md` |
| `Relatorio_Sprints_1_4.md` | `Codigo/Docs/Relatorio_Sprints_1_4.md` |

**Nota:** `Assets/Claude.md` **NAO foi movido** — ele precisa ficar na raiz de Assets/ para ser autodescoberto pelo Claude Code.

---

## 8. Checklist de Verificacao

Apos abrir o Unity com a nova estrutura, verifique:

- [ ] Unity abriu sem erros de compilacao na Console
- [ ] Console limpo (sem "Missing (Mono Script)" warnings)
- [ ] Prefab do Player (qualquer personagem): todos os scripts vinculados
- [ ] Prefab do Enemy: EnemyController + EnemyHealthSystem + EnemyCombatSystem presentes
- [ ] Torres: TowerController + TowerAbilitySystem presentes
- [ ] Cena "Network Test": funciona com Play (sem erros)
- [ ] Cena "SceneMapTest": funciona com Play
- [ ] No Project window: pasta `Codigo/` nao tem .cs soltos na raiz
- [ ] Pasta `Managers/` tem 7 scripts (+ subpasta Saves/)
- [ ] Pasta `Characters/JP/` nao existe mais (substituida por `AbilitySystem/`)

---

## 9. Estrutura Final Completa

```
Assets/Codigo/
├── Audio/                               (6 scripts)
│   ├── GerenciadorDeSomGlobal.cs
│   ├── LoseSound.cs
│   ├── MusicManager.cs
│   ├── VolumeManager.cs
│   ├── WinSound.cs
│   └── WindSound.cs
│
├── Characters/                          (reorganizado de "Char scripts")
│   ├── AbilitySystem/                   (reorganizado de "JP")
│   │   ├── Ability.cs
│   │   ├── CommanderAbilityController.cs
│   │   └── PassivaAbility.cs
│   ├── Base/
│   │   ├── CharacterBase.cs
│   │   └── ProjectileVisual.cs
│   ├── Player/                          (14 scripts)
│   ├── Coruja/                          (19 scripts + caminhos + assets)
│   ├── Raposa/                          (18 scripts + caminhos + assets)
│   ├── Dragao/                          (7 scripts + assets)
│   └── Polvo/                           (11 scripts + assets)
│
├── Enemy/                               (6 scripts — sem mudanca)
│
├── Managers/                            (7 scripts + Saves/)
│   ├── CurrencyManager.cs
│   ├── GameModeManager.cs
│   ├── HordeManager.cs
│   ├── MenuManager.cs
│   ├── ObjectiveHealthSystem.cs
│   ├── PauseControl.cs
│   ├── SpawnPath.cs
│   └── Saves/                           (8 scripts — sem mudanca)
│
├── Multiplayer/                         (23 scripts — sem mudanca)
│
├── Towers/                              (reorganizado de "Tower scripts")
│   ├── BuildManager.cs
│   ├── GridPlacement.cs
│   ├── TopDownCameraManager.cs
│   ├── TowerAbilitySystem.cs
│   ├── TowerBehavior.cs
│   ├── TowerController.cs
│   ├── TowerSelectionCircle.cs
│   ├── TowerSelectionManager.cs
│   ├── TurretController.cs
│   ├── Upgrade.cs
│   ├── UpgradePanelUI.cs
│   ├── UpgradePath.cs
│   └── Armadilhas/
│       ├── TrapDataSO.cs
│       └── TrapLogicBase.cs
│
├── UI/                                  (8 scripts)
│   ├── BotaoHabilidade.cs
│   ├── BuildButtonUI.cs
│   ├── BuildTooltipTrigger.cs
│   ├── DamagePopup.cs
│   ├── UIManager.cs
│   ├── UpgradeTooltip.cs
│   └── FrascosPoder/
│       ├── AtributoFrasco.cs
│       └── InventarioFrascos.cs
│
├── SkillTree/                           (3 scripts)
│   ├── Rastros.cs
│   ├── RastroUpgrade.cs
│   └── StatIconDatabase.cs
│
├── VFX/                                 (1 script)
│   └── MagicStar.cs
│
├── Utility/                             (1 script)
│   └── CursorOn.cs
│
└── Docs/                                (9 documentos)
    ├── Alertas_Migracao.md
    ├── Gemini.md
    ├── Guia_Game_Designer.md            ← ESTE GUIA
    ├── Guia_Reorganizacao.md            ← VOCE ESTA AQUI
    ├── PlanoMigracao.md
```
