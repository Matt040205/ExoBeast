# Guia de Setup Multiplayer - Cenas e Prefabs

Status: ativo
Publico: quem monta cenas e prefabs do multiplayer
Ler primeiro: `Estado_Atual_Multiplayer.md`
Nao usar como fonte de verdade: `SETUP_INSTRUCTIONS.md` e guias antigos de migracao

Guia operacional atual para configurar cenas, prefabs e testes do multiplayer.

## Fluxo atual

```text
NetworkBootstrap.unity -> MenuScene.unity -> LobbyScene.unity -> CenaSeleçao.unity -> CenaMapaNOVO.unity
```

Cenas de teste isolado:
- `EOSAuthTest.unity` — testa apenas autenticacao EOS
- `Network Test.unity` — testa Host/Client NGO sem EOS Lobby

- `LobbyScene.unity` e a cena de entrada do fluxo multiplayer.
- `CenaMapaNOVO.unity` e a cena de gameplay carregada pela rede (o nome `CenaMapaTeste` e legado).
- `CenaSeleçao.unity` e carregada pelo NGO entre o lobby e a partida.
- `NetworkBootstrap.unity` nao existe neste repositorio; o bootstrap e feito via componente `NetworkBootstrap.cs` na cena `NetworkBootstrap.unity` em `Assets/Cenas/`.

## LobbyScene.unity

Coloque aqui o runtime que precisa sobreviver ate a troca de cena:

- `NetworkManager`
- `UnityTransport`
- objeto do plugin externo `PlayEveryWare EOSManager`
- `EOSManagerWrapper`
- `EOSAuthenticator`
- `SessionManager`
- `UGSBootstrap`
- `LobbyManager`
- `LobbySceneUI` como interface canonica

Observacoes:

- O `NetworkManager` precisa continuar vivo ate `CenaMapaNOVO`.
- O fluxo atual de lobby publica o endereco de conexao e aguarda os clientes antes de carregar a cena de jogo.
- `LobbyPlaceholderUI.cs` e `MenuLobbyPanel.cs` foram deletados no Sprint 6 — nao adicionar de volta.
- `LobbyUIManager.cs` e tombstone `#if UNITY_EDITOR [Obsolete]` — nao e UI real, nao usa em producao.

## Player prefab

No prefab do jogador, mantenha os componentes locais do gameplay e adicione os componentes de rede abaixo:

- `NetworkObject`
- `ClientNetworkTransform`
- `NetworkAnimator`
- `PlayerNetworkSetup`

Componentes locais que o prefab normalmente ja possui:

- `PlayerMovement`
- `PlayerHealthSystem`
- `PlayerShooting`
- `MeleeCombatSystem`
- `PlayerCombatManager`
- `CameraController`
- `CharacterController`
- `LocalPlayerInputBridge`

Campos que o `PlayerNetworkSetup` tenta resolver:

- `movement`
- `cameraController`
- `characterController`
- `playerShooting`
- `meleeCombat`
- `playerCombatManager`
- `localInputBridge`
- `localOnlyObjects`

Regras:

- Nao use `NetworkedPlayerController` como fonte principal do setup local/remoto.
- O setup atual acontece em `PlayerNetworkSetup.OnNetworkSpawn()`.
- O jogador remoto deve ter input, camera e objetos locais desligados.

## 4. CenaMapaNOVO.unity (antes chamada SceneMapTest)

Hierarquia sugerida para a cena de gameplay:

### 4.1 GameManager

- `NetworkObject`
- `GameSetupManager`
- `MatchManager`
- `CurrencyManager`

### 4.2 NetworkSystems

- `NetworkObject`
- `PlayerRegistry`
- `GameServerManager`
- `PlayerIdentityBridge`

### 4.3 HordeSystem

- `NetworkObject`
- `HordeManager`
- `EnemyPoolManager`

### 4.4 Objective

- `NetworkObject`
- `ObjectiveHealthSystem`
- `Collider`

### 4.5 Spawn points

- `SpawnPoint_1`
- `SpawnPoint_2`
- `SpawnPoint_3`
- `SpawnPoint_4`

### 4.6 Pontos de inimigos

- Crie os transforms que o `HordeManager` vai usar como spawn points.

Notas:

- Todo objeto que tenha `NetworkBehaviour` precisa de `NetworkObject` na raiz.
- Os managers de gameplay que vivem fora da pasta multiplayer continuam validos, mas devem
  ser tratados como parte do cenario e nao como parte do core de rede.

## 5. NetworkManager e prefabs registrados

Registre no `NetworkManager` todo prefab que for spawnado pela rede:

- prefab do jogador
- prefabs de inimigo
- prefabs de torre
- prefabs de armadilha
- qualquer outro prefab que chame `NetworkObject.Spawn()`

Se um prefab nao estiver na lista, o cliente nao vai conseguir instanciar o objeto.

## 6. Build Settings

Garanta que estas cenas existam no build (use os nomes exatos que aparecem nas strings do codigo):

- `NetworkBootstrap`
- `MenuScene`
- `LobbyScene`
- `CenaSeleçao`
- `CenaMapaNOVO`
- `EOSAuthTest`
- `Network Test`
- `Win`
- `Lose`

## 7. Teste com MPPM

1. Abra `LobbyScene.unity`.
2. Rode o editor principal e faca login.
3. Crie ou entre em um lobby.
4. No clone MPPM, entre no mesmo lobby.
5. Verifique se o host publica os dados de conexao.
6. Inicie a partida.
7. Confirme que ambos carregam `CenaSeleçao.unity` → `CenaMapaNOVO.unity`.

O que checar:

- ambos os jogadores aparecem na sala
- cada jogador se move de forma independente
- o jogador remoto nao rouba camera ou input
- o `ConnectionApproval` carrega o personagem correto
- nao ha `AudioListener` duplicado
- nao ha erro de `NetworkPrefab not found`

## 8. Troubleshooting

- Player nao spawna: confira `NetworkObject` no prefab e a lista de prefabs do `NetworkManager`.
- Jogador nao se move: confira `ClientNetworkTransform` e `PlayerNetworkSetup`.
- Kamera pisca: confira se componentes remotos foram desativados.
- Cliente nao conecta: confira `SERVER_ADDRESS`, `RELAY_CODE` e o estado do `UGSBootstrap`.
- Personagem errado: confira `CharacterChoiceCache` e o payload de `ConnectionApproval`.

## 9. Referencia rapida

### LobbyScene

- `NetworkManager`
- `UnityTransport`
- `EOSManager` do plugin externo
- `EOSManagerWrapper`
- `EOSAuthenticator`
- `SessionManager`
- `UGSBootstrap`
- `LobbyManager`
- `LobbySceneUI`

### Player prefab

- `NetworkObject`
- `ClientNetworkTransform`
- `NetworkAnimator`
- `PlayerNetworkSetup`

### CenaMapaNOVO

- `GameSetupManager`
- `MatchManager`
- `CurrencyManager`
- `PlayerRegistry`
- `GameServerManager`
- `PlayerIdentityBridge`
- `HordeManager`
- `EnemyPoolManager`
- `ObjectiveHealthSystem`
