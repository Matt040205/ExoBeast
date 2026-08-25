# Exo Bridge — Blender 5.2

O addon `Exo Bridge Export` cria um pacote verificável para a janela Unity
`Exo Bridge > Pacotes`. Ele não importa nada na Unity e não altera o Exo Config
sozinho.

## Instalação

No PowerShell, a partir desta pasta, execute `./build_addon_zip.ps1`. Em
Blender 5.2, abra **Edit > Preferences > Add-ons > Install from Disk**, escolha
`dist/ExoBridge-1.0.0.zip` e habilite o addon. O painel fica em **View 3D > N >
Exo Bridge**.

## Exportação

1. Salve o `.blend`.
2. Em **Assets Path**, selecione a pasta `Assets` deste checkout Unity.
3. Informe o nome idêntico ao cadastrado no Exo Config, categoria e objeto
   raiz. A exportação inclui a raiz e seus descendentes Mesh/Empty/Armature.
4. Para cada material, use um `Principled BSDF` ligado diretamente a uma
   `Image Texture` no `Base Color`. PNG, JPG/JPEG e TGA são aceitos.
5. Opcionalmente, uma segunda `Image Texture` com nome ou label
   `EXO_SHADING` é exportada como mapa `_shadingMap`.
6. Exporte. O pacote será criado em
   `Assets/ExoBridge/Incoming/<entidade>/<uuid>/`.

O contrato fixa `forward=-Z`, `up=Y`, `globalScale=1` e `applyUnitScale=true`.
Cada Action é exportada como FBX separado. A cópia do `.blend` é guardada em
`source/*.blend.zip`, para que a Unity nunca a trate como modelo.

## Limites deliberados

- Nodes procedurais, mixagem de shaders, normal/roughness maps e qualquer
  ligação de Principled que não seja `Base Color` são recusados; não há
  conversão de shader Blender.
- O nome de cada slot é `<Mesh>[índice]::<Material>` e precisa de um binding
  explícito no `ExoPrefabProfile`: `rendererPath` relativo à raiz do FBX e
  `rendererMaterialIndex`. A ponte nunca tenta aplicar um slot em todos os
  Renderers.
- O addon só escreve em `Incoming`. A promoção para os caminhos canônicos só
  ocorre depois de prévia aprovada e confirmação humana na Unity.
