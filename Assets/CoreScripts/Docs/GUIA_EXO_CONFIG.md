# Guia de Uso - Exo Config

Status: ativo
Publico: toda a equipe
Ultima atualizacao: 2026-08-07

Este guia explica, do zero, como usar o Exo Config no estado atual do projeto.
O foco aqui e operacao pratica: onde clicar, o que preencher, o que o plugin
faz sozinho, o que ele nao faz e como interpretar avisos.

Se voce nunca usou o plugin antes, leia na ordem. Se quiser apenas executar uma
importacao agora, pule direto para a secao 4.

---

## 1. O que e o Exo Config

O Exo Config e uma ferramenta de Editor da Unity para pegar um FBX selecionado
e transformar esse asset em uma estrutura pronta para uso no projeto.

Na pratica, ele faz quatro coisas principais:

1. Organiza os arquivos do asset em pastas padronizadas.
2. Cria ou atualiza materiais e prefabs.
3. Conecta referencias de rede, animacao e dados de gameplay.
4. Valida o resultado final para reduzir erro silencioso.

Importante:

- O plugin nao e Blender.
- O plugin nao exporta modelos 3D.
- O plugin nao cria controllers de animacao.
- O plugin nao escreve scripts de gameplay.
- O plugin nao corrige automaticamente dado de jogo mal configurado.

Ele parte de um FBX ja pronto, com textura e, quando necessario, animacoes e
um perfil de prefab configurado.

---

## 2. Conceitos basicos

### 2.1 O que o plugin considera como entrada

O fluxo normal comeca com:

- um arquivo `.fbx` selecionado no Project Window
- uma textura irmã, quando existir, com o nome `[Nome]T.png`
- opcionalmente, arquivos `.anim` colocados na mesma pasta do FBX

### 2.2 Os dois menus que importam

- `Exo Config > Edit`
- `Assets > Exo Prefabs > Organizar...`

O primeiro serve para configurar a base do sistema.
O segundo executa o pipeline sobre o FBX selecionado.

### 2.3 Os arquivos centrais

- `Assets/Editor/ExoConfig/ExoToolConfig.asset`
- `Assets/Editor/ExoConfig/ExoToolConfig.cs`
- `Assets/Editor/ExoPrefabProfile.cs`

O `ExoToolConfig.asset` e a fonte de verdade da ferramenta.
Ele guarda a lista de entidades, os overrides de pasta e o perfil vinculado a
cada entidade.

### 2.4 As tres categorias

- `Personagens`
- `Monstros`
- `Environment`

Cada categoria muda o comportamento do pipeline, o conjunto de pastas
esperado e o tipo de prefab gerado.

---

## 3. Antes de usar

### 3.1 Verifique a versao do projeto

Use a versao do Unity definida pelo projeto:

- `6000.3.10f1`

### 3.2 Verifique se o projeto esta compilando

Antes de usar o Exo Config:

1. Abra o projeto.
2. Espere o Unity terminar de importar.
3. Confirme que nao ha erros vermelhos na Console.

Se a compilacao estiver quebrada, o plugin vai continuar aparecendo no menu,
mas o resultado da execucao pode ficar incompleto ou nao carregar componentes
corretamente.

### 3.3 Verifique o nome do FBX

O nome do arquivo selecionado importa.
Boa pratica:

- use um nome de FBX canonico e estavel
- mantenha o nome do FBX alinhado com o nome da entidade
- evite renomear toda hora depois que a estrutura ja estiver sendo usada

O nome cadastrado no `ExoToolConfig` identifica a entidade no menu.
O nome do arquivo FBX ainda influencia o nome final dos arquivos gerados.

### 3.4 Verifique a textura

Se houver textura, o plugin procura a irmandade de nome:

- `[Nome]T.png`

Ela precisa estar na mesma pasta do FBX de origem no momento da execucao.

### 3.5 Verifique as animacoes

Se voce quer que o plugin mova animacoes soltas, coloque os arquivos `.anim`
na mesma pasta do FBX antes de rodar.

O Exo Config:

- move apenas arquivos `.anim`
- olha apenas a pasta imediata do FBX
- nao vasculha subpastas
- nao tenta adivinhar FBX extra como animacao

### 3.6 Verifique o perfil, se for Personagem

Para `Personagens`, o perfil e obrigatorio.
Sem `ExoPrefabProfile.basePrefab`, o pipeline para.

O `basePrefab` ideal e um prefab base ja preparado para o jogador, com a
estrutura que o jogo espera.

---

## 4. Uso rapido

Se voce quer executar o plugin agora, siga este fluxo.

1. Abra `Exo Config > Edit`.
2. Selecione a categoria na barra lateral.
3. Adicione ou confira a entidade que voce vai processar.
4. Se for `Personagens`, crie ou vincule um `ExoPrefabProfile` e preencha
   `basePrefab`.
5. No Project Window, selecione o `.fbx` de origem.
6. Clique com o botao direito e escolha `Assets > Exo Prefabs > Organizar...`.
7. Escolha a entidade no menu que abrir.
8. Espere o pipeline terminar.
9. Leia a Console e confira os arquivos gerados.

Resultado esperado:

- o FBX vai para a pasta de `Modelos`
- a textura vai para `Texturas`, se existir
- o material vai para `Materiais`
- o prefab vai para `Prefabs`
- animacoes soltas vao para `Animação`, se houver
- prefabs networked vao para a lista de rede correta

---

## 5. A janela Exo Config > Edit

Essa janela e usada para configurar a ferramenta.
Ela nao executa a importacao do FBX.
Ela so edita a base de dados que o pipeline usa depois.

### 5.1 Barra lateral

Na lateral esquerda voce escolhe:

- `Personagens`
- `Monstros`
- `Environment`

### 5.2 Campo de nova entidade

No topo da area principal existe um campo de texto para adicionar entidade.

Uso:

1. Digite o nome da entidade.
2. Clique em `Adicionar`.

O nome precisa ser unico dentro da categoria.
Se a entidade ja existir, a janela nao duplica.

### 5.3 Botao `Organizar v`

Esse menu reorganiza a lista da categoria atual.

Opcoes:

- `A-Z`
- `Data Criacao (Antigo-Novo)`
- `Data Modificacao (Novo-Antigo)`

Use isso para manter a lista legivel para o time.

### 5.4 Lista de entidades

Cada linha da lista mostra uma entidade cadastrada.

Controles:

- clique no nome para selecionar e editar
- clique em `X` para remover a entidade

Remover uma entidade tambem remove o pacote de dados dela na config, porque as
informacoes ficam agrupadas no mesmo `ExoToolConfigEntry`.

### 5.5 Caminhos de pasta

A janela mostra os caminhos resolvidos por convencao.
Se a entidade precisar fugir da convencao, use override.

Quando nao ha override:

- o campo aparece como leitura
- o caminho mostrado e o caminho calculado pela regra do projeto
- o botao disponivel e `Sobrescrever`

Quando ha override:

- o campo vira editavel
- o fundo fica destacado
- o botao vira `Reverter`

Uso recomendado:

- deixe a convencao sempre que possivel
- use override apenas para excecoes reais

Exemplos de excecao:

- uma entidade cuja pasta de animacao foi padronizada de forma diferente
- monstros que ficam na raiz da arvore de inimigos
- casos antigos que ja existem no projeto e nao devem ser normalizados agora

### 5.6 Secao de perfil

Essa e a parte mais importante para quem vai usar `Personagens`.

Voce pode:

- vincular um profile existente
- criar um profile novo
- selecionar o profile no Project Window

#### Botao `Criar Perfil`

Esse botao cria um novo `ExoPrefabProfile` na pasta `Prefabs` da entidade.

Defaults automáticos ao criar:

- `Personagens`: tag `Player`, layer `6`
- `Monstros`: tag `Enemy`, layer `7`
- `Environment`: tag `Untagged`, layer `0`

Depois de criar, a janela salva o caminho do profile dentro do
`ExoToolConfig.asset`.

#### Botao `Selecionar Perfil`

Esse botao abre o profile vinculado no Project Window.
Use isso para editar campos detalhados sem precisar procurar o asset manualmente.

#### Sem profile

Se a entidade nao tiver profile:

- o modo basico continua valendo para material e organizacao
- `Personagens` nao conseguem ser executados sem `basePrefab`
- `Monstros` e `Environment` podem funcionar, mas com menos automacao

---

## 6. Como configurar um ExoPrefabProfile

O `ExoPrefabProfile` e o asset que define como a entidade deve ser montada.

### 6.1 Campos mais importantes

| Campo | Quando usar | O que faz |
|---|---|---|
| `entityType` | sempre | define se o profile e `Personagem`, `Monstro` ou `Edificio` |
| `basePrefab` | obrigatorio para `Personagens` | base do Prefab Variant nativo |
| `abilityScripts` | `Personagens` | scripts adicionados na criacao do prefab, nao na atualizacao |
| `characterData` | `Personagens` | valida e vincula `commanderPrefab` e `towerPrefab` |
| `enemyData` | `Monstros` | valida e vincula `enemyPrefab` |
| `animatorController` | qualquer categoria com animacao | override manual do controller |
| `baseMapTexture` | qualquer categoria | textura principal do material |
| `shadingMapTexture` | qualquer categoria | textura de shading opcional |
| `gameObjectTag` | principalmente `Monstros` e `Environment` | tag do root do prefab |
| `gameObjectLayer` | principalmente `Monstros` e `Environment` | layer do root do prefab |

### 6.2 O que o profile nao faz

O profile nao:

- cria script de habilidade
- cria controller de animacao
- cria animacao
- cria modelagem
- corrige referencia ruim automaticamente

Ele so define como o Exo Config vai montar o prefab final.

### 6.3 Regras para Personagens

Para `Personagens`, respeite esta ordem:

1. crie ou escolha o profile
2. preencha `basePrefab`
3. preencha `abilityScripts`, se a personagem tiver scripts especificos
4. preencha `characterData`
5. confira `animatorController`, se houver

Se `basePrefab` estiver vazio, o pipeline para.
Sem isso, nao existe Prefab Variant valido.

### 6.4 Regras para Monstros

Para `Monstros`, o profile e altamente recomendado.

Use-o para:

- definir tag e layer corretos
- preencher `enemyData`
- apontar `animatorController`, se houver
- guardar parametros de material e colisao

### 6.5 Regras para Environment

Para `Environment`, o profile geralmente e mais simples.

Use-o para:

- padronizar material
- padronizar layer/tag
- guardar ajustes de colisao, se o objeto precisar

---

## 7. Como executar o pipeline no FBX

Essa e a acao principal do plugin.

### 7.1 Abrir o menu correto

1. No Project Window, selecione o arquivo `.fbx`.
2. Clique com o botao direito.
3. Escolha `Assets > Exo Prefabs > Organizar...`.

Se o asset selecionado nao for FBX, esse menu nao fica habilitado.

### 7.2 Escolher a entidade

O menu mostra as entidades cadastradas em `ExoToolConfig`.
Selecione a entidade correta para o FBX que voce quer processar.

O menu e gerado a partir da config atual no momento do clique.
Nao existe arquivo de menu gerado em disco.

### 7.3 O que o pipeline faz por baixo

O pipeline executa esta sequencia:

1. resolve a categoria e a entidade
2. resolve os caminhos finais
3. move o FBX e a textura, se existir
4. cria ou atualiza o material
5. cria ou atualiza o prefab
6. move `.anim` soltos e atribui animator controller, se houver
7. registra os prefabs de rede
8. valida referencias serializadas

### 7.4 O que acontece com cada tipo

#### Personagem

O plugin:

- cria ou atualiza um Prefab Variant nativo
- exige `basePrefab`
- troca so o modelo sob `Pivot`
- preserva a estrutura herdada do prefab base
- adiciona `abilityScripts` apenas na criacao
- cria tambem a torre derivada
- atualiza `CharacterBase.commanderPrefab` e `CharacterBase.towerPrefab`

#### Monstro

O plugin:

- monta o prefab do zero
- adiciona os componentes de comportamento esperados
- registra o prefab na lista de rede
- valida `EnemyDataSO`, se estiver vinculado

#### Environment

O plugin:

- monta o prefab do zero
- aplica material e configuracao do profile
- nao registra prefab de rede
- nao precisa de `Animacao` por convencao

---

## 8. O que sai automaticamente

### 8.1 Material

O Exo Config usa o shader do projeto para materializar o asset.

Pontos importantes:

- ele nao cria shader novo
- ele nao faz fallback silencioso para outro shader
- se o shader esperado nao existir, o pipeline para com erro

### 8.2 Prefab

O prefab final e gerado em `Prefabs`.

Nome geral esperado:

- `Personagens`: `[FBX name] Variant.prefab`
- `Monstros` e `Environment`: `[FBX name].prefab`
- torre derivada de personagem: `Torreta[Nome].prefab`

### 8.3 Animacao

Se houver arquivos `.anim` na pasta de origem do FBX, eles sao movidos para a
pasta de `Animação`.

Se houver um controller com o nome esperado:

- `<Nome>Animator.controller`

ele pode ser atribuido automaticamente.

Se nao houver controller:

- o plugin avisa
- a montagem nao para por isso
- a equipe precisa criar o controller manualmente

### 8.4 Rede

Os prefabs montados para `Personagens`, `Monstros` e qualquer outra entidade
networked sao registrados em:

- `Assets/Multiplayer/Setup/DefaultNetworkPrefabs.asset`

Esse e o asset usado pela cena do projeto.
O arquivo raiz `Assets/DefaultNetworkPrefabs.asset` nao e o alvo da ferramenta.

### 8.5 Validacao final

No fim, o plugin confere se referencias serializadas importantes continuam
apontando para o prefab certo.

Essa checagem e importante porque uma referencia pode parecer valida no Editor
e ainda assim falhar em build standalone se o `fileID` nao existir literalmente
no YAML salvo.

---

## 9. Como interpretar mensagens

### 9.1 Info

Significa que o processo seguiu normalmente.

Exemplos:

- nenhuma textura encontrada
- categoria sem pasta de animacao
- profile ausente em uma entidade que nao exige profile obrigatorio

### 9.2 Warning

Significa que o pipeline continuou, mas alguem deve conferir o resultado.

Exemplos:

- controller de animacao nao encontrado
- referencia de fileID nao bateu com o prefab salvo
- rig do modelo antigo pode ter perdido referencias ao trocar o FBX
- prefab de rede nao existe ou nao pode ser carregado para registro

### 9.3 Error

Significa que a etapa nao conseguiu concluir.

Exemplos:

- categoria ou entidade nao encontrada
- `basePrefab` ausente em `Personagens`
- shader esperado ausente
- lista de rede nao encontrada

Se houver `Error`, trate a execucao como incompleta.

---

## 10. Fluxos praticos por pessoa do time

### 10.1 Artista ou tecnico de arte

Use este fluxo:

1. exporte o FBX
2. coloque a textura com nome `[Nome]T.png`
3. coloque os `.anim` da entidade na mesma pasta, se existirem
4. abra `Exo Config > Edit` e confira se a entidade existe
5. selecione o FBX e rode `Organizar...`
6. revise o prefab gerado

### 10.2 Designer ou game designer

Use este fluxo:

1. abra `Exo Config > Edit`
2. confira se a entidade esta cadastrada
3. crie ou selecione o profile
4. ajuste `basePrefab`, dados de combate e referencias
5. rode o pipeline no FBX
6. verifique warnings na Console

### 10.3 Programador

Use este fluxo:

1. confira se a estrutura do `ExoToolConfig.asset` esta coerente
2. confira se o `ExoPrefabProfile` aponta para os assets corretos
3. rode o pipeline em um FBX de teste
4. veja se os prefabs gerados continuam batendo com as SOs de runtime
5. nao aceite warning de validacao sem investigar

---

## 11. Erros comuns e como resolver

### 11.1 O menu `Assets > Exo Prefabs > Organizar...` nao aparece

Possiveis causas:

- o asset selecionado nao e `.fbx`
- nao existe nada selecionado
- o projeto ainda nao recompilou os scripts de Editor

### 11.2 A entidade nao aparece no menu

Possiveis causas:

- a entidade nao foi cadastrada em `Exo Config > Edit`
- o `ExoToolConfig.asset` esta vazio ou nao foi salvo
- voce esta olhando a categoria errada

### 11.3 Personagem falha com erro de `basePrefab`

Causa:

- o `ExoPrefabProfile` de `Personagens` nao tem `basePrefab`

Solucao:

1. abra o profile
2. arraste o prefab base correto
3. rode o pipeline de novo

### 11.4 O prefab de rede nao registra

Possiveis causas:

- `Assets/Multiplayer/Setup/DefaultNetworkPrefabs.asset` nao existe
- o prefab montado nao tem `NetworkObject`
- voce abriu o asset errado na raiz do projeto

### 11.5 O controller de animacao nao foi atribuido

Possiveis causas:

- o controller nao existe
- o nome do controller nao segue `<Nome>Animator.controller`
- o controller esta em outro caminho

Solucao:

1. crie ou mova o controller para a pasta `Animação`
2. confira o nome
3. rode o pipeline novamente

### 11.6 O warning diz que referencias de rig podem ter ficado nulas

Isso significa que o modelo antigo foi trocado e parte da hierarquia interna
foi descartada.

Solucao:

1. abra o prefab gerado
2. confira os componentes que apontavam para ossos ou filhos internos
3. reatribua manualmente o que nao sobreviver ao update

Esse warning e esperado em casos onde o modelo novo nao preserva exatamente a
mesma arvore de bones.

### 11.7 O warning de fileID aparece

Isso significa que a referencia serializada merece revisao.

Solucao:

1. abra o ScriptableObject correspondente
2. reatribua o prefab correto no Inspector
3. salve de novo

Se a referencia funciona no Editor, mas falha em build, nao ignore esse aviso.

### 11.8 O material nao criou

Possiveis causas:

- shader do projeto nao esta disponivel
- o asset nao abriu corretamente
- o material folder do profile nao foi resolvido

---

## 12. Boas praticas da equipe

1. Prefira manter os nomes de FBX estaveis.
2. Use `Exo Config > Edit` para mudar configuracao, nao EditorPrefs.
3. Crie profile para `Personagens` antes de tentar rodar o pipeline.
4. Revise warnings de validacao sempre.
5. Nao crie controllers de animacao automaticamente no fluxo do plugin.
6. Se uma entidade precisa de pasta diferente, use override explicito.
7. Se uma referencia falhou depois de trocar o modelo, confira manualmente os
   filhos e bones que a nova arvore destruiu.
8. Se um asset de rede nao foi registrado, nao tente contornar isso no
   Inspector sem antes entender por que a lista certa nao foi atualizada.

---

## 13. Resumo de uma linha

Se o time precisar de uma frase unica:

1. configure a entidade em `Exo Config > Edit`
2. selecione o FBX
3. rode `Assets > Exo Prefabs > Organizar...`
4. revise a Console e o prefab gerado

---

## 14. Onde ler se voce for manter o plugin

- `Assets/CoreScripts/Docs/Estado_Atual_ExoConfig.md`
- `Assets/Editor/ExoConfigWindow.cs`
- `Assets/Editor/ExoPrefabMenu.cs`
- `Assets/Editor/ExoConfig/ExoToolConfig.cs`
- `Assets/Editor/ExoPrefabProfile.cs`
- `Assets/Editor/ExoPrefabBuilder.cs`

Esse guia e operacional.
O `Estado_Atual_ExoConfig.md` continua sendo o historico tecnico da refatoracao.
