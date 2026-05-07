# Sprint 3 — Item A2: EOSManagerWrapper Double-Tick

> **Tempo estimado**: ~30 minutos. **Risco**: 🟢 Baixo. **Pré-requisitos**: nenhum.
> **Pré-leitura**: `01_padroes.md` (este diretório).

## Contexto

`EOSManagerWrapper.Update()` chama `GetPlatformInterface()?.Tick()` em todo frame
"como fallback". O comentário no código admite que é "double-tick" mas declara
seguro. Na prática, o `PlayEveryWare.EOSManager` (MonoBehaviour externo do
package PEW) JÁ ROUDA seu próprio `Tick()` no Update — logo, em estado normal,
o EOS SDK processa callbacks **duas vezes por frame**.

**Por quê isso importa**: cada `Tick()` processa a fila de callbacks EOS (lobby
heartbeat, auth refresh, presence updates, etc). Mesmo em fila vazia, há overhead
de C# → P/Invoke → SDK nativo. Duplicar isso é desperdício gratuito de CPU.

**Comentário existente no código** (`EOSManagerWrapper.cs:106-108`):
```
// Fallback tick: garante que o EOS SDK processa callbacks mesmo se o
// PlayEveryWare EOSManager nao estiver presente ou ativo na cena.
// Double-tick e seguro — callbacks sao removidos da fila apos disparar.
```

A intenção é ser fallback, mas não há detecção: tica sempre.

## Objetivo

Detectar se o `PlayEveryWare.EpicOnlineServices.EOSManager` MonoBehaviour está
**ativo e habilitado**. Se sim, NÃO ticar (deixar pra ele). Se não (caso raro
documentado: PEW desabilita a si mesmo em reload de play mode quando detecta
duplicata, conforme comentário em `Initialize`), aí sim ticamos como fallback.

## Investigação prévia (obrigatória)

### 1. Ler arquivo completo

```
Read: C:\Users\zegil\Documents\GitHub\ExoBeasts_V3\PI3D\Assets\Codigo\Multiplayer\Core\EOSManagerWrapper.cs
```

Pontos importantes a observar:
- `Update()` em linha ~104
- `Initialize()` em linha ~128 (já tem lógica de detecção `eosMonos.Length > 0` etc.)
- O wrapper armazena `platformInterface` cacheado para uso quando o MonoBehaviour
  desaparece — importante: a verificação de "PEW está ativo" deve usar a presença
  do MonoBehaviour viva, não só do `platformInterface` (cache).

### 2. Confirmar que a hipótese de double-tick procede

```
Grep: pattern="Tick\\(\\)" path="Packages/com.playeveryware.eos/Runtime"
```

Se Unity MCP estiver disponível:
```
mcp__UnityMCP__find_in_file uri="Packages/com.playeveryware.eos/Runtime/Core/EOSManager.cs" pattern="Update.*Tick"
```

Esperar encontrar o `Update()` de `EOSManager.cs` (PEW) que faz `_platformInterface.Tick()`.
Se NÃO encontrar, **abortar** o item e reportar — a premissa do bug pode não ser válida.

### 3. Ler comentários históricos relevantes

`Estado_Atual_Multiplayer.md` menciona "EOSManagerWrapper.cs carrega EOSConfig_Main,
valida credenciais, aguarda o EOSManager externo". Vai ajudar a entender a relação
PEW ↔ wrapper.

## Plano de mudança

### Mudança única em `EOSManagerWrapper.cs`

**Localização**: método `Update()` em ~linha 104.

**Estado atual** (CONFIRMAR antes de editar — código pode ter mudado):
```csharp
#if !EOS_DISABLE
private void Update()
{
    // Fallback tick: garante que o EOS SDK processa callbacks mesmo se o
    // PlayEveryWare EOSManager nao estiver presente ou ativo na cena.
    // Double-tick e seguro — callbacks sao removidos da fila apos disparar.
    if (isInitialized)
        GetPlatformInterface()?.Tick();
}
#endif
```

**Novo código** (substituir o método inteiro):
```csharp
#if !EOS_DISABLE
// OPTIMIZATION (Sprint 3 / Item A2 - 2026-05-XX): detectar se o PlayEveryWare EOSManager
// MonoBehaviour esta ativo + enabled. Se sim, ele ja chama Tick() no proprio Update —
// nosso Tick aqui era duplicacao gratuita (~2x CPU em P/Invoke + processamento de callbacks).
// Antes: tick incondicional sempre que isInitialized.
// Agora: tick apenas quando o PEW esta AUSENTE ou DESABILITADO (caso raro documentado em
// Initialize: PEW desabilita a si mesmo quando detecta duplicata em reload de play mode).
// Sem isso: cada frame processava callbacks EOS duas vezes — desperdicio em estado idle.
//
// Cache de PEW MonoBehaviour para evitar FindObjectsByType todo frame (caro). Resolve uma
// vez na Initialize bem-sucedida (linha ~178 ja faz a busca) e expira/refresca via
// _pewMonoCache field.
private void Update()
{
    if (!isInitialized) return;

    if (ShouldFallbackTick())
    {
        GetPlatformInterface()?.Tick();
    }
}

// Cache do MonoBehaviour PEW EOSManager. Atualizado em Initialize() e validado em ShouldFallbackTick().
private PlayEveryWare.EpicOnlineServices.EOSManager _pewMonoCache;

private bool ShouldFallbackTick()
{
    // Hot path — primeiro check rapido.
    if (_pewMonoCache != null && _pewMonoCache.isActiveAndEnabled)
        return false; // PEW ja vai ticar — pular

    // PEW null ou desabilitado: refrescar cache (caso o MonoBehaviour tenha sido recriado).
    // FindObjectsByType eh caro — fazemos no maximo uma vez por segundo via _lastPewSearchTime.
    if (Time.unscaledTime - _lastPewSearchTime >= 1f)
    {
        _lastPewSearchTime = Time.unscaledTime;
        var monos = UnityEngine.Object.FindObjectsByType<PlayEveryWare.EpicOnlineServices.EOSManager>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        _pewMonoCache = monos.Length > 0 ? monos[0] : null;

        if (_pewMonoCache != null && _pewMonoCache.isActiveAndEnabled)
            return false;
    }

    // PEW nao existe OU existe mas esta disabled — somos o fallback. Ticar.
    return true;
}

private float _lastPewSearchTime = -10f;
#endif
```

### Justificativa da implementação

- **Hot path primeiro**: a checagem `_pewMonoCache != null && _pewMonoCache.isActiveAndEnabled`
  é O(1). Em estado normal (PEW vivo), retorna em 2 instruções e pulamos o tick.
- **Refresh do cache rate-limited**: `FindObjectsByType` é caro (varre toda a hierarchy).
  Limitar a 1x por segundo é mais que suficiente — PEW raramente é destruído/recriado.
- **`FindObjectsInactive.Include`**: PEW pode ter MonoBehaviour disabled; ainda queremos
  detectá-lo para invalidar o cache se ele revivar.

### Refatoração ao invés de inline

A função `ShouldFallbackTick()` é separada por dois motivos:
1. Testabilidade futura (poderia adicionar `[ContextMenu]` para debug se necessário).
2. Ler o `Update()` em isolamento fica trivial — facilita debug por outros agentes.

## Validação

### 1. Build limpo
```powershell
dotnet build PI3D.sln
```
Esperar `0 Erro(s)`. 52 warnings pré-existentes OK.

### 2. Validação Unity (se Unity Editor estiver rodando)
```
mcp__UnityMCP__read_console action="get" count="20"
```
Não deve haver erros novos relativos a `EOSManagerWrapper`.

### 3. Validação funcional manual (quando possível)
- Iniciar Editor → cena `MenuScene`
- Click "Multiplayer"
- Esperar EOS auth completar (deve logar `[EOSAuthenticator] Login bem-sucedido!`)
- Criar lobby (deve logar `[LobbyManager] Lobby criado no EOS: <id>`)

Se ambos eventos disparam, EOS SDK está processando callbacks normalmente — significa
que o PEW está ticando OU nosso fallback está cobrindo.

### 4. Validação de performance (opcional — só se Unity Profiler disponível)
- Profiler CPU em modo Deep Profile
- Procurar por chamadas `PlatformInterface.Tick`
- Antes do fix: 2 chamadas por frame
- Depois do fix: 1 chamada por frame (em estado normal)

## Critérios de aceitação

- [ ] Build limpo (0 erros)
- [ ] Login EOS continua funcionando (criar lobby retorna sucesso)
- [ ] Cliente consegue conectar a lobby criado pelo host (validar que callbacks de
      lobby continuam disparando — host vê membro entrar)
- [ ] Comentário explicativo presente no código

## Riscos e mitigação

| Risco | Probabilidade | Mitigação |
|---|---|---|
| PEW raramente fica disabled mas quando fica, callbacks param de processar | Baixa | Cache rate-limited refresca a cada 1s e religa fallback se PEW sumir |
| `FindObjectsByType` mais lento do que esperado em cenas grandes | Muito baixa | Limitado a 1x/segundo. Em pior caso adiciona ~5ms a cada segundo, vs economia de 2 ticks por frame (60Hz) |
| Mudança quebra inicialização do EOS por race condition | Baixa | Inicialização (`Initialize()`) não foi tocada — só o tick em runtime. Logs `[EOSManagerWrapper]` continuam idênticos |

## Rollback

Se algo quebrar:
```powershell
git diff Assets/Codigo/Multiplayer/Core/EOSManagerWrapper.cs    # ver mudanças
git checkout Assets/Codigo/Multiplayer/Core/EOSManagerWrapper.cs  # reverter
```

Reverter é seguro — Update antigo era estatístico um superset funcional do novo.

## Reportar ao orquestrador (template)

```
Item: A2
Status: completed
Arquivos modificados: Assets/Codigo/Multiplayer/Core/EOSManagerWrapper.cs
Build: PASS (0 erros, 52 warnings)
Validação in-game: PASS (login + criação de lobby OK) | NOT_RUN (Unity não disponível)
Métrica medida: Tick rate em estado normal — antes: 2x/frame, depois: 1x/frame (verificado por inspeção de código + log)
Riscos detectados: nenhum
Próximo item liberado para execução: true (A3)
```
