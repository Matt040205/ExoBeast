> **Archived working document.** Original filename: `message.txt`, captured on 2026-04-13. Snapshot of the bug analysis written during a multiplayer debugging session. Kept for historical context; not maintained, and superseded by the canonical state document at [Assets/Codigo/Docs/Estado_Atual_Multiplayer.md](../../../Assets/Codigo/Docs/Estado_Atual_Multiplayer.md).

# Correção de Bugs Multiplayer - ExoBeasts

## Contexto

Após testes multiplayer, vários bugs foram identificados envolvendo habilidades de personagens (Samurai/Raposa e Coruja), armadilhas, e sincronização de timer. A **causa raiz principal** é que o `CommanderAbilityController` e as classes de habilidade rodam lógica protegida por `IsOwner` que bloqueia o jogador 2 (não-owner do próprio script), e algumas habilidades chamam componentes que foram desabilitados no jogador remoto.

---

## Análise dos Bugs

### Bug 1: Animação de Curar (Samurai/Raposa) não roda - AMBOS os jogadores

**Causa raiz**: Em `PeaceOfMindLogic.RequestPeaceOfMindServerRpc()`, o `NetworkAnimator.SetTrigger("Heal")` é chamado no **servidor**. Porém, o `NetworkAnimator.SetTrigger()` do NGO **só propaga para clientes quando chamado pelo OWNER do NetworkAnimator** (o jogador local), não pelo servidor. Quando o servidor chama `SetTrigger`, ele dispara localmente no host mas **não replica para os clientes**.

**Fix**: Após o servidor disparar a lógica de heal, enviar um `ClientRpc` para disparar a animação em todos os clientes. O owner já terá a animação correta, e remotos também verão.

---

### Bug 2: Habilidades do Jogador 2 (Q, E, Ult) não funcionam - Samurai E Coruja

**Causa raiz**: `CommanderAbilityController.Update()` tem `if (!IsOwner) return;` na linha 87, o que bloqueia o input do jogador local. **Isso está correto** — apenas o owner pode enviar input.

O problema real está nas classes `Activate()` das habilidades:

- **CuttingBladeAbility.Activate()** (Q/Dash): Chama `quemUsou.GetComponent<CharacterController>()` — mas o `CharacterController` **foi desabilitado** pelo `PlayerMovement.OnNetworkSpawn()` para jogadores remotos. Porém, como `Activate()` roda no **servidor** (chamado pelo `RequestActivateAbilityServerRpc`), o servidor está tentando usar o CharacterController do jogador 2, que pode não estar disponível corretamente. **Adicionalmente**, `CuttingBladeLogic.StartDash()` tem `if (!IsOwner) return;` — mas quem chama é o **servidor** via `RequestActivateAbilityServerRpc`, então `IsOwner` será `false` no servidor para o jogador 2.

- **PeaceOfMindAbility.Activate()**: Mesma questão — `PeaceOfMindLogic.StartEffect()` tem `if (!IsOwner) return;` — no servidor, `IsOwner` é `false` para o jogador 2.

- **NineTailsDanceAbility.Activate()**: `NineTailsDanceLogic.StartEffect()` tem `if (!IsOwner) return;` — mesmo problema.

- **HabilidadeVooGracioso.Activate()** (Coruja Q): Chama `PlayerMovement` e `PlayerShooting` diretamente. Roda no servidor, mas `PlayerShooting` está desabilitado para jogadores remotos.

- **HabilidadePerseguindoPresas.Activate()** (Coruja E): Deve funcionar, pois busca `EnemyHealthSystem` globalmente. Mas pode falhar se `FindObjectsByType` não encontrar inimigos no servidor.

- **HabilidadeCacadoraNoturna.Activate()** (Coruja Ult): Chama `GetComponent<PlayerMovement>()` que deve existir. Parece OK.

> [!IMPORTANT]
> **O padrão correto para habilidades no NGO**: As `Activate()` rodam no **servidor** via `RequestActivateAbilityServerRpc`. Portanto, guards `if (!IsOwner) return;` dentro dos Logic scripts impedem a execução para o jogador 2 (que não é owner no servidor). Precisamos remover esses guards e substituir pela validação de servidor (`if (!IsServer) return;` onde necessário), e enviar de volta ao owner via `ClientRpc` quando precisar rodar lógica local.

---

### Bug 3: Projétil da Coruja vai na direção do olhar (errado no multiplayer)

**Causa raiz**: `PlayerShooting.GetShotDirection()` usa `Camera.main` para calcular a direção. No multiplayer, `Camera.main` aponta para a **câmera do jogador local**, não do jogador que atirou. Quando o jogador 2 atira, o servidor calcula `GetShotDirection()` usando a câmera local (host), resultando em direção errada.

**Mas na verdade**, olhando melhor, `PlayerShooting.Shoot()` chama `GetShotDirection()` e envia via `ShootServerRpc(direction)`. O `GetShotDirection()` roda no **lado do owner** (pois `Update()` tem `if (!IsOwner) return;`). Portanto, a direção deveria estar correta no owner. O problema pode ser que `ShootVisualClientRpc(direction)` envia a direção correta mas o `ExecuteShootVisual` no lado remoto ignora-a ou usa outro cálculo.

Na verdade, olhando mais de perto, `ExecuteShootVisual()` na linha 238-239 faz `firePoint.rotation = Quaternion.LookRotation(direction)`, e na linha 243 `projectilePool.GetProjectile(firePoint.position, Quaternion.LookRotation(direction))` — usa a `direction` passada, está OK.

**Mas se o problema é a Coruja (arco)**: O tiro do arco usa o mesmo sistema `PlayerShooting`. A questão é que `GetShotDirection()` usa `Camera.main.ViewportPointToRay(0.5, 0.5)` — isso é o centro da tela. Quando combinado com a coruja, ele funciona de forma idêntica. Se o projétil vai "na direção do olhar" para ambos jogadores, pode ser que o problema esteja no **visual do projétil no cliente remoto** — o `ExecuteShootVisual` está usando `networkAnimator.SetTrigger("Shoot")` que pode causar duplicação.

Na verdade, reanalisando: Na linha 234, `ExecuteShootVisual` chama `networkAnimator.SetTrigger("Shoot")` **inclusive no lado do ClientRpc** (para remotos). Isso pode causar duplicação de trigger. O `SetTrigger` via `NetworkAnimator` já propaga automaticamente. Vou corrigir para que o trigger seja chamado apenas pelo owner.

---

### Bug 4: Armadilhas invisíveis e sem colisão para o Jogador 2

**Causa raiz**: Quando `BuildManager.RequestPlaceTrapServerRpc()` spawna uma armadilha com `netObj.Spawn()`, o prefab é instanciado no servidor e enviado ao cliente. MAS os prefabs de armadilha têm `TrapLogicBase` (que herda de `NetworkBehaviour`), e precisam de `NetworkObject`. 

O problema provável é que os prefabs de armadilha **não estão registrados na NetworkPrefabsList** do cliente, ou que o componente visual está em um `logicPrefab` separado que não é spawnado.

Olhando o `TrapDataSO`, tem dois campos: `prefab` (visual) e `logicPrefab` (lógica). O `BuildManager` usa `trapData.prefab` para instanciar, mas o `logicPrefab` nunca é instanciado. Se o prefab visual não tem `NetworkObject`, ele não é sincronizado pelo netcode.

**Adicionalmente**, no `HandleBuildGhost()`, `foreach (var col in currentBuildGhost.GetComponentsInChildren<Collider>()) col.enabled = false;` desabilita colisão no ghost. Quando o ServerRpc spawna um NOVO prefab, ele deveria ter colisão habilitada.

**Problema real**: O `BuildManager` é um singleton que roda apenas no **jogador que está no modo build**. O jogador 2 pode não ter o `BuildManager` configurado, ou o ghost local pode estar interferindo. A armadilha spawnada via `netObj.Spawn()` deveria aparecer para todos os clientes — se não aparece, o prefab pode não estar na NetworkPrefabsList.

> [!IMPORTANT]
> Preciso que você confirme: os prefabs de armadilha estão na **Default Network Prefabs list** do Unity? Se não estiverem, o Netcode não consegue instanciá-los no cliente. Esse é o problema mais provável para as armadilhas não aparecerem no jogador 2.

---

### Bug 5: Armadilhas não executam função

**Causa raiz**: O `TrapLogicBase` é abstrato e não tem nenhuma implementação de trigger (OnTriggerEnter, etc.). As classes derivadas deveriam ter essa implementação, mas não encontrei nenhuma no projeto. Só existem `TrapLogicBase.cs` e `TrapDataSO.cs` na pasta de Armadilhas.

> [!WARNING]
> Não encontrei nenhuma implementação concreta de armadilha (ex: `SlowTrap`, `DamageTrap`). Sem esses scripts, as armadilhas nunca executarão sua função.

---

### Bug 6: Timer atrasado entre telas

**Causa raiz encontrada**: O `MatchManager` já possui uma `NetworkVariable<float> MatchTime` que é incrementada pelo servidor no `Update()`. O `UIManager` lê esse valor — **mas tem um fallback problemático**:

```csharp
// UIManager.cs:56-63
void Update()
{
    if (ExoBeasts.Multiplayer.GameServer.MatchManager.Instance != null)
        gameTime = MatchManager.Instance.MatchTime.Value;  // ✅ lê da rede
    else
        gameTime += Time.deltaTime;  // ❌ FALLBACK LOCAL - começa do zero!
    UpdateTimerDisplay(gameTime);
}
```

O problema: Quando o jogador 2 entra na cena, o `MatchManager` pode ainda não ter spawnado em rede (ou a referência `Instance` é `null` por um ou mais frames). Nesse período, o `gameTime` acumula `Time.deltaTime` local, ficando com um **offset permanente** em relação ao servidor.

Além disso, a `NetworkVariable<float> MatchTime` já transmite o valor correto automaticamente para novos clientes via o mecanismo de sincronização inicial do NGO. Quando o jogador 2 conecta, o `MatchTime.Value` já reflete o tempo atual do host — **o UIManager só precisa sempre ler dessa variável.**

**Fix proposto (2 partes)**:

1. **`UIManager`**: Eliminar o fallback local `gameTime += Time.deltaTime`. Quando `MatchManager.Instance` é null, simplesmente não atualizar o timer (mantém o último valor mostrado ou 00:00). Assim que o MatchManager spawna, pega o valor correto instantaneamente.

2. **`MatchManager.OnNetworkSpawn()`**: Já existe e funciona corretamente — quando o cliente 2 entra, o `OnNetworkSpawn()` dispara, e nesse momento `MatchTime.Value` **já contém o valor do servidor** (rede NetworkVariable sincroniza automaticamente). Adicionar uma chamada no `OnNetworkSpawn()` para forçar a UI a atualizar imediatamente com o valor correto.

---

## Proposta de Mudanças

### Habilidades - Samurai/Raposa

#### [MODIFY] [PeaceOfMindLogic.cs](file:///c:/Users/mateu/OneDrive/Documentos/GitHub/ExoBeasts/PI3D/Assets/Codigo/Characters/Raposa/PeaceOfMindLogic.cs)
- Remover `if (!IsOwner) return;` do `StartEffect()` — essa função é chamada pelo servidor via `Activate()` dentro de `RequestActivateAbilityServerRpc`.
- Adicionar `PlayHealAnimationClientRpc()` separado que dispara `NetworkAnimator.SetTrigger("Heal")` no **owner** (pois o NetworkAnimator propaga do owner para todos).

#### [MODIFY] [CuttingBladeLogic.cs](file:///c:/Users/mateu/OneDrive/Documentos/GitHub/ExoBeasts/PI3D/Assets/Codigo/Characters/Raposa/CuttingBladeLogic.cs)
- Remover `if (!IsOwner) return;` do `StartDash()` — é chamado pelo servidor.
- Criar `RequestDashClientRpc()` que envia os dados do dash para o **owner** executar a movimentação localmente (CharacterController é local).
- Manter `PerformDashDamageServerRpc()` no servidor.

#### [MODIFY] [NineTailsDanceLogic.cs](file:///c:/Users/mateu/OneDrive/Documentos/GitHub/ExoBeasts/PI3D/Assets/Codigo/Characters/Raposa/NineTailsDanceLogic.cs)
- Remover `if (!IsOwner) return;` do `StartEffect()`.
- Chamar `SetUltimateStateServerRpc(true)` diretamente (já roda no servidor).
- Ajustar o timer para rodar no servidor em vez de no owner.

---

### Habilidades - Coruja

#### [MODIFY] [HabilidadeVooGracioso.cs](file:///c:/Users/mateu/OneDrive/Documentos/GitHub/ExoBeasts/PI3D/Assets/Codigo/Characters/Coruja/HabilidadeVooGracioso.cs)
- `Activate()` roda no servidor — precisa enviar ClientRpc ao owner para aplicar modificadores de movimento locais.

#### [MODIFY] [HabilidadePerseguindoPresas.cs](file:///c:/Users/mateu/OneDrive/Documentos/GitHub/ExoBeasts/PI3D/Assets/Codigo/Characters/Coruja/HabilidadePerseguindoPresas.cs)
- Já roda no servidor, onde `FindObjectsByType<EnemyHealthSystem>` deve funcionar. Precisa só garantir que o visual de marca seja enviado aos clientes.

#### [MODIFY] [HabilidadeCacadoraNoturna.cs](file:///c:/Users/mateu/OneDrive/Documentos/GitHub/ExoBeasts/PI3D/Assets/Codigo/Characters/Coruja/HabilidadeCacadoraNoturna.cs)
- `Activate()` roda no servidor — `DisparoDelayCoroutine` instancia e spawna o VFX corretamente.
- Garantir que a animação `CacadoraUltimate` seja disparada corretamente.

#### [MODIFY] [CacadoraNoturnaLogic.cs](file:///c:/Users/mateu/OneDrive/Documentos/GitHub/ExoBeasts/PI3D/Assets/Codigo/Characters/Coruja/CacadoraNoturnaLogic.cs)
- Verificar se a animação é disparada corretamente via `NetworkAnimator`.

---

### Tiro / Projétil da Coruja

#### [MODIFY] [PlayerShooting.cs](file:///c:/Users/mateu/OneDrive/Documentos/GitHub/ExoBeasts/PI3D/Assets/Codigo/Characters/Player/PlayerShooting.cs)
- Remover a chamada duplicada de `networkAnimator.SetTrigger("Shoot")` no `ShootVisualClientRpc` (remotos). O trigger já é propagado pelo `NetworkAnimator` quando o owner chama.

---

### Armadilhas (Visibilidade e Colisão)

#### [MODIFY] [BuildManager.cs](file:///c:/Users/mateu/OneDrive/Documentos/GitHub/ExoBeasts/PI3D/Assets/Codigo/Towers/BuildManager.cs)
- Garantir que os prefabs das armadilhas sejam adicionados à `NetworkPrefabsList` no `OnNetworkSpawn`.
- Adicionar logs para debugging.

> [!IMPORTANT]
> **Ação necessária do usuário**: Verificar no Unity Editor se os prefabs de armadilha estão na **DefaultNetworkPrefabs** list (Assets/DefaultNetworkPrefabs.asset). Se não estiverem, adicioná-los lá. Isso não pode ser feito via código.

---

### Timer Sincronizado

#### [MODIFY] [UIManager.cs](file:///c:/Users/mateu/OneDrive/Documentos/GitHub/ExoBeasts/PI3D/Assets/Codigo/UI/UIManager.cs)
- Remover a variável local `gameTime` e o fallback `gameTime += Time.deltaTime`.
- Sempre ler diretamente de `MatchManager.Instance.MatchTime.Value` quando disponível.
- Quando `MatchManager.Instance` é null, mostrar `00:00` (estado inicial).

#### [MODIFY] [MatchManager.cs](file:///c:/Users/mateu/OneDrive/Documentos/GitHub/ExoBeasts/PI3D/Assets/Codigo/Multiplayer/GameServer/MatchManager.cs)
- No `OnNetworkSpawn()`, adicionar bloco para **todos os clientes** (não apenas servidor):
  - Forçar imediatamente a leitura de `MatchTime.Value` e atualizar o UIManager.
  - Registrar callback `MatchTime.OnValueChanged` para manter a UI sempre em sincronia.
- Isso garante que o jogador 2 ao entrar pegue o tempo exato do servidor sem delay.

---

### Sincronização de Animação

#### [MODIFY] [CommanderAbilityController.cs](file:///c:/Users/mateu/OneDrive/Documentos/GitHub/ExoBeasts/PI3D/Assets/Codigo/Characters/AbilitySystem/CommanderAbilityController.cs)
- No `ActivateAbilityVisualClientRpc`, disparar visuals/animações para todos os clientes.
- Garantir que cooldowns sejam sincronizados corretamente para clientes remotos.

---

## Open Questions

> [!IMPORTANT]
> 1. **Prefabs de Armadilha na NetworkPrefabsList**: Os prefabs de armadilha estão na DefaultNetworkPrefabs.asset? Se não, precisam ser adicionados manualmente no editor do Unity.
> 2. **Implementação concreta de Armadilhas**: Não encontrei scripts como `DamageTrap`, `SlowTrap` etc. Existem classes derivadas de `TrapLogicBase` em algum outro lugar? Sem elas, as armadilhas nunca terão efeito.
> 3. **O "Samurai" é o personagem "Raposa"?** Confirmando que CuttingBlade(Q/dash), PeaceOfMind(E/heal) e NineTailsDance(X/ult) são as habilidades do Samurai que você mencionou.

## Verification Plan

### Automated Tests
- Compilar o projeto com `Unity Build` para garantir que não há erros de compilação.

### Manual Verification
- Testar multiplayer com 2 jogadores:
  1. Jogador 1 de Samurai: Q (dash), E (curar com animação), X (ultimate)
  2. Jogador 2 de Samurai: mesmas habilidades
  3. Verificar animação de cura visível em ambas as telas
  4. Jogador 1 de Coruja: Q (pulo no ar), E (marca), X (ultimate com animação)
  5. Jogador 2 de Coruja: mesmas habilidades
  6. Colocar armadilhas e verificar visibilidade em ambas as telas
  7. Verificar timer sincronizado
