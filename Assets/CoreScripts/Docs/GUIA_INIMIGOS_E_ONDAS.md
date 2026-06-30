# Guia de Inimigos e Ondas — ExoBeasts V3

Status: ativo
Público: quem configura inimigos, ondas de horda e dificuldade
Última atualização: 2026-06-30

---

## 1. EnemyDataSO — configuração de inimigo

Script em `Assets/CoreScripts/Enemy/EnemyDataSO.cs`.
Para criar: **Assets > Create > ScriptableObjects > Base de Dados > Enemy**.

### 1.1 Campos do Inspector

#### Prefab e Tipo

| Campo | Descrição |
|-------|-----------|
| `enemyPrefab` | Prefab do inimigo (deve estar em `Assets/Entidades/Inimigos/`) |
| `enemyType` | `Terrestre` ou `Voador` (define se torres com `TargetsFlyingEnemies = false` ignoram este inimigo) |

#### Status Básicos

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `baseHP` | float | Vida no nível 1 |
| `baseATQ` | float | Dano de ataque no nível 1 |
| `damageToBase` | float | Dano fixo causado ao Objetivo (Base) ao completar a rota |
| `moveSpeed` | float | Velocidade de movimento no nível 1 |
| `attackSpeed` | float | Frequência de ataque (ataques/segundo) |
| `baseArmor` | float (0–1) | Absorção de dano no nível 1. Ex: 0.2 = 20% de redução |

#### Escala por Nível

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `hpPerLevel` | float | Incremento de HP por nível (não utilizado pela fórmula atual — ver abaixo) |
| `atqPerLevel` | float | Incremento de ATQ por nível |
| `speedPerLevel` | float | Incremento de moveSpeed por nível |
| `armorPerLevel` | float (0–1) | Incremento de armor por nível |

#### Recompensas

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `geoditasOnDeath` | int | Geoditas concedidas ao time ao matar este inimigo |
| `etherDropChance` | float (0–1) | Probabilidade de dropar éter sombrio |

### 1.2 Fórmulas de scaling por nível

As fórmulas são aplicadas pelo `HordeManager` ao spawnar inimigos com o nível da horda atual.

| Stat | Fórmula |
|------|---------|
| HP | `Mathf.Round(baseHP × (1 + (nivel-1) × 0.15))` — cresce 15% por nível |
| Dano | `baseATQ + (nivel-1) × atqPerLevel` — crescimento linear |
| Velocidade | `moveSpeed + (nivel-1) × speedPerLevel` — crescimento linear |
| Armadura | `Mathf.Clamp01(baseArmor + (nivel-1) × armorPerLevel)` — linear, máximo 100% |

**Exemplos para um inimigo com baseHP=100, nível 5:**
- HP = `round(100 × (1 + 4 × 0.15))` = `round(100 × 1.60)` = **160**

O `nivel` passado para essas funções é o número da horda atual (`currentHorde`), não um nível do inimigo separado.

---

## 2. Inimigos disponíveis

Prefabs em `Assets/Entidades/Inimigos/`.

| Inimigo | Tipo | Observações |
|---------|------|-------------|
| Aranha | Terrestre | Padrão, spawn de teia |
| Capanga | Terrestre | Inimigo básico de combate |
| Escorpião | Terrestre | Ataque de veneno |
| Aguia | Voador | Ignora torres sem `TargetsFlyingEnemies` |
| MONSTRO | Terrestre | Inimigo pesado / boss |
| SpiderWeb | Terrestre | Web spawner (verifica uso no código) |

---

## 3. Como adicionar um novo tipo de inimigo

1. **Criar o EnemyDataSO**: Assets > Create > ScriptableObjects > Base de Dados > Enemy. Preencher todos os campos.
2. **Criar o prefab**: duplicar um prefab existente de `Assets/Entidades/Inimigos/` como base.
3. **Componentes obrigatórios no prefab**:

| Componente | Função |
|-----------|--------|
| `EnemyController` | Movimento, pathfinding e IA |
| `EnemyHealthSystem` | Vida, dano recebido, morte |
| `EnemyCombatSystem` | Lógica de ataque ao objetivo ou ao jogador |

4. **Em multiplayer**: adicionar `NetworkObject` + `NetworkTransform` (server-authoritative). Registrar o prefab no `Assets/Multiplayer/Setup/DefaultNetworkPrefabs.asset`.
5. **Atribuir no EnemyDataSO**: arrastar o prefab criado para o campo `enemyPrefab`.
6. **Incluir no HordeManager**: arrastar o EnemyDataSO para `enemyTypes[]` no HordeManager da cena (para ondas randômicas) ou referenciá-lo em `customWaves` (para ondas configuradas).

---

## 4. Configuração de ondas

### 4.1 WaveConfig e EnemySpawnConfig

`WaveConfig` e `EnemySpawnConfig` são structs definidas em `HordeManager.cs`:

**WaveConfig:**
| Campo | Tipo | Descrição |
|-------|------|-----------|
| `prepTime` | float | Tempo de fase de preparação antes desta onda (segundos) |
| `spawnSequence` | List\<EnemySpawnConfig\> | Sequência de spawns nesta onda |

**EnemySpawnConfig:**
| Campo | Tipo | Descrição |
|-------|------|-----------|
| `enemyData` | EnemyDataSO | Tipo de inimigo a spawnar |
| `spawnCount` | int | Quantidade de inimigos desta entrada |
| `pathIndex` | int | Índice no array `spawnPaths` do HordeManager. Valor negativo ou inválido = caminho aleatório |
| `spawnDelay` | float | Delay em segundos entre cada spawn desta entrada |

**Funcionamento:** cada `EnemySpawnConfig` com `spawnCount = 3` gera 3 spawns do mesmo inimigo no mesmo caminho, espaçados por `spawnDelay` segundos.

### 4.2 Ondas customizadas vs. randômicas

| Modo | Quando usar | Como ativar |
|------|-------------|-------------|
| **Randômico** | Testes e protótipos | Deixar `customWaves` vazia no Inspector |
| **Customizado** | Conteúdo de produção | Preencher a lista `customWaves` com WaveConfigs |

Quando `customWaves` tem elementos, o HordeManager usa o índice `currentHorde - 1` para selecionar a WaveConfig (onda 1 = índice 0, onda 2 = índice 1, etc.). Se o índice ultrapassar o tamanho da lista, o HordeManager cai para modo randômico.

---

## 5. HordeManager no Inspector

Script em `Assets/CoreScripts/Managers/HordeManager.cs`.
O HordeManager precisa de `NetworkObject` na raiz do GameObject em cena.

### 5.1 Campos de configuração

**Configurações da Horda:**
| Campo | Padrão | Descrição |
|-------|--------|-----------|
| `victoryHorde` | 5 | Número de ondas que o time precisa sobreviver para vencer |
| `timeBetweenWaves` | 10 | Segundos entre ondas (modo randômico) |
| `spawnInterval` | 1 | Delay entre spawns consecutivos (modo randômico) |
| `enemiesPerInterval` | 1 | Inimigos spawnados por intervalo (modo randômico) |

**Ondas Customizadas:**
| Campo | Descrição |
|-------|-----------|
| `customWaves` | Lista de WaveConfig. **Vazia = modo randômico** |

**Inimigos e Dificuldade (modo randômico):**
| Campo | Descrição |
|-------|-----------|
| `enemyTypes[]` | Tipos de inimigos disponíveis para seleção aleatória |
| `enemiesPerHordeMin` | Mínimo de inimigos por onda aleatória |
| `enemiesPerHordeMax` | Máximo de inimigos por onda aleatória |

**Caminhos de Spawn:**
| Campo | Descrição |
|-------|-----------|
| `spawnPaths` | Lista de `SpawnPath` — os transforms de spawn points da cena. Arraste os objetos da hierarquia |

**Fase de Preparação:**
| Campo | Padrão | Descrição |
|-------|--------|-----------|
| `prepTimeFirstWave` | 60 | Tempo de preparação antes da primeira onda (segundos) |
| `prepTimeBetweenWaves` | 30 | Tempo de preparação entre ondas subsequentes (segundos) |

**UI:**
| Campo | Descrição |
|-------|-----------|
| `hordeText` | TextMeshPro exibido na tela de jogo |
| `hordeTextBuild` | TextMeshPro exibido na tela de construção |

### 5.2 Configuração dos SpawnPaths na cena

Cada `SpawnPath` é um componente no GameObject de spawn point da cena. O HordeManager usa o `spawnPoint` (Transform) para posicionar o inimigo e `patrolPoints` para definir a rota que o inimigo vai seguir.

O `pathIndex` no `EnemySpawnConfig` é o índice nesse array. Se você tem 3 caminhos (índices 0, 1, 2) e usa `pathIndex = 4`, o HordeManager escolhe um caminho aleatório.

### 5.3 Controles durante o jogo

- **Tecla P**: pula a fase de preparação (disponível para qualquer jogador)
- O progresso de ondas é exibido no HUD como `hordaAtual/victoryHorde`

---

## 6. Multiplayer — HordeManager

- O HordeManager é server-authoritative: apenas o servidor/host gerencia o estado das ondas
- Clientes recebem updates via `NetworkVariable<int> currentHorde` e `NetworkVariable<int> enemiesRemaining`
- `isWaveActive` (NetworkVariable) indica se uma onda está em progresso
- O `EnemyPoolManager` é o sistema preferido para spawn de inimigos em rede; fallback direto via `NetworkObject.Spawn()` se o pool não estiver disponível
- Inimigos **devem** estar registrados em `DefaultNetworkPrefabs.asset` para aparecer nos clientes

---

## 7. Diagnóstico rápido

| Sintoma | Causa | Fix |
|---------|-------|-----|
| Inimigos não aparecem no cliente | Prefab não registrado no NGO | Adicionar ao `DefaultNetworkPrefabs.asset` |
| Inimigos não aparecem em build (ok no Editor) | Referência de prefab variant com fileID quebrado | Re-arrastar o prefab no campo `enemyPrefab` do EnemyDataSO no Inspector |
| Onda não avança (fica em 0 inimigos) | `spawnPaths` vazio ou sem `spawnPoint` | Verificar se os transforms de spawn foram atribuídos |
| `EnemyDataSO null` no log do HordeManager | `enemyTypes[]` tem slot vazio | Remover entradas null do array no Inspector |
| Inimigos não sobem de stats com a onda | Level não está sendo passado para `InitializeEnemy` | Verificar a chamada em `HordeManager.SpawnSingleEnemy()` |
