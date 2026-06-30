# Onboarding — ExoBeasts V3

Status: ativo
Público: desenvolvedor novo no projeto
Última atualização: 2026-06-30

Guia de primeiro acesso. Siga na ordem — cada seção depende da anterior.

---

## 1. Pré-requisitos

| Item | Versão / instrução |
|------|--------------------|
| Unity | **6000.3.10f1** — use exatamente esta versão (a LTS alvo do projeto) |
| MPPM | `com.unity.multiplayer.playmode` v1.6.3 — já incluso via Package Manager |
| Better FMOD | Instalado como pacote local — necessário para compilar |
| EOS Plugin | `com.playeveryware.eos` — pacote local em `Packages/` |
| Git LFS | Instalar antes de clonar (assets binários usam LFS) |

**Sobre versões do Unity:** abrir com uma versão diferente da especificada vai causar erros de serialização nos ScriptableObjects e prefabs. Não usar 6000.0.x — a versão alvo é 6000.3.

---

## 2. Setup inicial

### 2.1 Clonar o repositório

```bash
git lfs install     # sempre antes do primeiro clone
git clone <url>
```

### 2.2 Abrir no Unity Hub

1. No Unity Hub, clicar em "Open" e selecionar a pasta do projeto
2. Confirmar que o Unity Hub usa a versão `6000.3.10f1`
3. Aguardar a compilação inicial (pode demorar alguns minutos na primeira vez)
4. Se aparecer erros de compilação relacionados ao FMOD ou EOS, ver seção 5 (Erros comuns)

### 2.3 Configurar credenciais EOS

O jogo usa Epic Online Services para autenticação e lobby. Sem credenciais, o Play Mode não funciona.

1. Copiar `EOSCredentials.json.template` para `EOSCredentials.json` na raiz do projeto
2. Abrir `EOSCredentials.json` e preencher com as credenciais do Epic Dev Portal
3. O arquivo já está no `.gitignore` — nunca será commitado acidentalmente

Para instruções detalhadas, ver `Assets/Multiplayer/CREDENTIALS_SETUP.md`.

---

## 3. Primeira execução

1. No Unity, abrir a cena `Assets/Cenas/LobbyScene.unity`
2. Clicar em Play
3. No Console do Unity, confirmar que aparece algo como:
   - `[EOSManagerWrapper] Inicializado com sucesso`
   - `[EOSAuthenticator] Login bem-sucedido: <ProductUserId>`
4. A tela de lobby deve aparecer. Se a autenticação falhar, ver seção 5.

---

## 4. Teste multiplayer local (MPPM)

Para simular dois jogadores sem precisar de dois computadores:

1. Abrir `Window > Multiplayer Play Mode`
2. Na janela MPPM, adicionar 1 Virtual Player (total: Editor principal + 1 clone)
3. Clicar Play no Editor (o Editor será o host)
4. O clone abre automaticamente numa janela separada

**Fluxo de teste:**
- Editor (host): criar lobby → escolher personagem → clicar Pronto
- Clone (cliente): entrar no lobby → escolher personagem → clicar Pronto
- Editor (host): clicar "Iniciar Partida" (habilitado quando todos estão prontos)
- Ambos devem carregar a cena de seleção, depois a cena de jogo

**Dica de diagnóstico:** manter o Console do Unity aberto em ambas as janelas. O Console do clone aparece no canto inferior da tela do clone.

---

## 5. Mapa das cenas

```
NetworkBootstrap.unity
  └─► MenuScene.unity
        └─► LobbyScene.unity          ← lobby EOS, selecao de sala
              └─► EscolherPersonagem.unity  ← selecao de personagem (via NGO)
                    └─► CenaMapaNOVO.unity  ← partida de jogo
```

Cenas de teste isolado (fora do fluxo normal):
- `EOSAuthTest.unity` — testa apenas autenticação EOS
- `Network Test.unity` — testa Host/Client NGO sem EOS Lobby

**Importante:** `CenaMapaTeste` aparece apenas em código legado. O nome canônico atual é `CenaMapaNOVO`.

---

## 6. Mapa das pastas

```
Assets/
├── Cenas/             ← todas as cenas canônicas do projeto
├── CoreScripts/       ← sistemas de gameplay (Enemy, Managers, Towers, UI, Combat, Audio)
│   ├── Base/          ← CharacterBase.cs (ScriptableObject compartilhado por commanders e torres)
│   └── Docs/          ← documentação técnica do projeto ← VOCÊ ESTÁ AQUI
├── Personagens/       ← assets de personagem (scripts, prefabs, SOs, animações de todos os commanders)
│   └── AbilitySystem/ ← Ability.cs, CommanderAbilityController.cs, PassivaAbility.cs
├── Multiplayer/       ← código NGO + EOS (Auth, Lobby, Sync, GameServer)
│   ├── Setup/         ← DefaultNetworkPrefabs.asset (lista NGO de prefabs)
│   └── Docs/          ← guides de auth e credentials
├── Armadilhas/        ← scripts de armadilhas (Broca, Espinhos, Fogueira, Piche, Teleportador)
├── Entidades/
│   └── Inimigos/      ← prefabs dos inimigos (Aguia, Aranha, Capanga, Escorpião, MONSTRO)
├── VFXgenerico/       ← efeitos visuais compartilhados
└── Editor/            ← scripts de Editor (EOSConfigGenerator, etc.)
```

---

## 7. Arquivos importantes para ler

### Multiplayer e rede
| Arquivo | Por que ler |
|---------|-------------|
| `PADROES_NGO.md` (esta pasta) | Padrões que causaram bugs reais — ler antes de mexer em qualquer NetworkBehaviour |
| `Estado_Atual_Multiplayer.md` (esta pasta) | Estado canônico: o que existe hoje, o que foi deletado, o que mudou |
| `Guia_Game_Designer.md` (esta pasta) | Explicação sistema a sistema do multiplayer, com status de migração |
| `Guia_Setup_Multiplayer_Cenas.md` (esta pasta) | Como montar cenas, prefabs e NetworkManager corretamente |
| `Assets/Multiplayer/CREDENTIALS_SETUP.md` | Como configurar EOS para desenvolvimento e CI/CD |

### Sistemas de gameplay
| Arquivo | Por que ler |
|---------|-------------|
| `GUIA_PERSONAGENS.md` (esta pasta) | CharacterBase SO, prefab de commander, habilidades Q/E/X, sistema Rastros |
| `GUIA_TORRES_ARMADILHAS.md` (esta pasta) | towerData, caminhos de upgrade, TowerBehavior, TrapDataSO dual prefab |
| `GUIA_INIMIGOS_E_ONDAS.md` (esta pasta) | EnemyDataSO, fórmulas de scaling, WaveConfig, HordeManager Inspector |

---

## 8. Erros comuns de primeiro dia

### EOS não inicializa / Login falha

**Causa:** `EOSCredentials.json` não existe ou campos estão vazios.

**Fix:** copiar `EOSCredentials.json.template` → `EOSCredentials.json` e preencher com as credenciais do projeto.

### Player não se move após spawn (host)

**Causa:** a coroutine `FinishLocalSetupNextFrame()` foi interrompida — geralmente por NullReferenceException em algum Manager antes do PlayerInput ser re-habilitado.

**Diagnóstico:** verificar se o Console mostra `[PlayerNetworkSetup] PlayerInput configurado no ActionMap 'Player'.`. Se não aparecer, algum código novo foi adicionado antes do bloco do PlayerInput.

**Referência:** `PADROES_NGO.md` — Padrão P4.

### Player não se move após spawn (cliente)

**Causa:** dois `PlayerInput` ativos competem pelo teclado.

**Diagnóstico:** no Console do clone, verificar se aparece `[PlayerMovement] SetupOwnerInputFallback: PlayerInput e bridge configurados`.

**Referência:** `bug_host_client_movement.md` na memória Claude (ver Seção 2).

### Inimigos não aparecem em build standalone (ok no Editor)

**Causa:** referência de Prefab Variant com fileID "herdado" que não existe no YAML do variant. Funciona no Editor via AssetDatabase, falha em build.

**Fix:** re-arrastar o Prefab Variant no campo do ScriptableObject no Inspector.

**Referência:** `PADROES_NGO.md` — Padrão P6.

### Armadilha ou heal não funciona para o jogador cliente

**Causa:** `Rigidbody Kinematic` ausente no player — triggers não disparam para movimento via `transform.position`.

**Referência:** `PADROES_NGO.md` — Padrão P2.

### "NetworkPrefab not found" no Console

**Causa:** prefab de torre, armadilha ou inimigo não está registrado em `Assets/Multiplayer/Setup/DefaultNetworkPrefabs.asset`.

**Fix:** abrir o asset, clicar em + e arrastar o prefab.

### Console mostra "non-server writes to NetworkVariable"

**Causa:** um cliente está tentando escrever em uma NetworkVariable que só o servidor pode modificar.

**Fix:** verificar se o código tem guard `if (!IsServer) return;` antes da escrita.

---

## 9. Onde encontrar bugs conhecidos e pendências

- **Tabela de alertas ativos**: `Guia_Game_Designer.md` — Seção 5
- **Bugs de movimento host/cliente**: memória Claude `bug_host_client_movement.md`
- **Bugs do sistema de armadilhas**: memória Claude `bug_trap_system_multiplayer.md`
- **TODO do Game Designer no Editor**: `Estado_Atual_Multiplayer.md` — seção "TODO Game Designer no Editor"

---

## 10. Fluxo de trabalho seguro

Antes de commitar mudanças em código multiplayer:

1. Testar com MPPM (host + cliente) — não só singleplayer
2. Verificar o checklist em `PADROES_NGO.md` (final do arquivo)
3. Se modificar `PlayerNetworkSetup.cs` ou `PlayerMovement.cs`, confirmar que host E cliente se movem
4. Se modificar `BuildManager.cs` ou `NetworkedTrapVisual.cs`, confirmar que armadilhas aparecem e respeitam limite
5. Atualizar `Estado_Atual_Multiplayer.md` se mudar fluxo, cena, prefab de rede ou contrato EOS/NGO
