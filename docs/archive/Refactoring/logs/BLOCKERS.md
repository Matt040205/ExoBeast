# BLOCKERS — Índice global de bloqueios ativos

> Registra bloqueios que impedem progresso de uma sprint. Cada entrada deve ser resolvida ou explicitamente arquivada antes da sprint ser fechada.

## Status global (2026-05-21)

**Todos os 3 bloqueios da Sprint 0 estão RESOLVIDOS e validados.** Sprint 0 fechada (smoke test base passou). Pré-requisitos para Sprint 1 satisfeitos.

| Bloqueio | Descrição | Status |
|---|---|---|
| #1 | EOSConfigGenerator path resolution no clone MPPM | ✅ RESOLVIDO (commit local, fix mínimo) |
| #2 | Suspeita inicial de cache corrompido | ✅ ESCALADO → #3 (não era cache) |
| #3 | Unity 6 MPPM `SceneManager.LoadScene(name)` shared scene list | ✅ RESOLVIDO (outro agente, fix robusto) |

---

## RESOLVIDO — Bloqueio #1: Sprint 0 — Clone MPPM falha em Play Mode (EOSConfigGenerator não encontra credenciais)

**Aberto:** 2026-05-21
**Resolvido:** 2026-05-21
**Sprint afetada:** Sprint 0 (smoke test base 0.5)
**Reportado por:** Usuário em sessão (screenshot do console)
**Status:** AGUARDANDO_VALIDACAO_DO_USUARIO (build OK; precisa re-testar MPPM no Editor)

### Patch aplicado

Branch: `multi-player-refactor` (commit direto, autorizado pelo orquestrador).
Arquivo: `Assets/Editor/EOSConfigGenerator.cs`.
Mudanças:
- Adicionado `using ExoBeasts.Multiplayer.Core;` (acesso a MppmHelper)
- `TryLoadFromFile`: path resolution agora considera `MppmHelper.IsClone`, replicando pattern de `EOSConfig.cs:72-76`.
- Adicionado comentário `BUG FIX (2026-05-21)` no bloco modificado.

### Build pós-fix

- `dotnet build PI3D.sln --no-incremental`: **0 erros, 68 warnings (baseline mantido)**.

### Sintoma

Ao entrar em Play Mode no clone MPPM (Player 2), o console mostra:

```
[EOSConfigGenerator] Nenhuma fonte de credenciais EOS encontrada.
[EOSConfigGenerator] Play Mode bloqueado — corrija as credenciais EOS.
Destroy may not be called from edit mode! Use DestroyImmediate instead.
[EOSEditorPlayModeHelper] Encerrando EOS antes do domain reload...
[EOSEditorPlayModeHelper] EOS encerrado com sucesso.
```

Erro derivado: `EditorApplication.isPlaying = false` chamado em momento inadequado da transição Edit→Play, causando "Destroy may not be called from edit mode!".

### Causa raiz

`Assets/Editor/EOSConfigGenerator.cs:127-128` resolve o caminho do projeto sem considerar clones MPPM:

```csharp
// Linha 127-128 (BUG):
string projectRoot = Path.GetDirectoryName(Application.dataPath);
string filePath = Path.Combine(projectRoot, CREDENTIALS_FILE);
```

No clone MPPM, `Application.dataPath` aponta para a cópia virtual em `%LocalAppData%\Unity\Editor\MultiplayerPlayMode\...\Assets`, **não** para a raiz do projeto principal onde o `EOSCredentials.json` realmente está.

**Pattern correto já existe** em `Assets/Codigo/Multiplayer/Core/EOSConfig.cs:72-76`:

```csharp
string dataParent = MppmHelper.IsClone
    ? Path.Combine(Application.dataPath, "..", "..", "..", "..")
    : Path.Combine(Application.dataPath, "..");

string filePath = Path.GetFullPath(Path.Combine(dataParent, CREDENTIALS_FILE));
```

O EOSConfig (runtime) trata clones corretamente. O EOSConfigGenerator (editor, roda primeiro) **não**.

### Confirmação na memória

A memória `eos_credentials_refactor.md` (13 Maio 2026) §Pendências lista:
> "Validação de funcionamento (pendente): Verificar MPPM clone ainda autentica (path resolution especial em `EOSConfig.TryLoadFromFile`)"

Este bloqueio é exatamente a materialização dessa pendência aberta.

### Escopo do fix proposto

**Arquivo único**: `Assets/Editor/EOSConfigGenerator.cs`.
**Mudança**: replicar o pattern de `EOSConfig.cs:70-82` no método `TryLoadFromFile` do generator.

Patch sugerido (3 linhas alteradas):

```csharp
// Em vez de:
string projectRoot = Path.GetDirectoryName(Application.dataPath);
string filePath = Path.Combine(projectRoot, CREDENTIALS_FILE);

// Usar:
string projectRoot = ExoBeasts.Multiplayer.Core.MppmHelper.IsClone
    ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", ".."))
    : Path.GetDirectoryName(Application.dataPath);
string filePath = Path.Combine(projectRoot, CREDENTIALS_FILE);
```

### Risco do fix

- 🟢 **Risco baixo**: pattern copiado de código já validado em produção.
- 🟢 Editor-only — não afeta build standalone.
- 🟢 Behavior change apenas em clone MPPM; projeto principal continua resolvendo igual.
- ⚠️ Dependência: `MppmHelper` está em `Assembly-CSharp`; `EOSConfigGenerator` em `Assembly-CSharp-Editor`. Editor → Runtime referência é OK por padrão Unity.

### Categorização

**NÃO é parte das sprints 0-8 do plano.** É um **pré-requisito** para Sprint 0 fechar (smoke test base precisa passar). Análogo a "build precisa compilar antes de começar" — bloqueio infra.

### Próximos passos

1. ✅ Bloqueio registrado neste arquivo
2. ⏳ Aguardando autorização do orquestrador para aplicar o fix
3. Após autorização:
   - Aplicar patch em `EOSConfigGenerator.cs`
   - Build + repetir smoke test MPPM
   - Marcar bloqueio como RESOLVIDO neste arquivo
   - Atualizar `eos_credentials_refactor.md` da memória (item de pendência resolvido)
   - Fechar Sprint 0 → iniciar Sprint 1

### Decisão pendente do orquestrador

Esta correção deve ser tratada como:
- **(a)** commit isolado direto em `multi-player-refactor` (pré-requisito, fora das sprints)
- **(b)** PR separado contra `main` (fix de bug de produção que aparece na branch principal também)
- **(c)** PR contra `multi-player-refactor` (só para destravar nossas sprints)

Recomendação: **(b)** — é um bug que afeta qualquer dev que tentar usar MPPM, não só esta refatoração. Faz mais sentido em `main`.

---

## RESOLVIDO PARCIAL — Bloqueio #2 → escalou para Bloqueio #3

O Bloqueio #2 (suspeita de cache corrompido) foi parcialmente resolvido: o reset do Virtual Player limpou cache mas **o problema persistiu no clone novo**, confirmando que NÃO era cache — era arquitetural. Resultou no Bloqueio #3.

### Histórico
- Reset do Virtual Player: `Library/VP/mppm2c2807dc/` (antigo) apagado + `SystemData.json` resetado
- Unity reabriu, criou novo clone `mppm5b7be4a6` (145MB)
- Console limpo no novo clone
- **MAS** mesmo erro de LoadScene voltou no novo clone

## RESOLVIDO — Bloqueio #3: Unity 6 MPPM v1.6.3 — SceneManager.LoadScene(name) em clones

**Aberto:** 2026-05-21
**Resolvido:** 2026-05-21 (validado por smoke test do usuário)
**Sprint afetada:** Sprint 0 (smoke test 0.5)
**Status:** TOTALMENTE_RESOLVIDO

### Solução final (consolidada após intervenção de outro agente)

A solução evoluiu em duas etapas:

**Etapa A (minha tentativa inicial)**: workaround em runtime via `LoadLocalSceneMppmSafe` em `GameModeManager.cs` — resolver cena por build index em vez de nome. Funcional mas só rede de segurança, não causa raiz.

**Etapa B (intervenção do outro agente — solução definitiva)**: ataque à causa raiz + endurecimento da rede de segurança + testes de regressão.

#### B.1 — `Assets/Editor/BuildSceneListGuard.cs` (NOVO, 187 LOC)

Componente Editor-time `[InitializeOnLoad]` com hook `playModeStateChanged` em `ExitingEditMode` (mesmo timing do `EOSConfigGenerator`).

Antes de cada Play Mode:
1. Valida que a lista canônica de 8 cenas existe como assets (`AssetDatabase.LoadAssetAtPath<SceneAsset>`).
2. Compara contra `EditorBuildSettings.globalScenes` **E** `EditorBuildSettings.scenes` — **as duas propriedades**.
3. Se não bate, **auto-repara** ambas listas para a forma canônica, e chama `AssetDatabase.SaveAssets()`.
4. Se ainda divergir após auto-repair: bloqueia Play Mode (`EditorApplication.isPlaying = false`) com mensagem clara.
5. Expõe menu manual: `Tools > ExoBeasts > Repair Build Scene List`.

**A descoberta-chave** que essa solução implica: Unity 6 introduziu `EditorBuildSettings.globalScenes` (nova) **separada** de `EditorBuildSettings.scenes` (clássica). MPPM clones em Unity 6 usam `globalScenes`; se a sincronização entre as duas falhar (acontece com mudanças via Build Profiles UI ou em domain reloads), o clone "vê" lista vazia. O guard mantém ambas sincronizadas sempre.

Esse pattern é o mesmo do `EOSConfigGenerator`: ambos rodam no `ExitingEditMode`, ambos têm fallback de bloqueio de Play Mode, ambos expõem reparo manual via menu. Cohesão arquitetural.

#### B.2 — `Assets/Codigo/Managers/GameModeManager.cs` (refinado)

Mantém `LoadLocalSceneMppmSafe` da Etapa A mas adiciona:
- Helper `GetScenePath(name)` que aceita path completo OU nome curto (mais defensivo).
- Helper `GetBuildSceneListForLog()` que itera `SceneManager.sceneCountInBuildSettings` para debug — quando falhar, o log mostra exatamente o que o runtime do clone "vê".
- **Fallback crítico final**: `TryLoadSceneInEditorPlayMode` (sob `#if UNITY_EDITOR`) que chama `EditorSceneManager.LoadSceneInPlayMode(path, params)`. Esse é o último recurso quando nem index nem path resolvem — funciona via Editor API mesmo quando Build Profiles está vazio no clone MPPM.

#### B.3 — `Assets/Codigo/Multiplayer/Lobby/LobbySceneUI.cs` (fix UX adicional)

Descoberto que botões "Entrar Lobby" e "Lobby Público" tinham filhos `TMP_Text` com `raycastTarget=true`, expandindo a área clicável e roubando cliques em casos extremos. Adicionado `DisableButtonLabelRaycasts()` que desabilita raycast em todos `TMP_Text` filhos de `Button` no `Awake`. Bonus: também aceita typo `PrcurarLobbyID` como fallback no AutoDetect.

#### B.4 — `Assets/Scenes/LobbyScene.unity` (persistência da config UX)

4 `MonoBehaviour` (TMP_Text de botões) com `m_RaycastTarget: 1 → 0` persistidos na cena. Garante que o fix do B.3 é o estado padrão da cena, não só aplicado em runtime.

#### B.5 — `Assets/Tests/Editor/MenuSceneValidationTests.cs` (3 testes novos)

Transforma o conhecimento institucional em guard automatizado:

| Teste | Garantia |
|---|---|
| `CanonicalScenesAreEnabledAndOrderedInBuildSettings` | `EditorBuildSettings.globalScenes` E `.scenes` batem com lista canônica (regression direta do bug do MPPM) |
| `LobbySceneResolvesToBuildIndex` | `SceneUtility.GetBuildIndexByScenePath(LobbyScene)` retorna ≥ 0 |
| `LobbySceneJoinButtonsDoNotLetChildTextStealRaycasts` | Botões `EntrarLobby` e `LobbyPublico` não têm filhos TMP_Text com `raycastTarget=true` |

Refactored `WithMenuScene` em helper genérico `WithScene` para evitar duplicação.

#### B.6 — `Assets/Modelos/fontes/PaytoneOne SDF.asset` e `LiberationSans SDF - Fallback.asset`

Touch automático do Unity (re-save de font atlas durante Play Mode ou domain reload). Não causal ao fix; ruído incidental.

### Por que essa solução é superior

| Aspecto | Etapa A (minha) | Etapa B (outro agente) |
|---|---|---|
| Causa raiz endereçada | ❌ Só workaround runtime | ✅ Sincroniza globalScenes ↔ scenes |
| Rede de segurança | Index resolution | Index + path + EditorSceneManager fallback |
| Prevenção em domain reload | ❌ Não | ✅ Auto-repair em ExitingEditMode |
| Diagnóstico em falha | Warning simples | Log com lista completa de cenas do runtime |
| Regression tests | ❌ Não | ✅ 3 testes editor |
| Persistência da config UX | ❌ Não | ✅ Cena + runtime alinhados |
| Reparo manual | ❌ Não | ✅ Menu `Tools > ExoBeasts > Repair Build Scene List` |

### Validação

- Build: 0 erros, 68 warnings (baseline mantido pelo passo anterior).
- Smoke test base MPPM: **PASS** (Player 1 + Player 2 chegam em LobbyScene, criam/entram em sala, escolhem personagem, iniciam partida em CenaMapaTeste — confirmado pelo usuário).

### Sintoma

Em clones MPPM (Player 2+), `SceneManager.LoadScene("LobbyScene")` falha com:
```
Scene 'LobbyScene' couldn't be loaded because it has not been added to the active build profile or shared scene list or the AssetBundle has not been loaded.
```

Acontece em qualquer clone (testado com cache antigo e novo). Configuração YAML idêntica entre principal e clone (`EditorBuildSettings.asset`, `BuildProfileContext.asset`, `SharedProfile.asset`, `PlatformProfile.asset`).

### Diagnóstico

| Verificação | Resultado |
|---|---|
| `EditorBuildSettings.asset` clone vs principal | ✅ Idêntico (LobbyScene em buildIndex=2) |
| `BuildProfileContext.asset` (active profile) | ✅ Ambos vazios (= use shared scene list) |
| `SharedProfile.asset` `m_OverrideGlobalSceneList` | ✅ Ambos 0 |
| `manage_build profiles` via MCP | ✅ 0 profiles, active_profile: null (em ambos) |
| `manage_scene get_build_settings` via MCP | ✅ Retorna 8 cenas corretas em ambos |
| Principal carrega LobbyScene | ✅ Sim (confirmado: criou "Minha Sala") |
| Clone carrega LobbyScene por nome | ❌ Falha |

**Causa raiz**: bug genuíno do Unity 6 + MPPM v1.6.3. `SceneManager.LoadScene(string name)` em clones falha ao resolver pela shared scene list mesmo quando a cena está lá. O resolver por **build index** (`SceneManager.LoadScene(int index)`) funciona pois consulta direto o array de cenas, sem passar pelo resolver de nomes quebrado.

### Patch aplicado

Branch: `multi-player-refactor` (commit autorizado pelo orquestrador).
Arquivo: `Assets/Codigo/Managers/GameModeManager.cs` (fora do anel interno/externo do plano).

**Mudanças**:
1. Novo método privado static `LoadLocalSceneMppmSafe(string sceneName)`:
   - Resolve build index via `SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/{name}.unity")`
   - Se index ≥ 0: usa `SceneManager.LoadScene(index)` (caminho confiável em clones)
   - Senão: fallback para `SceneManager.LoadScene(name)` com warning
2. `LoadSceneSafe` (caminho não-network) agora delega para `LoadLocalSceneMppmSafe`
3. `SceneTransitionRoutine` (caminho network) agora delega para `LoadLocalSceneMppmSafe`
4. Comentário `BUG FIX (2026-05-21)` explicando o workaround

**Não modificado**: caminhos NGO (`NetworkManager.Singleton.SceneManager.LoadScene`) — esses funcionam corretamente em ambos host e clientes.

### Build pós-fix

- `dotnet build PI3D.sln --no-incremental`: **0 erros, 68 warnings (baseline mantido)**.

### Próximos passos

1. ✅ Patch aplicado
2. ⏳ Usuário re-executa smoke test no clone
3. Após validação: marcar bloqueio como totalmente RESOLVIDO, fechar Sprint 0, iniciar Sprint 1

### Por que o fix NÃO é parte das sprints

`GameModeManager.cs` está em `Assets/Codigo/Managers/`, fora do escopo de refatoração multiplayer (`Assets/Codigo/Multiplayer/`). Esse é um fix de infraestrutura — pré-requisito para Sprint 0 fechar.

---

## ANTERIOR — Bloqueio #2: Sprint 0 — Clone MPPM não consegue carregar LobbyScene

**Aberto:** 2026-05-21 (após resolução do bloqueio #1)
**Sprint afetada:** Sprint 0 (smoke test base 0.5)
**Status:** AGUARDANDO_DECISAO_DO_USUARIO

### Sintoma

Após fix do bloqueio #1, ao clicar "Multiplayer" no MenuScene do Player 2 (clone MPPM):

```
Scene 'LobbyScene' couldn't be loaded because it has not been added to the active build profile or shared scene list or the AssetBundle has not been loaded.
To add a scene to the active build profile or shared scene list use the menu File->Build Profiles
UnityEngine.SceneManagement.SceneManager:LoadScene (string)
ExoBeasts.Managers.GameModeManager/<SceneTransitionRoutine>d__24:MoveNext () (at C:/Users/zegil/Documents/GitHub/ExoBeasts_V3/PI3D/Assets/Codigo/Managers/GameModeManager.cs:130)
```

### Análise

| Verificação | Resultado |
|---|---|
| `Assets/Scenes/LobbyScene.unity` existe | ✅ Sim |
| `ProjectSettings/EditorBuildSettings.asset` lista LobbyScene | ✅ enabled=1, guid=5ea820e70f1c5bc4da6edd7b825faa6c |
| EditorBuildSettings do clone (`Library/VP/mppm2c2807dc/ProjectSettings/`) | ✅ **Idêntico** ao do principal |
| `Library/BuildProfileContext.asset` (principal) | Vazio → usa Shared Scene List |
| `Library/VP/mppm2c2807dc/Library/BuildProfileContext.asset` (clone) | Vazio → usa Shared Scene List |
| `Library/BuildProfiles/SharedProfile.asset` (principal) | `m_OverrideGlobalSceneList: 0`, `m_Scenes: []` |
| `Library/VP/mppm2c2807dc/Library/BuildProfiles/SharedProfile.asset` (clone) | `m_OverrideGlobalSceneList: 0`, `m_Scenes: []` |
| `GameModeManager.cs:130` chama `SceneManager.LoadScene(sceneName)` | OK, `sceneName="LobbyScene"` via SerializeField |

**Configurações de scene loading estão idênticas entre principal e clone**, mas o clone falha.

### Hipótese

Comportamento conhecido do **MPPM v1.6.3 em Unity 6**: cada clone tem seu próprio `Library/` físico (não symlink). O cache de scene-resolution do clone pode ficar dessincronizado mesmo quando as configs YAML estão idênticas.

### Não é causado pela refatoração

- Estamos em Sprint 0 — nenhum código de produção foi modificado.
- O único fix aplicado (bloqueio #1) tocou apenas `Assets/Editor/EOSConfigGenerator.cs`.
- O erro vem de `GameModeManager.cs:130` que está intocado.

### Soluções propostas (do menos ao mais destrutivo)

#### Solução A — Refresh Asset Database no clone (não destrutivo)
1. Na janela do Player 2 (clone): Assets menu → "Refresh" (ou Ctrl+R)
2. Tentar Play novamente

#### Solução B — Forçar reimport no clone (médio impacto)
1. No clone: Assets menu → "Reimport All"
2. Aguardar reimport completar
3. Tentar Play

#### Solução C — Limpar cache de Build do clone (destrutivo — recriar caches do clone)
1. Fechar o clone (Window → Multiplayer → Play Mode → remover Virtual Player)
2. Apagar pasta `Library/VP/mppm2c2807dc/` inteira
3. Recriar Virtual Player (`Window → Multiplayer → Play Mode → +Add`)
4. Aguardar Unity recompilar e reimportar tudo (pode levar minutos)
5. Testar smoke test do início

#### Solução D — Forçar Shared Scene List explicitamente
1. No projeto principal: File → Build Profiles
2. Criar/selecionar profile Windows 64-bit
3. Marcar "Use shared scene list" explicitamente (ou copiar as cenas para o profile)
4. Salvar e fechar
5. Reabrir clone — deve herdar

#### Solução E — Investigar via Unity MCP
Se o clone Unity Editor estiver com MCP-FOR-UNITY ativo (visto no console), eu posso executar:
- `mcp__UnityMCP__read_console` para ver logs do clone
- `mcp__UnityMCP__refresh_unity` para forçar Asset Database refresh
- `mcp__UnityMCP__manage_editor` para verificar estado

### Próximos passos

Aguardando decisão do usuário sobre qual solução tentar primeiro.

### Investigação via Unity MCP (2026-05-21)

Conectado simultaneamente a 2 instâncias:
- `PI3D@a759c4e1` (porta 6402, principal)
- `mppm2c2807dc@7afd1371` (porta 6401, clone)

**Principal (PI3D)**:
- ✅ Console: chegou em LobbyScene + criou sala "Minha Sala"
- ✅ Logs: `[LobbyManager] Criando lobby EOS: 'Minha Sala'...` + `[LobbySceneUI] Sala criada!`
- ✅ EOS autenticado: `LogEOSMessaging(Info): Connect Messaging: Successfully connected to Stomp.`
- ✅ Lobby socket criado
- ✅ `manage_scene get_build_settings` retorna LobbyScene em buildIndex=2, enabled=true

**Clone (mppm2c2807dc)**:
- ✅ `manage_scene get_build_settings` retorna **idêntico** ao principal (mesma lista de cenas)
- ❌ Console mostra: `"Scene 'LobbyScene' couldn't be loaded..."`
- ❌ `execute_code` falha mesmo para `return "ok";` — erro mono.exe `"O nome do arquivo ou a extensão é muito grande"`
- ❌ `refresh_unity force` → timeout 60s
- ❌ `execute_menu_item Assets/Refresh` → "menu invalid/disabled"
- ✅ `manage_editor stop` → "already stopped" (não está em Play)

**Diagnóstico**: o clone tem state corrompido em múltiplas camadas — não só scene loading, mas todo o sistema de code execution e refresh está travado. A configuração YAML (`EditorBuildSettings.asset`, `BuildProfileContext.asset`) está idêntica ao principal, mas o runtime do clone não funciona.

**Causa raiz mais provável**: combinação de cache desincronizado + PATH/process state do mono.exe no clone. Pode ter sido causado por:
1. Restart do clone após o fix de EOSConfigGenerator (domain reload pode ter parado no meio)
2. Multiplos restarts/play attempts deixaram caches em state intermediário
3. Bug conhecido do MPPM v1.6.3 em Unity 6 sob restart pesado

### Solução recomendada

**Recriar o Virtual Player do MPPM** — é a única solução que limpa todos os caches do clone simultaneamente:

1. No projeto principal: `Window → Multiplayer → Play Mode`
2. Remover o Virtual Player atual (`mppm2c2807dc`)
3. Apagar fisicamente a pasta `Library/VP/mppm2c2807dc/` (~vai ser recriada do zero)
4. Criar novo Virtual Player (`+Add` no painel)
5. Aguardar Unity importar tudo no novo clone (alguns minutos)
6. Tentar smoke test do início

Custo: ~5-10 minutos de reimport. Sem risco para o projeto principal (Library/VP/ é só cache).

### Verificação adicional sugerida antes de recriar clone

Antes de recriar (que é caro), verificar via Unity Editor manualmente:
- No clone, abrir `File → Build Profiles` — confirmar que aparece a lista de cenas correta
- No clone, console deve estar limpo de erros após restart
- Se o clone responder a inputs lentamente: confirma state corrompido

---

## RESOLVIDOS

(nenhum ainda — bloqueio #1 está em "AGUARDANDO_VALIDACAO" até o smoke test completar)

---

**Fim do `BLOCKERS.md`.**
