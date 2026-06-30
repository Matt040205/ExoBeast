# Sprint 4 — Item A6: LobbyManager.SetMemberAttribute Debounce

> **Tempo estimado**: ~30-45 minutos. **Risco**: 🟡 Médio. **Pré-requisitos**: A3 (Sprint 3) mergeado.
> **Pré-leitura**: `01_padroes.md` + esta nota especial.

## ⚠️ Nota especial — `LobbyManager.cs` está na lista de frágeis

Mesmo aviso do A3 (Sprint 3): `LobbyManager.cs` tem bug histórico `StartHost falha com IsClient=True` (Abril 2026). **MAS** este item modifica APENAS o método `SetMemberAttribute` e adiciona infra-estrutura de debounce — **não toca**:
- `StartMatchCoroutine`
- `OnLobbyAttributeUpdated`
- `_currentLobby`
- Conexão NGO

A6 é seguro porque modifica fluxo independente (UI → SetReady/SelectCharacter → debounce → SetMemberAttribute → EOS). Não tem efeito colateral em estado de lobby compartilhado.

## Contexto

`LobbyManager.SetMemberAttribute(string key, string value)` (~linha 633) é chamado por:
- `SetReady(bool ready)` em linha 711 → chamado pelo botão Ready do UI
- `SelectCharacter(int characterIndex)` em linha 733 → chamado quando UI escolhe personagem

Cada chamada dispara um `UpdateLobbyMember` (network call EOS). Em UI hesitante:
- Jogador clica Raposa → SetMemberAttribute(CHARACTER_INDEX, "0") → EOS call
- 200ms depois: clica Coruja → SetMemberAttribute(CHARACTER_INDEX, "1") → EOS call
- 200ms depois: clica Dragão → SetMemberAttribute(CHARACTER_INDEX, "2") → EOS call
- 200ms depois: volta pra Raposa → SetMemberAttribute(CHARACTER_INDEX, "0") → EOS call

= **4 EOS calls** quando deveria ser **1 (a final)**.

**Por quê isso importa**:
- EOS Lobby Service tem rate limits por usuário (~30 calls/min documentado).
- Hesitação de jogador é caso normal — não é abuso.
- Ainda mais: cada `UpdateLobbyMember` gera callbacks `OnMemberUpdate` em **todos os outros clientes** do lobby = ruído distribuído.
- Cada call falha se EOS estiver lento ou em throttling — risco de Ready/character "preso" no estado anterior.

## Objetivo

Implementar **debounce de 250ms** em `SetMemberAttribute`:
- Múltiplas chamadas em rápida sucessão para a mesma `key` colapsam em UMA única call EOS com o **último valor**.
- O timer reseta a cada nova chamada (debounce padrão, não throttle).
- Manter API pública intacta — UI continua chamando `SetMemberAttribute(key, value)` normalmente.

## Investigação prévia (obrigatória)

### 1. Ler arquivo (cuidado: ~1700 linhas — usar offset/limit)

```
Read: Assets/Codigo/Multiplayer/Lobby/LobbyManager.cs (offset 625, limit 100)
```

Localizar:
- `public void SetMemberAttribute(string key, string value)` (~linha 633)
- Estrutura interna: chama EOS `UpdateLobbyMember` síncrono ou agendado?

### 2. Mapear consumers

```
Grep: pattern="SetMemberAttribute\(" path="Assets/Codigo"
```

Esperar encontrar:
- `LobbyManager.cs` (definição + chamadas internas em CreateLobby/JoinLobby — linhas 205, 487)
- `LobbyManager.SetReady` (linha 711)
- `LobbyManager.SelectCharacter` (linha 733)
- `LobbySceneUI.cs` (UI canvas de produção)
- Possivelmente `MenuLobbyPanel.cs`, `LobbyUIManager.cs`

**Importante**: as chamadas em CreateLobby/JoinLobby (linhas 205, 487) são **inicialização** — definem `DISPLAY_NAME` ao entrar. Essas NÃO devem ser debounced (são one-shot). Ver plano abaixo para isolar essas chamadas.

### 3. Confirmar que SetMemberAttribute é seguro chamar de coroutine

```
Read: Assets/Codigo/Multiplayer/Lobby/LobbyManager.cs (offset 633, limit 60)
```

Verificar:
- Não captura estado em variável local que precise estar sincronizado (ex: `_currentLobby` deve ser thread-safe se acessado por main thread)
- EOS handle é obtido na hora da chamada (não cacheado)
- Coroutine pode rodar entre frames sem corromper

## Plano de mudança

### Estratégia: separar API pública (com debounce) de API interna (direto)

Renomear o método existente como `SetMemberAttributeImmediate` (privado/interno) e criar nova `SetMemberAttribute` pública que apenas agenda debounce.

### Mudanças em `LobbyManager.cs`

#### 1. Adicionar campos privados (próximo aos outros campos privados, ~linha 70-80)

```csharp
// OPTIMIZATION (Sprint 4 / Item A6 - 2026-MM-DD): debounce de SetMemberAttribute.
// EOS Lobby Service tem rate limit (~30 calls/min). UI hesitante (jogador trocando
// personagem rapidamente) disparava varios UpdateLobbyMember consecutivos.
// Antes: cada SetMemberAttribute -> chamada EOS imediata.
// Agora: chamadas para mesma key dentro de 250ms colapsam em uma unica chamada com ultimo valor.
// Sem isso: ate 5 EOS calls por hesitacao tipica, risco de Result.RateLimited e
// callbacks redundantes em outros clientes.
private const float SET_MEMBER_ATTRIBUTE_DEBOUNCE_SECONDS = 0.25f;

// Pending values por key. Coroutine ativa por key tambem.
private readonly Dictionary<string, string> _pendingMemberAttributes = new Dictionary<string, string>();
private readonly Dictionary<string, Coroutine> _memberAttributeDebounceCoroutines = new Dictionary<string, Coroutine>();
```

**Por que `Dictionary<string, Coroutine>` (uma por key)**:
- Trocar Ready (key=`IS_READY`) NÃO deve resetar o timer de troca de personagem (key=`CHARACTER_INDEX`)
- Cada key tem seu próprio debounce independente.

#### 2. Renomear método existente para `SetMemberAttributeImmediate` (mantendo internal/private)

**Antes** (linha 633):
```csharp
public void SetMemberAttribute(string key, string value)
{
#if !EOS_DISABLE
    // ... codigo de EOS ...
}
```

**Depois**:
```csharp
// Variante imediata. Usada internamente pela coroutine de debounce + chamadas
// de inicializacao (CreateLobby, JoinLobby) que NAO devem ser debounced.
private void SetMemberAttributeImmediate(string key, string value)
{
#if !EOS_DISABLE
    // ... mesmo codigo de antes, sem alteracao ...
}
```

#### 3. Criar nova `SetMemberAttribute` pública com debounce

```csharp
/// <summary>
/// API publica - debounce de 250ms. Chamadas rapidas para a mesma key colapsam em uma.
/// Para chamada imediata (inicializacao), use SetMemberAttributeImmediate (privado).
/// </summary>
public void SetMemberAttribute(string key, string value)
{
    // OPTIMIZATION (Sprint 4 / Item A6): ver comentario nos campos privados.
    _pendingMemberAttributes[key] = value;

    if (_memberAttributeDebounceCoroutines.TryGetValue(key, out var existing) && existing != null)
        StopCoroutine(existing);

    _memberAttributeDebounceCoroutines[key] = StartCoroutine(DebouncedSubmitMemberAttribute(key));
}

private System.Collections.IEnumerator DebouncedSubmitMemberAttribute(string key)
{
    yield return new WaitForSeconds(SET_MEMBER_ATTRIBUTE_DEBOUNCE_SECONDS);

    if (_pendingMemberAttributes.TryGetValue(key, out string pendingValue))
    {
        _pendingMemberAttributes.Remove(key);
        SetMemberAttributeImmediate(key, pendingValue);
    }

    _memberAttributeDebounceCoroutines.Remove(key);
}
```

#### 4. Atualizar chamadas internas de inicializacao para usar a versao imediata

**Linha ~205** (em CreateLobby success callback):
```csharp
// Antes:
SetMemberAttribute(MemberAttributes.DISPLAY_NAME, ...);
// Depois:
SetMemberAttributeImmediate(MemberAttributes.DISPLAY_NAME, ...);
```

**Linha ~487** (em JoinLobby success callback):
```csharp
// Antes:
SetMemberAttribute(MemberAttributes.DISPLAY_NAME, ...);
// Depois:
SetMemberAttributeImmediate(MemberAttributes.DISPLAY_NAME, ...);
```

**Linha ~711** (em SetReady) — manter `SetMemberAttribute` (a debounced — é o caso de uso principal)
**Linha ~733** (em SelectCharacter) — manter `SetMemberAttribute` (a debounced — é o caso de uso principal)

#### 5. Limpar coroutines em OnDestroy / Leave

Localizar `OnDestroy` ou método de cleanup do LobbyManager (~linha 90-110 ou final do arquivo):
```csharp
private void OnDestroy()
{
    // ... codigo existente ...

    // OPTIMIZATION (Sprint 4 / Item A6): cancelar coroutines de debounce pendentes.
    foreach (var kvp in _memberAttributeDebounceCoroutines)
    {
        if (kvp.Value != null) StopCoroutine(kvp.Value);
    }
    _memberAttributeDebounceCoroutines.Clear();
    _pendingMemberAttributes.Clear();
}
```

Se houver método `LeaveLobby` que faz cleanup local, repetir a limpeza lá também (cancela debounces pendentes ao sair do lobby).

```
Grep: pattern="LeaveLobby\|OnDestroy" path="Assets/Codigo/Multiplayer/Lobby/LobbyManager.cs"
```

## Edge cases a considerar

1. **Jogador clica Ready → debounce 250ms → host inicia partida em 100ms**:
   - Coroutine ainda não terminou
   - Quando coroutine terminar, vai chamar SetMemberAttributeImmediate em lobby que pode estar em transição
   - **Mitigação**: SetMemberAttributeImmediate já tem guards para `_currentLobby == null`

2. **Jogador troca personagem 5x em 1 segundo**:
   - Cada call cancela coroutine anterior + agenda nova
   - Após 250ms da última, EOS call é feita com último valor
   - **OK**: comportamento desejado.

3. **Two diferentes keys simultaneamente** (Ready=true + character=2):
   - Cada key tem coroutine separada
   - Ambas resolvem ~250ms depois, ordem não-determinística
   - **OK**: EOS aceita updates em qualquer ordem para keys diferentes.

4. **Cliente "spamming" SetReady toggle**:
   - 5 toggles em 1s = 1 EOS call no final
   - Estado final é o último valor — comportamento esperado.

## Validação

### 1. Build limpo
```powershell
dotnet build PI3D.sln
```

### 2. Validação funcional manual

**Cenário A — debounce de troca de personagem**:
1. Editor + 1 cliente MPPM
2. Host cria lobby, cliente entra
3. Cliente clica rapidamente: Raposa → Coruja → Dragão → Polvo (4 cliques em <1s)
4. Esperar 500ms
5. **Verificar**: somente UMA EOS call disparada (procurar log `[LobbyManager] SetMemberAttributeImmediate`)
6. **Verificar**: outro cliente vê apenas o personagem final (Polvo), não a sequência intermediária

**Cenário B — debounce de Ready**:
1. Cliente clica Ready 5x em 500ms
2. Esperar 500ms
3. Verificar Ready final corresponde ao último click (não-Ready se par, Ready se ímpar)

**Cenário C — chamadas independentes**:
1. Cliente clica Ready ON
2. **Imediatamente** (antes de 250ms) clica Personagem Coruja
3. Esperar 500ms
4. Verificar **ambos** ficam aplicados (Ready=true + char=Coruja)

**Cenário D — sair do lobby durante debounce**:
1. Cliente clica Personagem Coruja
2. **Imediatamente** clica "Sair do Lobby"
3. Verificar não há crash nem warning sobre coroutine
4. Verificar coroutine foi cancelada (logs limpos após Leave)

### 3. Logs esperados

Adicionar `Debug.Log` em `SetMemberAttribute` (debounced) e `SetMemberAttributeImmediate`:

```csharp
public void SetMemberAttribute(string key, string value)
{
#if UNITY_EDITOR
    Debug.Log($"[LobbyManager] SetMemberAttribute (debounced) key={key} value={value}");
#endif
    // ...
}
```

```csharp
private void SetMemberAttributeImmediate(string key, string value)
{
#if UNITY_EDITOR
    Debug.Log($"[LobbyManager] SetMemberAttributeImmediate -> EOS call: key={key} value={value}");
#endif
    // ...
}
```

Em UI spam, esperar:
- Vários `SetMemberAttribute (debounced)` (1 por click)
- Apenas 1 `SetMemberAttributeImmediate -> EOS call` por janela de 250ms

## Critérios de aceitação

- [ ] Build limpo (0 erros)
- [ ] Cenários A, B, C, D do passo 2 passam
- [ ] Cliente hesitante (4 trocas rápidas) gera 1 EOS call em vez de 4
- [ ] OnDestroy / LeaveLobby cancelam coroutines pendentes (sem warnings)
- [ ] API pública `SetMemberAttribute(key, value)` mantém assinatura — UI não precisa mudar
- [ ] Inicializações em CreateLobby/JoinLobby usam `SetMemberAttributeImmediate`
- [ ] Comentários OPTIMIZATION presentes

## Riscos e mitigação

| Risco | Probabilidade | Mitigação |
|---|---|---|
| Coroutine roda após `_currentLobby == null` (sair durante debounce) | Possível | Guards em SetMemberAttributeImmediate + cancel em LeaveLobby |
| Nova SetMemberAttribute pública chamada antes de NetworkSpawn | Baixa | StartCoroutine em MonoBehaviour requer GameObject ativo — deve estar ok |
| UI esperava call síncrona e checa estado após | Baixa | UI já reage assincronamente a `_members` updates do EOS |
| Edge case: host inicia partida durante debounce ativo | Possível | Canceled em OnDestroy quando cena muda; SetMemberAttributeImmediate é resiliente |

## Rollback

```powershell
git checkout Assets/Codigo/Multiplayer/Lobby/LobbyManager.cs
```

## Reportar ao orquestrador (template)

```
Item: A6
Status: completed
Arquivos modificados: Assets/Codigo/Multiplayer/Lobby/LobbyManager.cs
Build: PASS (0 erros, 52 warnings)
Validacao in-game: PASS (cenarios A-D) | NOT_RUN
Metrica medida: EOS calls em UI hesitante (4 trocas rapidas) — antes: 4, depois: 1
Riscos detectados: nenhum
Proximo item liberado: true (E7 e V1 - paralelos)
Notas: chamadas de inicializacao em CreateLobby (linha 205) e JoinLobby (linha 487) migradas para SetMemberAttributeImmediate. SetReady e SelectCharacter mantidas em SetMemberAttribute (debounced). Cleanup de coroutines validado em OnDestroy + LeaveLobby.
```

## Notas finais

A6 é o único item da Sprint 4 que toca um arquivo na lista de frágeis. **NÃO** mexer em fluxo de start de partida. Se o build falhar com erro relacionado a `LobbyManager`, **abortar e reportar** — não tentar fix especulativo.

Padrão "debounce com Dictionary<key, Coroutine>" é reutilizável para outros pontos do projeto que tenham UI hesitante. Anotar para Sprint 5 se aparecer.
