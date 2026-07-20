# Estado Atual — Refatoração Exo Config

Status: ativo — refatoração em andamento, Fases 1–7 de 9 concluídas
Público: Claude (Sonnet 5), Codex, Gemini — qualquer agente que retome este trabalho
Última atualização: 20 Jul 2026, sessão em máquina nova (clone fresco) — Fases 6 e 7 executadas e commitadas nesta sessão (commits `362aa467` e `bf379782`)
Não usar como fonte de verdade: o dossiê `message.txt` que o usuário colou no início desta refatoração (descreve o comportamento ANTIGO/quebrado do plugin, não o atual)

Leia este documento inteiro antes de tocar em qualquer arquivo `Assets/Editor/Exo*.cs` ou `Assets/Editor/ExoConfig/`. Ele existe porque a sessão anterior rodou numa máquina diferente da que vai continuar este trabalho — o plano original (`~/.claude/plans/...md`) e a memória entre sessões do Claude Code são **locais à máquina antiga** e não viajam com o `git pull`. Este arquivo é a única fonte de contexto garantida de chegar na próxima máquina.

---

## 1. O que é o Exo Config e por que este trabalho existe

O Exo Config é uma ferramenta de Editor da Unity (não tem nenhuma relação com Blender — isso foi confirmado por sondagem exaustiva: zero arquivos `.py`/`.blend`, zero menções a "blender" em qualquer `.cs`/`.md`/`.json` do repositório) escrita por Mateus (commits de 8 Jul 2026, "Começo do mod" → "Mod quase 100% funcional"), pensada para automatizar 100% o fluxo de importar um FBX+textura e gerar prefabs prontos (física, scripts de rede, colisão, habilidades, material, referências) sem configuração manual no Inspector.

**O autor não conseguiu explicar o próprio código ao usuário.** Segundo o usuário, "ele vibecodou sem entender nada". Pedido do usuário para começar o trabalho:

> "primeiro sonde tudo antes de criarmos um plano, tudo que seja desse plugin blender e unity" ... "a ideia seria um auto export com todos os parâmetros necessários para o nosso projeto para a unity, tudo em um clique, e a ideia seria manter as referências nesse export, ou seja, se estamos trabalhando na ayame, ele já exporta tudo que é necessário para a unity em prefabs e organiza tudo, pastas, nomes, objetos e animações" ... "com critérios extremamente rigorosos de clean code, organização, escalabilidade e funcionalidade... me entregue um plano... dê uma nota ao plugin, com base nos seus critérios rigorosos, faça que ele atinga nota 9.5+"

**Critério de sucesso explícito do usuário: nota ≥ 9,5/10** em Funcionalidade (peso 35%), Clean Code (25%), Escalabilidade (25%), Organização (15%). **Nota inicial atribuída ao plugin original: 2,4/10.**

---

## 2. Metodologia de execução — LEIA ANTES DE CONTINUAR

O plano aprovado define uma divisão rígida de papéis:

| Papel | Modelo | Como |
|---|---|---|
| **Execução** | Claude Sonnet 5 | Subagente por fase, via `Agent` tool com `model: "sonnet"`, `run_in_background: true` |
| **Validação** | Opus 4.8 | O modelo da sessão, diretamente — nunca delegada a subagente |

**Regra de ouro original:** nenhuma fase avança sem validação aprovada da anterior. Uma fase reprovada volta para o Sonnet com o defeito nomeado — não é corrigida em silêncio pelo validador.

### ⚠️ Desvio em vigor nesta sessão (o usuário já confirmou, não precisa reperguntar)

Durante a execução, o modelo Opus 4.8 ficou **temporariamente indisponível** (erros `"claude-opus-4-8 is temporarily unavailable"` em chamadas de `Agent`/`run_tests`). O usuário trocou o modelo da sessão para Sonnet 5 (via `/model claude-sonnet-5`) e autorizou explicitamente:

> "como o modelo opus está indisponível, execute tudo que é possível no sonnet agora e ao fim de todas as tarefas o opus revisa"

Ou seja: **Sonnet 5 fez tanto a execução (subagentes) quanto a validação (checagem direta: ler diffs, rodar testes via MCP, checar console, verificar escopo do `git status`)** — sem ser Opus, mas seguindo a mesma disciplina de nunca aceitar um relatório de subagente sem verificação independente. Isso é **mais fraco** que uma revisão adversarial Opus de verdade (que faria perguntas mais difíceis, criticaria decisões de design com mais profundidade), mas não é "aceitar cegamente" — cada fase abaixo foi validada por leitura de código linha a linha, não só pelo relatório do executor.

**Existe uma tarefa registrada** (ver seção 8) para uma revisão adversarial completa por Opus quando disponível. **Na mensagem que originou este handoff, o usuário confirmou que a próxima sessão também vai continuar em Sonnet 5** — ou seja, essa revisão Opus pode nunca acontecer como o plano original previa, a menos que o usuário peça explicitamente (ex.: rodar `/code-review ultra`, que é multi-agente na nuvem e não depende do modelo da sessão) ou o Opus volte a ficar disponível e o usuário queira trocar de volta.

---

## 3. Estado atual (resumo executivo)

- **Branch:** `exo-config-refactor`, com remoto (`origin/exo-config-refactor` existe e está sincronizado — commits até `bf379782` já dados push nesta sessão).
- **Máquina desta sessão é NOVA** (clone fresco, diferente das duas anteriores) — **sem bridge MCP-Unity conectado** (o pacote `com.coplaydev.unity-mcp` está em `Packages/manifest.json`, mas não há ferramentas `mcp__UnityMCP__*` disponíveis nesta sessão de Claude Code). Toda validação de Fases 6 e 7 usou Unity em modo batch via linha de comando em vez de MCP:
  ```
  "<EditorPath>\Unity.exe" -batchmode -nographics -projectPath "<projeto>" -runTests -testPlatform EditMode -assemblyNames ExoBeasts.EditorTests -testResults "<xml>" -logFile "<log>"
  ```
  para testes, e `-executeMethod <Classe>.<Metodo> -quit` para rodar um script de scratch temporário quando precisa de código sob medida. Funciona bem, mas **só uma instância do Unity pode segurar o lock do projeto por vez** — se o Editor estiver aberto (GUI, MPPM, etc.), todo comando batch falha com erro de lock até fechar.
- **Testes:** 192 passando, 0 falhas, assembly `ExoBeasts.EditorTests`. Baseline que a Fase 8 em diante NÃO pode regredir. Progressão: 139 (fim da Fase 5) → 165 (Fase 6, +26) → 192 (Fase 7, +27).
- **Nota do plano (Funcionalidade/Clean Code/Escalabilidade/Organização):** não recalculada nesta sessão (a tabela abaixo é histórica, até Fase 5). Fases 6–7 não passaram por revisão adversarial Opus — ver seção 8, ainda pendente.

| Critério | Peso | Nota (até Fase 5) |
|---|---|---|
| Funcionalidade | 35% | 7,5 |
| Clean Code | 25% | 8,0 |
| Escalabilidade | 25% | 7,5 |
| Organização | 15% | 7,0 |
| **Geral (ponderado)** | | **~7,5 / 10** |

Progressão por fase: 2,4 (início) → 3,2 (Fase 1) → 4,6 (Fase 2) → 6,5 (Fase 3) → 6,5 (Fase 4) → 7,5 (Fase 5) → Fases 6–7 concluídas, nota não recalculada.

- **Próxima ação pendente:** Fase 8 (corrigir `Ayame.asset.towerPrefab` órfão) — ver seção 7. Fase 6 (relink Torre/Monstro) e Fase 7 (Animator/NetworkRegistration/Validate) estão concluídas, commitadas e com push feito — ver seções 6 e 6.1.

---

## 4. Arquitetura alvo (visão geral)

```
Assets/Editor/ExoConfig/
├── Core/                                    ← asmdef PURO, zero deps de jogo/Unity, 100% testável
│   ├── ExoBeasts.ExoConfig.Core.asmdef       (references: [], noEngineReferences: true — o COMPILADOR impede UnityEngine/UnityEditor aqui)
│   ├── ExoCategory.cs                        (enum Personagens/Monstros/Environment)
│   ├── ExoPathResolver.cs                    (convenção de pastas + overrides — função pura)
│   ├── ExoNaming.cs                          ([Nome]T.png, Torreta[Nome], [Nome] Variant, CleanEntityName)
│   ├── ExoEnumParsing.cs                     (ExoCategoryParser/ExoAssetTypeParser — string→enum canônico)
│   ├── ExoEntityDefinition.cs                (DTO: Nome/Categoria/FolderOverrides — só primitivos)
│   ├── ExoOverrideMapBuilder.cs              (ExoEntityDefinition[] → Dictionary de overrides)
│   ├── ExoBuildReport.cs                     (mensagens Info/Warning/Error estruturadas)
│   ├── ExoLegacyPrefsMigration.cs            (parser puro do formato legado EditorPrefs)
│   ├── ExoPickerItemBuilder.cs               (monta lista ordenada/agrupada do picker de menu)
│   ├── ExoInputActionsResolver.cs            (resolve qual caminho acentuado/não-acentuado existe)
│   └── ExoFileIdPresenceChecker.cs           (Fase 5 — verifica se um fileID existe no YAML de um prefab; AINDA NÃO usado por nenhum step, isso é Fase 7/ValidateStep)
│
├── ExoToolConfig.cs / ExoToolConfig.asset    ← SO VERSIONADO, substitui EditorPrefs (Assembly-CSharp-Editor, pode usar UnityEngine/Editor)
├── ExoConfigEditorPrefsMigrator.cs           ← casca impura do migrador one-shot + MenuItem
├── Pipeline/
│   ├── ExoBuildContext.cs                    (estado da execução: categoria, nome, paths, profile, report, DryRun)
│   ├── IExoBuildStep.cs
│   ├── ExoBuildPipeline.cs                   (executa steps em ordem, para no 1º erro, try/finally em Start/StopAssetEditing)
│   └── Steps/
│       ├── ResolvePathsStep.cs
│       ├── ImportAssetsStep.cs               (move FBX+textura, checa retorno de MoveAsset, cria pasta se faltar)
│       ├── MaterialStep.cs                   (shader ToonExobeasts sempre, sem fallback silencioso)
│       └── BuildPrefabStep.cs                (chama ExoPrefabBuilder.BuildCharacterPrefab)
│
Assets/Editor/ExoConfigWindow.cs              ← janela "Exo Config > Edit", lê/escreve ExoToolConfig (não EditorPrefs)
Assets/Editor/ExoPrefabMenu.cs                ← 1 MenuItem "Assets/Exo Prefabs/Organizar..." + picker (GenericMenu), ExecutarOrganizar(categoria, nome) roda o pipeline
Assets/Editor/ExoPrefabBuilder.cs             ← núcleo de montagem. Personagem = Prefab Variant nativo (Fase 5). Torre/Monstro = ainda montagem do zero + relink frágil (Fase 6 corrige)
Assets/Editor/ExoPrefabProfile.cs             ← ScriptableObject de perfil por entidade: basePrefab (Fase 5), abilityScripts (Fase 5), dados de material/física/etc

Assets/Tests/Editor/ExoConfig/                ← usa ExoBeasts.EditorTests.asmdef (já referenciava Core desde a Fase 1)
```

**Restrição técnica que molda tudo isso:** asmdefs não podem referenciar `Assembly-CSharp` (assemblies predefinidas da Unity). Por isso o `Core/` é isolado e puro — qualquer código que precise de `PlayerMovement`/`TowerController`/`CharacterBase`/etc. (tipos definidos em `Assembly-CSharp`) fica fora do asmdef, na raiz de `Assets/Editor/ExoConfig/` ou em `Assets/Editor/` (que compilam implicitamente em `Assembly-CSharp-Editor`, que SIM pode referenciar `Assembly-CSharp`).

---

## 5. O que cada fase corrigiu (1–5, todas concluídas e validadas)

### Fase 1 — Fundação (`Core/` + asmdef + testes)
Criou o assembly puro `ExoBeasts.ExoConfig.Core` com `ExoPathResolver`/`ExoNaming` extraídos (não reimplementados — copiados fielmente) do comportamento real do `ExoPrefabBuilder`/`ExoPrefabMenu` originais. **Não mudou nenhum comportamento ainda.** 81 testes (46 novos). Achado de correção: `char.ToUpper` sem cultura invariante em `TowerBaseName` (bug de dependência de locale — corrigido para `ToUpperInvariant`).

### Fase 2 — Matar o `EditorPrefs`
Toda a config (lista de entidades, caminhos de pasta, perfil vinculado) vivia no registro do Windows (`EditorPrefs`) — não versiona, some entre máquinas. Substituído por `ExoToolConfig.asset` (versionado), semeado com as 10 entidades reais e os 2 overrides reais confirmados no disco:
- Sylvie → `Animação/Terranomas/Arqueira` (aninhamento diferente de Ayame)
- Monstros → prefabs na **raiz** de `Assets/Entidades/Inimigos/` (não em `<Nome>/Prefabs/`, que existe mas está vazia)

**Armadilha neutralizada:** `ExoPrefabMenu.GenerateMenus()` fazia `File.WriteAllText` incondicional sobre `ExoGeneratedMenus.cs`; com a config vazia (comum numa máquina nova), isso **apagava o arquivo gerado**, que era o único registro sobrevivente das 10 entidades. Corrigido com dupla defesa: trocar a fonte de dados + guard que recusa sobrescrever um arquivo populado com uma classe vazia. 99 testes.

**Migrador one-shot** (`ExoConfigEditorPrefsMigrator`, menu "Exo Config/Migrar EditorPrefs (One-Shot)"): lê o `EditorPrefs` antigo (se existir — nesta máquina está vazio) e importa pro `ExoToolConfig`. Lógica de parsing é pura/testada no Core; só a leitura real do registro é impura, injetada via `Func<string,string>`.

### Fase 3 — Matar o codegen
`ExoGeneratedMenus.cs` (118 linhas geradas, commitadas) foi **deletado**. Substituído por `ExoPrefabMenu.BuildPickerItems()` (lê `ExoToolConfig` em tempo real) + 1 único par de `[MenuItem]` que abre um `GenericMenu` agrupado por categoria. 115 testes. Provado em execução via `[MenuItem]` de diagnóstico temporário (deletado depois) — as 10 entidades reais (incluindo `Águia`/`Escorpião` acentuadas) aparecem corretamente na ordem certa.

### Fase 4 — Pipeline de steps + higiene
`ExoBuildPipeline`/`IExoBuildStep`/4 steps (`ResolvePaths`/`ImportAssets`/`Material`/`BuildPrefab`). 126 testes.

**Achado factual importante (correção de algo que EU tinha dito errado ao usuário antes desta fase):** minha sondagem original disse que o shader `"Toon/Toon"` não existia no projeto (grep restrito a `Assets/` não achou nada). **Estava incompleto.** `Shader.Find("Toon/Toon")` na verdade resolve, via o pacote `com.unity.toonshader@0.13.4-preview` (instalado, fora de `Assets/`), para `UnityToon.shader` — um shader toon genérico de terceiros, sem nenhuma relação com o `ToonExobeasts.shadergraph` próprio do projeto. Ou seja: o bug real não era "cai em silêncio pro fallback URP/Lit" (esse fallback nunca era alcançado, era código morto) — era "usa em silêncio um shader toon genérico errado, visualmente parecido mas inconsistente". **Decisão confirmada e implementada: todas as categorias usam `Shader Graphs/ToonExobeasts`, sem fallback — se não resolver, `ExoBuildReport.Error` e aborta.**

Outras correções: `AssetDatabase.MoveAsset` tinha o retorno de erro ignorado (agora checado); material sempre recriado via `CreateAsset` (trocava GUID, quebrava referências externas — agora atualiza in-place se já existe); `_MainTex` setado à toa (removido — `ToonExobeasts` não expõe essa propriedade); `Start/StopAssetEditing` agora em `try/finally`.

### Fase 5 — Personagem via Prefab Variant nativo + update-in-place (A MAIS ARRISCADA)
**Esta é a fase que ataca a promessa central do usuário** ("trabalhando na Ayame, reexporta e mantém as referências"). 139 testes (126 + 13 novos do `ExoFileIdPresenceChecker`).

**Confirmado por inspeção real do YAML (não suposição):**
- `Assets/Personagens/Player 1.prefab` é um prefab **plano** (não-Variant), root com 5 filhos diretos: `CM_Normal`, `CameraTarget`, `CM_Aim`, `Pivot`, `SwordPoint`. **`SwordPoint` é IRMÃO de `Pivot`, não filho** (o código antigo `ConfigureAsCharacter` construía errado, colocando `SwordPoint` dentro de `Pivot`).
- `Pivot` tem 1 filho: um **Prefab Instance aninhado** (não um FBX cru) apontando pra um modelo placeholder.
- `Samurai Variant.prefab` (Ayame) e `Coruja Arqueira.prefab` (Sylvie) **JÁ SÃO** Prefab Variants reais de `Player 1.prefab` (confirmado via `m_SourcePrefab` no YAML, guid `a0dbf9148972383479f04912c1ec19f8`).
- **Nenhuma das 4 entidades reais de Personagem (Ayame/Brunhilde/Coral/Sylvie) tem `ExoPrefabProfile` configurado hoje** (`ProfileAssetPath` vazio nas 4, em `ExoToolConfig.asset`) — ou seja, rodar a ferramenta em qualquer uma delas hoje bate no guard "sem fallback silencioso" e aborta com erro. **Isso é um pré-requisito manual pendente** (ver seção 7).

**Mudança implementada:** `ExoPrefabBuilder.BuildOrUpdateCharacterVariant` (novo método privado, substitui `ConfigureAsCharacter`+`SetupCameraHierarchy`, ambos **deletados** — confirmado via grep que não tinham outro chamador):
- **Criação** (prefab não existe): `PrefabUtility.InstantiatePrefab(profile.basePrefab)` → troca modelo sob `Pivot` → aplica material → aplica `abilityScripts` do profile → `SaveAsPrefabAsset` → Unity produz um Prefab Variant nativo de `basePrefab`.
- **Update-in-place** (prefab já existe): `PrefabUtility.LoadPrefabContents(prefabPath)` → troca **só** modelo sob `Pivot` + material + `Animator.runtimeAnimatorController` → `SaveAsPrefabAsset` de volta no mesmo caminho → `PrefabUtility.UnloadPrefabContents`. **Nada mais é tocado** — é a garantia central que o usuário pediu.
- `ExoPrefabProfile.basePrefab` (GameObject) é **obrigatório** para Personagem — sem ele, erro explícito, sem fallback silencioso (mesmo espírito da decisão de shader da Fase 4).
- `ExoPrefabProfile.abilityScripts` (`MonoScript[]`) substitui o array hardcoded de 13 nomes que `ConfigureAsCharacter` instalava em **qualquer** personagem — confirmado que isso fazia a Ayame receber `VooGraciosoLogic`/`CacadoraNoturnaLogic`, que são scripts da Sylvie. Agora é por perfil, type-safe (sobrevive a rename de classe).
- A metade de **Torre** dentro de `BuildCharacterPrefab` (que ainda usa `ConfigureAsTower` + `CopySerializedValuesAndRelink`) **NÃO foi tocada** — confirmado via `git diff`, é escopo da Fase 6.
- Se `BuildOrUpdateCharacterVariant` falhar (perfil/basePrefab ausente), o método **aborta antes** de montar a Torre e antes de sobrescrever `commanderPrefab`/`towerPrefab` no `CharacterBase` — não deixa dado velho parecendo válido.

**Limitação real, descoberta e documentada (não corrigida — decisão deliberada, ver justificativa abaixo):** objetos presos a um osso do MODELO antigo (ex.: `AimTarget_Fixo`/`GripRig`/`GripRig hint`/`firepoint` minúsculo dentro do `Samurai Variant.prefab` real) são destruídos quando o modelo é trocado sob `Pivot`, porque Unity destrói a subárvore inteira do filho antigo. Campos que apontavam pra dentro dessa subárvore (`PlayerMovement.aimRig/aimTarget/aimConstraint`, `PlayerShooting.firePoint`, `WeaponGripIK.gripRig`) ficam nulos. **Isso NÃO é uma regressão desta fase** — o código antigo tinha exatamente a mesma lacuna (`CopySerializedValuesAndRelink` só copia componentes pra objetos que já existem no alvo por caminho igual; nunca cria um GameObject novo, então `AimTarget_Fixo` nunca seria encontrado no `charRoot` recém-construído do zero e seria descartado em silêncio mesmo pelo código antigo). A diferença: agora existe um `Debug.LogWarning`/`report.Warning` explícito depois de toda troca de modelo, avisando pra conferir manualmente — antes, isso quebrava sem ninguém perceber.

**`ExoFileIdPresenceChecker`** (novo, Core, puro): codifica em código a regra de memória do projeto "Prefab Variants — fileID quebrado em builds standalone" (bug histórico de 29 Abril/2 Maio 2026: fileID "virtual" resolve no Editor mas vira null em build standalone). Ainda não é um step do pipeline (isso é Fase 7/`ValidateStep`), mas existe e está testado (13 testes) porque é agora que o risco volta a existir de verdade.

**Como foi provado:** entidade 100% descartável (`_ScratchFase5Test`, FBXs copiados via `AssetDatabase.CopyAsset`, nunca registrada em `ExoToolConfig.asset`, tudo deletado ao final) — **nunca rodou o pipeline de verdade sobre Ayame/Sylvie/Player 1.prefab reais** (proibido explicitamente no briefing desta fase, porque a revisão adversarial completa que normalmente aconteceria antes de mexer no que o jogo spawna não rolou com o Opus indisponível). Confirmado: fileID de um componente (`FaceCameraBillboard`) idêntico antes/depois de um update-in-place — a garantia central de estabilidade de fileID se sustenta.

**Contaminação incidental pega e revertida:** testar prefabs com `NetworkObject` disparou o hook automático do NGO, que reescreveu `Assets/DefaultNetworkPrefabs.asset`; a cena `Assets/Cenas/MenuScene.unity` e `ProjectSettings/URPProjectSettings.asset` também pegaram mudanças incidentais durante a sessão. Todos os três foram revertidos via `git checkout HEAD --` e reconfirmados com diff vazio.

---

## 6. Fase 6 — CONCLUÍDA (relink de Torre/Monstro)

Commit `362aa467`. Corrigia o bug que só funcionava por acidente para Personagem: `MapRelativePath` assumia literalmente um nó `"Pivot/"`, mas `ConfigureAsTower`/`ConfigureAsEnemy` nunca criam Pivot — referências para dentro do modelo eram gravadas como null em silêncio sempre que o FBX era reimportado com nome diferente (cenário `samurai` → `samurai 2`).

- **`ExoPrefabBuilder.FindModelChild`** (novo): acha o filho-modelo por identidade estrutural (`PrefabUtility.GetCorrespondingObjectFromSource(child) != null`), não por nome de pasta fixo. A primeira tentativa (`GetPrefabAssetType == Model`) parecia certa e passava nos testes, mas falhou no teste de scratch com FBX renomeado — `GetPrefabAssetType`/`GetPrefabInstanceStatus` respondem "como está sendo visto agora", que diverge entre uma hierarquia viva e um Prefab Asset recarregado do disco. `GetCorrespondingObjectFromSource` é a API estável nos dois contextos.
- **`ExoRelinkPathMapper`** (novo, Core): `MapRelativePath` puro e testado, ancorado no nome do nó-modelo em vez de `"Pivot/"`.
- **`ExoOriginalPrefabMatcher`** (novo, Core) + `FindOriginalPrefab` reescrito: prioriza igualdade exata sobre fuzzy-`Contains`, corrigindo colisão real (`Aguia`/`Aguiaa`, `Aranha`/`Aranhaa` — ambos existem em `Assets/Entidades/Inimigos/`) que podia relinkar contra o template errado em silêncio. Fuzzy virou fallback com aviso explícito.

Personagem (Fase 5) e Environment intocados. 139 → 165 testes (26 novos).

## 6.1. Fase 7 — CONCLUÍDA (AnimatorStep + NetworkRegistrationStep + ValidateStep)

Commit `bf379782`. Acrescenta 3 steps ao pipeline: `ResolvePaths → ImportAssets → Material → BuildPrefab → Animator → NetworkRegistration → Validate`.

- **AnimatorStep**: move `.anim` soltos para a pasta `Animação`, atribui `RuntimeAnimatorController` por convenção (`<Nome>Animator.controller`, novo em `ExoNaming`), respeita override de `ExoPrefabProfile.animatorController`. Só organiza/atribui — nunca cria controller nem máquina de estados. Degrada com Warning (nunca Error) quando a entidade não tem controller ainda (caso real: Brunhilde/Coral).
- **NetworkRegistrationStep**: registra o(s) prefab(s) montados em `Assets/Multiplayer/Setup/DefaultNetworkPrefabs.asset` (confirmado por GUID, não só por nome, que é o `NetworkPrefabsList` referenciado de verdade por `MenuScene.unity` — o da raiz do projeto é órfão), sem duplicar. Substitui o `Debug.LogWarning("ACAO NECESSARIA...")` antigo em `ExoPrefabBuilder`.
- **ValidateStep**: liga o `ExoFileIdPresenceChecker` (Fase 5, existia desde então mas sem chamador) como guard real — confirma que o fileID gravado em `CharacterBase`/`EnemyDataSO` aparece literalmente no YAML do prefab recém-salvo. Nunca usa Error (é o último step, sem rollback; SO não configurada ainda é estado normal, não falha).
- `ExoPrefabBuilder.BuildCharacterPrefab` (5 sobrecargas) passou de `void` para `List<string>` (`ExoBuildContext.BuiltPrefabPaths`) — os novos steps precisam saber exatamente quais prefabs a execução tocou (Personagem monta 2: ele mesmo + a Torre derivada, sempre juntos, nessa ordem).

**Dois bugs reais encontrados e corrigidos durante a verificação** (o subagente que implementou nunca chegou a rodar nada — o Editor estava aberto pelo usuário; achados vieram da minha verificação independente depois, com teste de scratch real, não hipótese):
1. `MaterialStep`/`BuildMaterial` (código da Fase 4) nunca criava a pasta `Materiais` antes de salvar o material — mesma lacuna que `ImportAssetsStep` já corrigia para Modelos/Texturas, nunca replicada aqui. Só não aparecia porque toda entidade já cadastrada em produção já tem a pasta; quebrou no primeiro teste com uma entidade genuinamente nova.
2. `ExoFileIdPresenceChecker`/`ValidateStep` verificavam só o número do fileID, nunca o guid. Confirmado com dados reais: o fileID `919132149155446097` (convenção da Unity para a raiz de um modelo importado) se repete em pelo menos 2 FBX distintos deste projeto — sem checar o guid, `ValidateStep` podia confirmar "certo" uma referência apontando pro asset ERRADO, um falso positivo pior que não validar nada. `ExoScriptableObjectReferenceParser.ExtractGuid` (novo) + guard de guid em `ValidateStep` (confere guid antes de aceitar o fileID) corrigem isso.

**Achado importante, NÃO corrigido nesta fase (característica real do Unity, não bug do ValidateStep — vale registrar para quem for configurar profiles):** a raiz de um Personagem Variant só ganha fileID literal no YAML se algo nela for sobrescrito em relação ao `basePrefab`. Hoje, só `ExoPrefabProfile.abilityScripts` faz isso — confirmado ao vivo que renomear a raiz (que `BuildOrUpdateCharacterVariant` sempre faz) não basta. Ou seja: **qualquer personagem cujo profile fique com `abilityScripts` vazio terá `commanderPrefab` marcado por `ValidateStep` como possível `null` em build standalone.** Não é um bug — é o guard fazendo exatamente o que devia — mas é algo a checar quando alguém configurar os profiles de Ayame/Brunhilde/Coral/Sylvie (nenhuma tem profile hoje, ver Fase 5 acima). Relacionado: `commanderPrefab` agora é atribuído a partir de um `AssetDatabase.LoadAssetAtPath` fresco (como `towerPrefab` já fazia), não do retorno bruto de `SaveAsPrefabAsset` — melhoria real, mas não resolve sozinha o ponto acima.

165 → 192 testes (27 novos).

---

## 7. Fases restantes (8, 9) — ainda não iniciadas

### Fase 8 — Corrigir dado de produção: `Ayame.asset → towerPrefab` órfão
**Ainda não corrigido.** Confirmado na sondagem original: `Ayame.asset.towerPrefab` aponta pro GUID `fd0bbd1c417566a43800d83168a82c10`, que **não existe em nenhum `.meta` do repositório** — foi provavelmente produzido por uma execução real e nunca commitada do plugin antigo (`samurai 3`/4/5 na raiz de `Assets/` são as sobras dessa execução). Fix: restaurar pro `TorretaSamurai.prefab`, fileID `3333250326587255744` (validado nesta sessão anterior: presente 12× no YAML do prefab real). É o único estrago de dado real que a sondagem original encontrou que precisa correção — todo o resto (grafias de pasta divergentes, `Mina` no registro sem pasta, `Assets/DefaultNetworkPrefabs.asset` órfão) fica como está, por decisão já confirmada com o usuário (sem normalização de assets existentes).

### Fase 9 — Atualizar documentação
`Assets/Diretrizes_Multiagente.md` diz "se um comportamento mudar, atualizar a documentação afetada" — isso ainda não foi feito. Não existe hoje, dentro do repositório git, nenhum documento equivalente ao dossiê `message.txt` que o usuário colou no início (esse dossiê é um arquivo local em `Downloads/`, fora do repo, e descreve o comportamento ANTIGO). Esta fase deveria: (a) criar a versão correta/atual desse dossiê dentro do repo (provavelmente `Assets/CoreScripts/Docs/` ou `Assets/Editor/ExoConfig/`), descrevendo o comportamento real pós-refatoração; (b) atualizar este próprio arquivo (`Estado_Atual_ExoConfig.md`) removendo o que virou histórico.

---

## 8. Tarefa pendente separada: revisão adversarial completa por Opus

Registrada como tarefa própria durante a sessão anterior (não uma fase numerada do plano — é sobre TODAS as fases já feitas, agora 1–7). Objetivo: reler os diffs de cada fase linha a linha contra a intenção declarada de cada uma; conferir que nenhuma referência de prefab virou null; checar que os caminhos acentuados (`Configurações`, `Animação`, `Escorpião`, `Águia`) foram cobertos por teste real; confirmar que os guards de segurança (arquivo gerado, fileID de Variant, guid em `ValidateStep`) seguem funcionando; reavaliar a nota com os critérios do plano em vez de aceitar as autochecagens do Sonnet como validação final.

**Estado:** não executada. O usuário confirmou (na mensagem que originou este handoff) que a continuação também será em Sonnet 5 — ou seja, essa revisão pode não acontecer da forma originalmente planejada (Opus, sessão direta). Se o usuário quiser uma segunda opinião independente sem trocar de modelo, `/code-review ultra` (revisão multi-agente na nuvem, cobrada à parte, roda sobre o branch atual) é a alternativa mais próxima disponível — mas é acionada pelo usuário, não por um agente.

---

## 9. Sobre o commit que acompanha este handoff (histórico — Fases 1–5)

Nota (20 Jul 2026): a partir da Fase 6, cada fase virou um commit próprio já com push (`362aa467` = Fase 6, `bf379782` = Fase 7) — não um único commit "de handoff" acumulado como abaixo. O texto original desta seção (sobre o PRIMEIRO commit da refatoração, que juntou as Fases 1–5) fica como histórico.

Até este ponto, **nenhuma das Fases 1–5 tinha sido commitada** — tudo vivia em working tree não versionado na branch local `exo-config-refactor` (que também não tinha remoto ainda). O commit que acompanha este documento inclui:
- Todos os arquivos `Assets/Editor/Exo*.cs` modificados/deletados (Fases 1–5)
- `Assets/Editor/ExoConfig/` inteiro (novo)
- `Assets/Tests/Editor/ExoConfig/` inteiro (novo)
- `Assets/Tests/Editor/ExoBeasts.EditorTests.asmdef` (referência ao novo asmdef, Fase 1)
- Este documento (`Assets/CoreScripts/Docs/Estado_Atual_ExoConfig.md`) e o pointer adicionado em `Assets/Diretrizes_Multiagente.md`

**Deliberadamente EXCLUÍDO do commit** (encontrado no working tree, sem nenhuma relação com este trabalho):
- `Assets/UI/fontes/Fraunces_72pt_SuperSoft-Regular SDF.asset`
- `Assets/UI/fontes/PaytoneOne SDF.asset`

Esses dois arquivos de fonte apareceram modificados (timestamps de hoje) provavelmente como efeito colateral dos múltiplos domain reloads da Unity durante esta sessão (recompilar scripts de Editor repetidamente pode disparar reimport de outros assets). Não tenho confirmação se são ruído incidental ou trabalho de outra pessoa em andamento — por segurança, ficaram **fora deste commit, mas também não foram revertidos**: continuam no working tree desta máquina, exatamente como estavam, para quem usar esta máquina depois decidir o que fazer com eles.

---

## 10. Checklist rápido para quem retomar

- [x] Fases 6 e 7 concluídas, commitadas e com push (`362aa467`, `bf379782`)
- [ ] Confirmar que `git log --oneline -3` mostra `bf379782` no topo da branch `exo-config-refactor`
- [ ] Rodar os 192 testes (`ExoBeasts.EditorTests`, EditMode) e confirmar que passam nesta máquina antes de tocar em qualquer coisa
- [ ] Se não houver bridge MCP-Unity conectado na sua sessão, usar Unity em modo batch via CLI (ver seção 3) em vez de `mcp__UnityMCP__*` — funciona bem, só exige rodar tudo em sequência (um Unity por vez segurando o lock do projeto)
- [ ] Seguir pra Fase 8 → 9, mesma disciplina de validação a cada fase (implementar → provar com scratch descartável → reproduzir testes você mesmo, não confiar só no relatório do subagente → `git status --short` limpo)
- [ ] Antes de considerar o plugin "pronto para uso real": alguém (game designer ou o próprio usuário) precisa criar `ExoPrefabProfile` para Ayame/Brunhilde/Coral/Sylvie com `basePrefab = Player 1.prefab` e a lista certa de `abilityScripts` por personagem — sem isso a ferramenta recusa operar em qualquer Personagem real (por design, não é bug). **Atenção (achado da Fase 7):** se algum personagem ficar com `abilityScripts` vazio, `ValidateStep` vai avisar que `commanderPrefab` pode virar null em build standalone — não é bug da ferramenta, é a raiz do Variant genuinamente sem fileID literal; considere se isso precisa de solução antes de publicar.
- [ ] Ao final de tudo, se o usuário quiser uma segunda opinião independente de verdade: sugerir `/code-review ultra` ou aguardar Opus disponível para a revisão da seção 8
