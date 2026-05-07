# Sprint 3 — Item A3: LobbyManager.SearchLobbies Rate-Limit

> **Tempo estimado**: ~1 hora. **Risco**: 🟢 Baixo. **Pré-requisitos**: A2 mergeado.
> **Pré-leitura**: `01_padroes.md` + esta nota especial.

## ⚠️ Nota especial — `LobbyManager.cs` está na lista de frágeis

`LobbyManager.cs` no GERAL está na lista de arquivos frágeis (ver `01_padroes.md`)
por causa do bug `StartHost falha com IsClient=True` corrigido em Abril 2026.
**MAS** este item (A3) modifica APENAS o método `SearchLobbies`, que **não está
no caminho crítico** desse bug. As funções delicadas são `StartMatchCoroutine` e
`OnLobbyAttributeUpdated` — não tocar nelas neste item.

A3 é considerado seguro porque:
- `SearchLobbies` é fluxo independente (UI → busca → resultados).
- Não tem efeito colateral em `_currentLobby`, `_isInLobby`, ou conexão NGO.
- O orquestrador já aprovou modificação deste método específico.

## Contexto

`LobbyManager.SearchLobbies(LobbySearchFilter filter)` em
`Assets/Codigo/Multiplayer/Lobby/LobbyManager.cs` (~linha 264) cria um novo
handle `LobbySearch` e chama `Find()` em cada invocação. Não há cooldown nem
cache.

**Por quê isso importa**:
- EOS Lobby Service tem rate limits por usuário (~30 calls/min documentado);
  exceder dispara erro `Result.RateLimited` ou throttling no backend.
- UI atual (`LobbySceneUI.cs`) tem botão "Buscar" — usuário impaciente clica 5x
  em 2s = 5 requests EOS simultâneos.
- Mesmo sem hit no rate limit, é desperdício de banda + CPU pra processar
  callbacks redundantes.

## Objetivo

1. **Cooldown** de 2 segundos entre buscas. Tentativas durante o cooldown
   retornam o último resultado em cache (se existir) sem disparar nova request.
2. **Cache** do último resultado (`List<LobbyInfo>`) válido por 2 segundos.
3. **Não quebrar** UI nem fluxo atual: o evento `OnLobbiesFound` deve continuar
   sendo disparado normalmente em ambos os caminhos (cache e network).

## Investigação prévia (obrigatória)

### 1. Ler arquivo completo (cuidado: arquivo grande, ~1700 linhas — usar offset/limit)

```
Read: Assets/Codigo/Multiplayer/Lobby/LobbyManager.cs (offset 264, limit 200)
```

Procurar por:
- `public void SearchLobbies(LobbySearchFilter filter)` — método alvo
- `OnLobbiesFound?.Invoke(lobbies);` — onde os resultados são publicados (~linha 360)
- Qualquer uso de `_lobbiesCache` ou similar (não deve existir; vamos criar)

### 2. Ler o consumer principal

```
Grep: pattern="SearchLobbies\\(" path="Assets/Codigo"
```

Esperar encontrar:
- `LobbySceneUI.cs` (UI canvas de produção)
- `LobbyUIManager.cs` (UI debug, mas existe)
- Possivelmente `MenuLobbyPanel.cs` ou `LobbyPlaceholderUI.cs`

Confirmar que NENHUM consumer chama `SearchLobbies` em loop apertado por segundo
(seria caso degenerado que o cooldown poderia mascarar). Se algum chama em loop,
abortar o item e reportar — pode haver bug de design pré-existente.

## Plano de mudança

### Mudanças em `LobbyManager.cs`

#### Adicionar campos privados (próximo aos outros campos privados, ~linha 80)

```csharp
// OPTIMIZATION (Sprint 3 / Item A3 - 2026-05-XX): rate-limit + cache de SearchLobbies.
// EOS Lobby Service tem rate limit por usuario (~30 calls/min). Sem cooldown, usuario
// clicando "Buscar" repetidamente disparava varias requests simultaneas — desperdicio
// de banda + risco de Result.RateLimited.
private const float SEARCH_LOBBIES_COOLDOWN_SECONDS = 2f;
private float _lastSearchLobbiesTime = -10f;
private List<LobbyInfo> _lastSearchLobbiesResult; // null antes da primeira busca
```

#### Modificar método `SearchLobbies(filter)` (linha ~264)

**Estado atual (CONFIRMAR antes de editar)**:
```csharp
public void SearchLobbies(LobbySearchFilter filter)
{
#if !EOS_DISABLE
    var lobbyInterface = GetLobbyInterface();
    if (lobbyInterface == null) { OnError?.Invoke("EOS nao inicializado"); return; }

    var localUserId = GetLocalUserId();
    if (localUserId == null || !localUserId.IsValid())
    {
        OnError?.Invoke("Usuario nao autenticado. Faca login antes de buscar lobbies.");
        return;
    }

    // ... resto do método: cria search handle, chama Find(), publica via OnLobbiesFound
}
```

**Adicionar GUARD no início do método**, ANTES da chamada `GetLobbyInterface()`:
```csharp
public void SearchLobbies(LobbySearchFilter filter)
{
#if !EOS_DISABLE
    // OPTIMIZATION (Sprint 3 / Item A3): rate-limit + cache.
    // Se ja temos resultado recente, republica do cache em vez de disparar nova request.
    float elapsed = Time.unscaledTime - _lastSearchLobbiesTime;
    if (elapsed < SEARCH_LOBBIES_COOLDOWN_SECONDS && _lastSearchLobbiesResult != null)
    {
        Debug.Log($"[LobbyManager] SearchLobbies em cooldown ({elapsed:F1}s/{SEARCH_LOBBIES_COOLDOWN_SECONDS}s). " +
                  $"Republicando cache com {_lastSearchLobbiesResult.Count} lobbies.");
        OnLobbiesFound?.Invoke(_lastSearchLobbiesResult);
        return;
    }

    var lobbyInterface = GetLobbyInterface();
    // ... resto do método continua igual
```

#### Modificar o callback de Find (onde `OnLobbiesFound?.Invoke(lobbies)` é chamado, ~linha 360)

Encontrar a linha onde os lobbies são publicados (algo como `OnLobbiesFound?.Invoke(lobbies);`)
e ANTES de invokar, salvar no cache:
```csharp
_lastSearchLobbiesResult = lobbies;
_lastSearchLobbiesTime = Time.unscaledTime;
OnLobbiesFound?.Invoke(lobbies);
```

**Importante**: salvar a REFERÊNCIA, não copiar. `OnLobbiesFound` recebe a mesma `List<LobbyInfo>`
que `_lastSearchLobbiesResult` aponta. Em uso prático isso é OK — UI lê e renderiza, depois
descarta. Mas DOCUMENTAR no comentário que mutação externa do resultado afeta o cache.

#### Tratar caso de erro (Find falha)

Se a Find retornar erro (Result != Success), o cache NÃO deve ser invalidado nem atualizado.
Usuário pode tentar de novo, e se < 2s, recebe o último cache válido. Verificar o callback
existente para garantir que o `OnLobbiesFound` só é chamado em sucesso.

Se o callback chama `OnError` em falha SEM chamar `OnLobbiesFound`, está OK — não precisa
tocar.

### Adicionar método público para invalidar cache (boa prática)

```csharp
/// <summary>
/// Invalida o cache de SearchLobbies — proxima chamada SearchLobbies dispara nova request
/// mesmo dentro do cooldown. Util quando um lobby acabou de ser criado/destruido.
/// </summary>
public void InvalidateLobbySearchCache()
{
    _lastSearchLobbiesResult = null;
    _lastSearchLobbiesTime = -10f;
}
```

Deixa exposto para que UI possa chamar após criar lobby (forçar refresh imediato).
**Não** wirar essa chamada nesta PR — outros itens podem precisar dela. Apenas expor.

## Validação

### 1. Build limpo
```powershell
dotnet build PI3D.sln
```
Esperar `0 Erro(s)`.

### 2. Validação funcional manual (Unity Editor + LobbyScene)

1. Abrir Editor → cena `Assets/Scenes/LobbyScene.unity`
2. Login EOS (auto)
3. Click "Buscar lobbies" (BtnEntrarCliente → busca)
4. Observar log: deve disparar request EOS normalmente
5. **Click "Buscar" 3 vezes rapidamente**
6. Observar log: 1ª chamada dispara request, próximas 2 mostram
   `SearchLobbies em cooldown (...). Republicando cache com X lobbies.`
7. Esperar 3 segundos
8. Click "Buscar" novamente: deve disparar nova request (cooldown expirou)

### 3. Validação edge cases

- Buscar quando ainda não houve nenhuma busca: cache `null`, dispara request normalmente.
- Buscar enquanto request em andamento (tempo entre `Find()` chamado e callback voltar):
  comportamento atual NÃO é coberto por este item (deduplicação concorrente). Aceitar
  que duas requests podem voar se cliques forem dentro de < 200ms da primeira chamada
  (antes de `_lastSearchLobbiesTime` atualizar). Isso é acceptable por enquanto.

## Critérios de aceitação

- [ ] Build limpo (0 erros)
- [ ] Cliques rápidos de "Buscar" no UI: apenas 1 request EOS por janela de 2s
- [ ] Cache funciona (cliques dentro do cooldown retornam último resultado)
- [ ] `InvalidateLobbySearchCache()` exposto publicamente
- [ ] `OnLobbiesFound` continua disparando normalmente em ambos os caminhos
      (network e cache)
- [ ] Comentário explicativo presente

## Riscos e mitigação

| Risco | Probabilidade | Mitigação |
|---|---|---|
| Usuário aguarda 2s sentindo "lag" para refresh manual | Baixa | UI feedback (futuro) — neste item, aceitar |
| Lobby criado no host não aparece no client por 2s | Possível | `InvalidateLobbySearchCache()` permite UI invalidar manualmente após eventos relevantes |
| Mutação externa da `List<LobbyInfo>` afeta cache | Baixa | Comentar no código; UI não muta hoje |

## Rollback

```powershell
git checkout Assets/Codigo/Multiplayer/Lobby/LobbyManager.cs
```

## Reportar ao orquestrador (template)

```
Item: A3
Status: completed
Arquivos modificados: Assets/Codigo/Multiplayer/Lobby/LobbyManager.cs
Build: PASS (0 erros, 52 warnings)
Validação in-game: PASS (3 cliques rápidos = 1 request EOS) | NOT_RUN
Métrica medida: SearchLobbies request rate em spam click — antes: 5/2s, depois: 1/2s
Riscos detectados: nenhum
Próximo item liberado para execução: true (E5)
```
