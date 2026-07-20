# Estado Atual — Refatoração Exo Config

Status: ativo — refatoração em andamento, Fases 1–5 de 9 concluídas
Público: Claude (Sonnet 5), Codex, Gemini — qualquer agente que retome este trabalho
Última atualização: 17 Jul 2026, handoff para sessão em outra máquina
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

- **Branch:** `exo-config-refactor` (local). **Sem commits próprios ainda até este handoff** — todo o trabalho das Fases 1–5 estava em working tree não commitado (ver seção 9 sobre o commit que acompanha este documento).
- **Sem branch remota ainda** (`origin/exo-config-refactor` não existe até o push que acompanha este handoff).
- **Testes:** 139 passando, 0 falhas, assembly `ExoBeasts.EditorTests` (rodar via `mcp__UnityMCP__run_tests` + `mcp__UnityMCP__get_test_job`). Este número é a baseline que a Fase 6 em diante NÃO pode regredir.
- **Nota atual (autoavaliação Sonnet, sem revisão Opus completa):**

| Critério | Peso | Nota |
|---|---|---|
| Funcionalidade | 35% | 7,5 |
| Clean Code | 25% | 8,0 |
| Escalabilidade | 25% | 7,5 |
| Organização | 15% | 7,0 |
| **Geral (ponderado)** | | **~7,5 / 10** |

Progressão por fase: 2,4 (início) → 3,2 (Fase 1) → 4,6 (Fase 2) → 6,5 (Fase 3) → 6,5 (Fase 4) → **7,5 (Fase 5, atual)**.

- **Próxima ação pendente:** Fase 6 (relink de Torre/Monstro). **Não foi iniciada** — três tentativas de lançar o subagente falharam com `"claude-sonnet-5 is temporarily unavailable, so auto mode cannot determine the safety of Agent"` (indisponibilidade transitória do classificador de segurança do harness, não um bloqueio de design). O prompt completo já está escrito — ver seção 6.

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

## 6. Fase 6 — PRÓXIMA AÇÃO (bloqueada só por infra, não por design)

**Não foi iniciada.** Três tentativas de `Agent(model: "sonnet", run_in_background: true, ...)` falharam com o mesmo erro: `"claude-sonnet-5 is temporarily unavailable, so auto mode cannot determine the safety of Agent right now."` — isso é o classificador de segurança do harness (não o modelo de chat da sessão) temporariamente fora do ar. **Só tentar de novo** — na sessão anterior isso se resolveu sozinho depois de alguns minutos para outras chamadas (`read_console` chegou a falhar e depois funcionar sem nenhuma mudança de contexto).

### Objetivo
Corrigir o relink de referências para **Torre** e **Monstro** (Environment não usa relink — sem template original, é sempre criação nova via `ConfigureAsBuilding`). Hoje o relink só funciona, por acidente de estrutura, para Personagem (que a Fase 5 já corrigiu de outro jeito — nem usa mais esse relink).

### O bug exato (ainda vivo em `ConfigureAsTower`/`ConfigureAsEnemy`)
```csharp
string origToken = "Pivot/" + origFbxName;
string newToken = "Pivot/" + newFbxName;
if (origPath.StartsWith(origToken)) return newToken + origPath.Substring(origToken.Length);
return origPath;
```
(`ExoPrefabBuilder.MapRelativePath` — releia o arquivo atual, os números de linha mudaram bastante desde a Fase 5). Isso só remapeia caminhos que começam literalmente com `"Pivot/"`. `ConfigureAsTower`/`ConfigureAsEnemy` não criam nenhum `Pivot` — o modelo é filho direto do root. `origFbxName`/`newFbxName` ficam vazios, `MapRelativePath` devolve o caminho intocado, o `Find` falha, e a referência é gravada como **null** em silêncio. Modo de falha nº1 do plugin original.

### PROMPT COMPLETO, pronto para reenviar via `Agent` (copiar literalmente)

```
Você está executando a Fase 6 da refatoração do plugin Exo Config. Projeto Unity: C:\Users\zegil\Documents\GitHub\ExoBeasts_V3\PI3D, branch exo-config-refactor.

## Objetivo da Fase 6
Corrigir o relink de referências para Torre e Monstro (Environment não usa relink hoje — sem template original a copiar, é sempre criação nova). Hoje o relink funciona, por acidente, só para Personagem, e destrói referências em silêncio para Torre e Monstro.

## O bug exato (confirmado, não hipótese)
ExoPrefabBuilder.MapRelativePath (releia o arquivo atual — a Fase 5 já mudou bastante o arquivo, não confie em números de linha antigos) faz isto:
    string origToken = "Pivot/" + origFbxName;
    string newToken = "Pivot/" + newFbxName;
    if (origPath.StartsWith(origToken)) return newToken + origPath.Substring(origToken.Length);
    return origPath;
Isso só remapeia caminhos que começam literalmente com "Pivot/". ConfigureAsTower e ConfigureAsEnemy não criam nenhum Pivot — o modelo é filho direto do root. Então origFbxName/newFbxName (calculados a partir do primeiro filho de um nó chamado "Pivot", ver CopySerializedValuesAndRelink) ficam vazios para Torre/Monstro, MapRelativePath devolve o caminho intocado, o Find(mappedPath) falha, e CopyPropertyAndRelink grava a referência como null — silenciosamente. É o modo de falha nº1 do plugin original.

## Estado atual (Fases 1–5 validadas — NÃO refaça)
Assets/Editor/ExoConfig/Core/ (asmdef puro, noEngineReferences: true): ExoCategory, ExoPathResolver, ExoNaming, ExoEntityDefinition, ExoEnumParsing, ExoBuildReport, ExoOverrideMapBuilder, ExoLegacyPrefsMigration, ExoPickerItemBuilder, ExoInputActionsResolver, ExoFileIdPresenceChecker (novo na Fase 5 — verifica se um fileID aparece literalmente no YAML de um prefab; ainda não usado por nenhum step).

Assets/Editor/ExoConfig/Pipeline/: ExoBuildContext, IExoBuildStep, ExoBuildPipeline, Steps/{ResolvePathsStep,ImportAssetsStep,MaterialStep,BuildPrefabStep}.

Assets/Editor/ExoPrefabBuilder.cs: a Fase 5 reescreveu a metade de Personagem (BuildOrUpdateCharacterVariant, ReplaceModelUnderPivot, ApplyAbilityScripts — releia, é código novo) e deletou ConfigureAsCharacter/SetupCameraHierarchy. A metade de Torre (ConfigureAsTower) e o caminho de Monstro (ConfigureAsEnemy) continuam EXATAMENTE como no código original do Mateus — é isso que você vai corrigir agora. CopySerializedValuesAndRelink, CopyComponentsAndRelink, CopyPropertyAndRelink, GetRelativePath, MapRelativePath, IsChildOf, FindOriginalPrefab — todos intocados até agora, ainda usados por Torre/Monstro.

ExoPrefabProfile.cs ganhou basePrefab (GameObject) e abilityScripts (MonoScript[]) na Fase 5 — esses campos são só para Personagem, não mude o comportamento de Torre/Monstro por causa deles.

Baseline: 139 testes passando, 0 falhas. Não pode regredir.

## Escopo desta fase

### 1. Ancorar o relink na identidade do modelo, não no literal "Pivot/"
A ideia do plano original era usar PrefabUtility.GetCorrespondingObjectFromSource — mas investigue primeiro se isso se aplica aqui: essa API resolve a correspondência entre uma instância e o prefab de origem dela (útil quando os dois objetos vêm do MESMO prefab source), o que não é exatamente o caso aqui (estamos comparando um prefab TEMPLATE antigo com um prefab NOVO recém-instanciado a partir de um FBX diferente). Avalie se a abordagem certa é:
- (a) ancorar por identidade estrutural do nó-raiz do modelo (em vez de assumir literalmente "Pivot/<nome>", descubra dinamicamente qual filho do root é o modelo/FBX instanciado — ex.: o único filho que é uma instância de prefab/FBX, não um GameObject vazio criado pelo builder) tanto para Torre (ConfigureAsTower: modelo é filho direto do root, junto com GameObject/CirculoSeletor) quanto para Monstro (ConfigureAsEnemy: modelo é filho direto do root, junto com DamagePopupPosition/Sphere/Indicador_Aggro/Dissolvevfx); ou
- (b) alguma outra estratégia que você julgue mais robusta, desde que funcione para as DUAS estruturas (Torre e Monstro) sem assumir um nome de pasta fixo.

Documente a decisão e por quê. O objetivo: MapRelativePath/o mecanismo equivalente precisa saber "qual é o nó-modelo" em QUALQUER uma das 3 estruturas (Personagem com Pivot — já resolvido diferente na Fase 5 — Torre sem Pivot, Monstro sem Pivot), não só a que tem uma pasta chamada "Pivot".

### 2. Escopo estrito: só Torre e Monstro
Não toque na metade de Personagem (Fase 5, já aprovada). Não toque em Environment (não usa relink — ConfigureAsBuilding não tem template original, é sempre NavMeshModifier novo).

### 3. FindOriginalPrefab usa Contains fuzzy — risco real, documentado no plano
    if (name.ToLower().Contains(cleanEntity.ToLower())) { ... }
Isso já é um risco conhecido: Assets/Entidades/Inimigos/ tem tanto Aguia.prefab quanto Aguiaa.prefab, tanto Aranha.prefab quanto Aranhaa.prefab, e a ordem de AssetDatabase.FindAssets não é determinística. Corrigir isso pertence ao escopo desta fase se e somente se for necessário para o relink funcionar corretamente — avalie e decida. Se decidir corrigir, prefira comparação exata (ou exata + fallback fuzzy só como aviso, nunca como match silencioso) e explique. Se decidir NÃO mexer nisso agora (por ser um problema ortogonal ao bug do relink), documente explicitamente por que está ficando pra depois e não deixe a lacuna sem registro.

### 4. Usar o ExoFileIdPresenceChecker (Fase 5) como guard real
Agora que Torre/Monstro também vão gerar Variants efetivamente (ou pelo menos ter relink correto), adicione um teste/validação que usa ExoFileIdPresenceChecker.ContainsFileId para confirmar que um fileID relinkado (não apenas herdado) realmente aparece no YAML salvo — não precisa virar um step formal do pipeline ainda (isso é Fase 7/ValidateStep), mas prove que o checker se aplica ao cenário real que essa fase introduz.

## Como provar com segurança
Não regenere entidades reais de Monstro/Torre em produção (Aranha, Águia, Escorpião, Capanga, Monstro, e as torres derivadas de Personagem como TorretaSamurai) sem cuidado extremo — são prefabs referenciados por EnemyDataSO/CharacterBase/listas de rede. Prove a correção com uma entidade descartável de scratch (mesma disciplina da Fase 5: copie um FBX pequeno para uma pasta temporária, rode a lógica de verdade sobre a cópia, confirme via leitura de YAML que o relink funcionou — incluindo o cenário que hoje QUEBRA: um segundo run com o FBX trocado de nome, tipo o samurai→samurai 2 que o comentário original do dossiê citava), e delete tudo no final.

Se quiser confirmar contra um caso real só de leitura (sem escrever), pode inspecionar o YAML de prefabs de Monstro já existentes (ex.: comparar Assets/Entidades/Inimigos/Aranha.prefab como ele é hoje) para embasar o desenho — isso é seguro, é só leitura.

## Contrato do projeto (Assets/Diretrizes_Multiagente.md)
Confirmar nomes reais antes de citar/editar (o código mudou bastante na Fase 5, releia tudo). Preservar mudanças existentes. Marcar incerteza quando não puder confirmar algo.

## Armadilhas conhecidas
- execute_code do UnityMCP pode estar QUEBRADO neste ambiente (era o caso na máquina anterior — testar de novo nesta máquina, não assumir) — se estiver, use create_script + [MenuItem] temporário + execute_menu_item, delete no fim.
- git ls-tree não lista diretório vazio — use disco (ls/find).
- Bridge MCP-Unity pode ficar instável — instância PI3D pode sumir e voltar, e o classificador de segurança do harness também já ficou temporariamente indisponível para chamadas de tool nesta sessão. Se algo falhar com "instance not found" ou "temporarily unavailable", tente de novo — geralmente resolve.
- Se run_tests/get_test_job continuarem falhando após várias tentativas, você pode ler C:\Users\zegil\AppData\LocalLow\DefaultCompany\PI3D\TestResults.xml diretamente do disco como evidência do último run (confira o timestamp start-time/end-time pra garantir que é posterior às suas mudanças) — mas prefira sempre confirmar com uma chamada MCP fresca primeiro.

## Definição de pronto
1. Relink funciona para Torre E Monstro num teste de scratch com nome de FBX trocado (o cenário que hoje quebra).
2. Metade de Personagem (Fase 5) e Environment não tocados.
3. Zero diff em prefabs/assets reais de produção (Torre/Monstro existentes).
4. Testes via MCP: run_tests (mode EditMode, assembly ExoBeasts.EditorTests) + get_test_job (wait_timeout 60). Baseline 139 — não pode regredir. Adicione testes para toda lógica pura extraída.
5. read_console (types: ["error"]) sem erro de compilação.
6. Nenhum script/artefato de scratch sobrando.

## Relatório final (obrigatório)
O que você decidiu para "ancorar por identidade" e por quê (com o que descartou e por quê). O que mudou em cada arquivo. Transcrição completa do teste de scratch, incluindo o cenário de nome trocado. Decisão sobre FindOriginalPrefab/Contains fuzzy (corrigiu ou não, e por quê). Números de teste antes/depois. Estado do console. git status --short final. O que não conseguiu confirmar. Se discordar de algo do briefing, diga por quê em vez de implementar contrariado.
```

### Como validar a Fase 6 depois que ela rodar (metodologia usada nas fases 1–5, repita)
1. **Não confie no relatório do subagente sozinho.** Leia o diff real (`git diff Assets/Editor/ExoPrefabBuilder.cs`) e confirme que só a metade de Torre/Monstro mudou.
2. Rode `mcp__UnityMCP__run_tests` (mode EditMode, assembly `ExoBeasts.EditorTests`) + `mcp__UnityMCP__get_test_job` (wait_timeout 60) você mesmo — não aceite o número que o subagente diz sem reproduzir.
3. `mcp__UnityMCP__read_console` (types: ["error"]) — confirmar console limpo.
4. `git status --short` — confirmar que nenhum arquivo de produção (prefabs reais de Monstro/Torre, `EnemyDataSO`, listas de rede) tem diff inesperado.
5. Procure por artefatos de scratch esquecidos (`find Assets -iname "*Scratch*" -o -iname "*Diagnostic*" -o -iname "*TEMP*"`).
6. Leia o código novo do relink (a função de "ancorar por identidade") e confirme que faz sentido pras duas estruturas (Torre e Monstro), não só uma.

---

## 7. Fases restantes (7, 8, 9) — ainda não iniciadas

### Fase 7 — AnimatorStep + NetworkRegistrationStep + ValidateStep
- **AnimatorStep**: move `.anim`/FBX de animação pra pasta resolvida (`ExoPathResolver` já resolve `ExoAssetType.Animacao` pra Personagem/Monstro) e resolve o controller por convenção (`<Nome>Animator.controller`) com override no profile. Escopo honesto: organiza e atribui, não gera máquina de estados (controllers são autorais). Brunhilde e Coral não têm nenhuma animação/controller hoje — degradar com warning, não exceção.
- **NetworkRegistrationStep**: registra o prefab na lista viva `Assets/Multiplayer/Setup/DefaultNetworkPrefabs.asset` (confirmado: é a referenciada pela `MenuScene`; `Assets/DefaultNetworkPrefabs.asset` na raiz é órfã, não referenciada em nenhuma cena/prefab do projeto). Substitui o `Debug.LogWarning("ACAO NECESSARIA: Arraste os prefabs...")` que ainda existe em `ExoPrefabBuilder.BuildCharacterPrefab`.
- **ValidateStep**: liga o `ExoFileIdPresenceChecker` (Fase 5) como step de verdade no pipeline — depois de salvar um prefab de Personagem/Torre/Monstro, ler o fileID gravado no `CharacterBase`/`EnemyDataSO` correspondente e confirmar que existe literalmente no YAML salvo. Isso transforma a regra de memória do projeto (fileID virtual quebra em build standalone) numa checagem automática, não só numa lembrança.

### Fase 8 — Corrigir dado de produção: `Ayame.asset → towerPrefab` órfão
**Ainda não corrigido.** Confirmado na sondagem original: `Ayame.asset.towerPrefab` aponta pro GUID `fd0bbd1c417566a43800d83168a82c10`, que **não existe em nenhum `.meta` do repositório** — foi provavelmente produzido por uma execução real e nunca commitada do plugin antigo (`samurai 3`/4/5 na raiz de `Assets/` são as sobras dessa execução). Fix: restaurar pro `TorretaSamurai.prefab`, fileID `3333250326587255744` (validado nesta sessão anterior: presente 12× no YAML do prefab real). É o único estrago de dado real que a sondagem original encontrou que precisa correção — todo o resto (grafias de pasta divergentes, `Mina` no registro sem pasta, `Assets/DefaultNetworkPrefabs.asset` órfão) fica como está, por decisão já confirmada com o usuário (sem normalização de assets existentes).

### Fase 9 — Atualizar documentação
`Assets/Diretrizes_Multiagente.md` diz "se um comportamento mudar, atualizar a documentação afetada" — isso ainda não foi feito. Não existe hoje, dentro do repositório git, nenhum documento equivalente ao dossiê `message.txt` que o usuário colou no início (esse dossiê é um arquivo local em `Downloads/`, fora do repo, e descreve o comportamento ANTIGO). Esta fase deveria: (a) criar a versão correta/atual desse dossiê dentro do repo (provavelmente `Assets/CoreScripts/Docs/` ou `Assets/Editor/ExoConfig/`), descrevendo o comportamento real pós-refatoração; (b) atualizar este próprio arquivo (`Estado_Atual_ExoConfig.md`) removendo o que virou histórico.

---

## 8. Tarefa pendente separada: revisão adversarial completa por Opus

Registrada como tarefa própria durante a sessão anterior (não uma fase numerada do plano — é sobre TODAS as fases já feitas). Objetivo: reler os diffs de cada fase (1–6 em diante) linha a linha contra a intenção declarada de cada uma; conferir que nenhuma referência de prefab virou null; checar que os caminhos acentuados (`Configurações`, `Animação`, `Escorpião`, `Águia`) foram cobertos por teste real; confirmar que os guards de segurança (arquivo gerado, fileID de Variant) seguem funcionando; reavaliar a nota com os critérios do plano em vez de aceitar as autochecagens do Sonnet como validação final.

**Estado:** não executada. O usuário confirmou (na mensagem que originou este handoff) que a continuação também será em Sonnet 5 — ou seja, essa revisão pode não acontecer da forma originalmente planejada (Opus, sessão direta). Se o usuário quiser uma segunda opinião independente sem trocar de modelo, `/code-review ultra` (revisão multi-agente na nuvem, cobrada à parte, roda sobre o branch atual) é a alternativa mais próxima disponível — mas é acionada pelo usuário, não por um agente.

---

## 9. Sobre o commit que acompanha este handoff

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

- [ ] Confirmar que `git log --oneline -3` mostra o commit desta refatoração no topo da branch `exo-config-refactor`
- [ ] Rodar os 139 testes (`ExoBeasts.EditorTests`, EditMode) e confirmar que passam nesta máquina antes de tocar em qualquer coisa
- [ ] Relançar a Fase 6 com o prompt da seção 6 (retry se der erro de classificador indisponível — é transitório)
- [ ] Validar a Fase 6 de forma independente (seção 6, "Como validar")
- [ ] Seguir pra Fase 7 → 8 → 9, mesma disciplina de validação a cada fase
- [ ] Antes de considerar o plugin "pronto para uso real": alguém (game designer ou o próprio usuário) precisa criar `ExoPrefabProfile` para Ayame/Brunhilde/Coral/Sylvie com `basePrefab = Player 1.prefab` e a lista certa de `abilityScripts` por personagem — sem isso a ferramenta recusa operar em qualquer Personagem real (por design, não é bug)
- [ ] Ao final de tudo, se o usuário quiser uma segunda opinião independente de verdade: sugerir `/code-review ultra` ou aguardar Opus disponível para a revisão da seção 8
