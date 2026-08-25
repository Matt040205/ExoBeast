# Exo Bridge — Blender para Exo Config

O Exo Bridge é a fronteira versionada entre Blender 5.2 e o Exo Config. Ele
não substitui o fluxo manual existente: gera uma evidência em `Incoming`,
mostra uma prévia estruturada e só promove arquivos após confirmação humana.

## Contrato de pacote

O addon em `Tools/Blender/ExoBridge` gera:

- `Assets/ExoBridge/Incoming/<entidade>/<packageId>/exo-package.json`;
- um FBX em `model/`;
- texturas PNG/JPG/JPEG/TGA em `textures/`;
- cada Action em um FBX independente em `animations/`;
- cópia do `.blend` em `source/*.blend.zip`.

`Incoming` e `Backups` são ignorados pelo Git. O manifesto traz schema,
UUID, origem, versões do addon/Blender, eixos, escala, hashes SHA-256, slots e
Actions. A Unity bloqueia schema desconhecido, hash divergente, path traversal,
arquivo/extensão não compatível e configuração `forward=-Z`, `up=Y`, escala 1.

## Uso seguro

1. No Blender, exporte pelo painel **Exo Bridge Export** para a pasta `Assets`
   deste projeto. O addon aceita somente imagem direta no `Base Color` de um
   `Principled BSDF`; use `EXO_SHADING` para o mapa opcional.
2. Na Unity, abra **Exo Bridge > Configurar perfis**. Crie o
   `ExoPrefabProfile` da entidade e confirme explicitamente base prefab,
   ScriptableObjects, scripts, bindings de material (`rendererPath` relativo
   ao FBX + índice) e bindings de Action.
3. Abra **Exo Bridge > Pacotes**, clique **Prévia**, resolva todos os bloqueios
   e só então confirme **Importar pacote aprovado**.

A ponte não lê `Selection.activeObject`, não cria state machines e não converte
nodes/shaders Blender. Actions exigem `AnimatorOverrideController` existente e
um `targetClip` declarado para cada Action.

Em reimportação, somente modelo, materiais e clips com bindings explícitos são
elegíveis. Antes disso, a ponte abre os prefabs e bloqueia qualquer referência
externa para dentro da subárvore de modelo que seria removida. Para Ayame,
`CharacterBase.towerPrefab` ausente/órfão é um bloqueio de pré-voo: a correção
continua manual e nunca é feita silenciosamente pelo bridge.

## Validação

Rode no Blender:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --python Tools/Blender/ExoBridge/tests/headless_contract_test.py
```

No Unity, execute os EditMode tests `ExoBridgeManifestReflectionTests` e a
suíte já existente do Exo Config. Uma prévia ou importação recusada não deve
alterar assets canônicos; os arquivos que seriam substituídos recebem cópia em
`Assets/ExoBridge/Backups/<packageId>/` antes de uma promoção efetiva.
