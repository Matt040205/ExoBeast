# Sprint 4 — Item G7: CommanderAbilityController Alocação por Frame

> **Tempo estimado**: ~15 minutos. **Risco**: 🟢 Baixo. **Pré-requisitos**: nenhum.
> **Pré-leitura**: `01_padroes.md` — em especial **Padrão 4 (Cache de lista)**.

## Contexto

`CommanderAbilityController.cs` faz `new List<Ability>(abilityCooldowns.Keys)` em DOIS lugares no hot path:
- Linha **143** (dentro do `Update()`)
- Linha **555** (dentro de outro método chamado frequentemente)

```csharp
// Linha 143 - Update():
List<Ability> keys = new List<Ability>(abilityCooldowns.Keys);
foreach (Ability ability in keys)
{
    if (abilityCooldowns[ability] > 0)
    {
        abilityCooldowns[ability] -= Time.deltaTime;
        ...
    }
}
```

**Por quê isso importa**:
- Cada `new List<>()` aloca memória heap.
- 4 jogadores × 60 fps × 2 lugares = **480 listas alocadas/s** durante combate ativo.
- Em 10 minutos de partida = ~290.000 listas → GC pressure mensurável.
- **Sintoma final**: stutters de frame quando GC roda (alocação de Gen0).

A razão pela qual o autor original usou `new List<>()` é correta: iterar `dict.Keys` enquanto modifica `dict[key]` lança `InvalidOperationException` em alguns cenários. A solução é **cache reutilizado**, não eliminar o snapshot.

## Objetivo

Substituir os dois `new List<Ability>(abilityCooldowns.Keys)` por um único cache estático/reutilizável da classe, eliminando alocação por frame.

## Investigação prévia (obrigatória)

### 1. Localizar exatamente os pontos

```
Grep: pattern="new List<Ability>" path="Assets/Codigo/Characters/AbilitySystem/CommanderAbilityController.cs"
```

Esperar 2 ocorrências. Se mais que 2: investigar antes de mudar (pode haver lógica que precisa de cópia independente).

### 2. Ler contexto dos dois pontos

```
Read: Assets/Codigo/Characters/AbilitySystem/CommanderAbilityController.cs (offset 130, limit 30)
Read: Assets/Codigo/Characters/AbilitySystem/CommanderAbilityController.cs (offset 545, limit 30)
```

Verificar:
- A. Os dois usos são idênticos (snapshot de keys + iteração com modificação do dict)?
- B. Algum dos usos guarda referência da lista para uso posterior (fora da função)?

Se **B for verdade** em algum dos dois pontos: o cache compartilhado quebra. Aí cada ponto precisa de seu próprio cache OU manter alocação naquele ponto.

### 3. Confirmar tipo do dicionário

```
Grep: pattern="abilityCooldowns" path="Assets/Codigo/Characters/AbilitySystem/CommanderAbilityController.cs" -n
```

Confirmar que é `Dictionary<Ability, float>`. Se for outro tipo, ajustar tipo do cache.

## Plano de mudança

### Mudança em `CommanderAbilityController.cs`

#### Adicionar campo privado (próximo aos outros campos da classe, ~linha 30-40)

```csharp
// OPTIMIZATION (Sprint 4 / Item G7 - 2026-MM-DD): cache reutilizavel para iterar
// abilityCooldowns.Keys sem alocar lista nova por frame.
// Antes: new List<Ability>(...) em 2 hot paths -> ~480 alocacoes/s em 4 jogadores.
// Agora: cache estatico da instancia (Clear + AddRange) -> zero alocacao por frame.
// Sem isso: ~600KB/min de garbage collection durante combate ativo.
private readonly List<Ability> _cooldownKeysCache = new List<Ability>(8);
```

Capacidade inicial `8` é estimativa generosa — a maioria dos personagens tem 3-5 abilities (Q, E, X, F, ult). Se o tipo `Ability` for mais que isso, ajustar para 16.

#### Modificar linha ~143 (Update)

**Antes**:
```csharp
List<Ability> keys = new List<Ability>(abilityCooldowns.Keys);
foreach (Ability ability in keys)
{
    if (abilityCooldowns[ability] > 0)
    {
        abilityCooldowns[ability] -= Time.deltaTime;
        if (abilityCooldowns[ability] < 0)
            abilityCooldowns[ability] = 0;
    }
}
```

**Depois**:
```csharp
// OPTIMIZATION (Sprint 4 / Item G7): usar cache reutilizavel.
_cooldownKeysCache.Clear();
foreach (var kvp in abilityCooldowns)
    _cooldownKeysCache.Add(kvp.Key);

for (int i = 0; i < _cooldownKeysCache.Count; i++)
{
    Ability ability = _cooldownKeysCache[i];
    if (abilityCooldowns[ability] > 0)
    {
        abilityCooldowns[ability] -= Time.deltaTime;
        if (abilityCooldowns[ability] < 0)
            abilityCooldowns[ability] = 0;
    }
}
```

**Por que `for` em vez de `foreach`**: `List<T>.Enumerator` ainda aloca pequeno (struct boxed em alguns cenários do Mono). `for` com índice é zero-alloc garantido.

**Por que iterar Keys via foreach no Add em vez de `AddRange`**: `Dictionary<TKey,TValue>.Keys.AddRange()` em algumas versões do .NET aloca um array intermediário. O `foreach` no KVP usa o struct enumerator do Dictionary, zero-alloc.

#### Modificar linha ~555 (segundo ponto)

Mesmo padrão. **MAS atenção**: se o segundo ponto não for em hot path (ex: chamado uma vez ao entrar em estado X), pode-se manter `new List<>()` lá — não vai impactar GC. Decidir baseado em onde está chamado.

```
Read: Assets/Codigo/Characters/AbilitySystem/CommanderAbilityController.cs (offset 540, limit 40)
```

Se for em hot path: aplicar mesmo padrão usando `_cooldownKeysCache` (já que é private da classe e thread-único — sem race).

Se for chamado raramente (ex: ao morrer, ao trocar personagem): pode manter `new List<>()` ou usar o mesmo cache. Se usar o cache, **adicionar comentário** explicando a chamada para reentrancy não ser um problema.

### Verificar uso compartilhado seguro

Se ambas as funções podem rodar no mesmo frame mas SERIALMENTE (uma após outra):
- ✅ Compartilhar `_cooldownKeysCache` é seguro
- A primeira escreve cache, itera, terminou; segunda escreve cache, itera, terminou.

Se há reentrância (uma chama outra durante iteração):
- ❌ Compartilhar quebra
- Solução: ter `_cooldownKeysCacheA` e `_cooldownKeysCacheB`

**Quase certamente** o caso é o primeiro (cooldown tick é simples). Mas verificar.

## Validação

### 1. Build limpo
```powershell
dotnet build PI3D.sln
```
Esperar `0 Erro(s)`.

### 2. Validação funcional manual

Cenário multiplayer com habilidades ativas:
1. Editor + cliente MPPM
2. Host iniciar partida em `CenaMapaTeste`
3. Cada jogador escolher comandante com várias habilidades (Coruja: Q, E, X, ult)
4. Usar habilidades — verificar que **cooldowns reduzem visualmente** (UI de cooldown deve girar/decrementar)
5. Esperar cooldown completo — habilidade volta a ser usável

Se cooldown ficar **preso** (não decrementa) ou **resetar instantaneamente** (zero todo frame): rollback.

### 3. Validação de performance (opcional)

Unity Profiler em modo Deep Profile:
1. Janela "Memory" → procurar alocações de `List<Ability>` em runtime
2. Antes do fix: ~8 listas/s por player (em 60Hz com 2 pontos sendo chamados)
3. Depois do fix: 0 listas/frame

## Critérios de aceitação

- [ ] Build limpo (0 erros)
- [ ] Cooldowns de habilidades funcionam corretamente em multiplayer
- [ ] Field `_cooldownKeysCache` adicionado com comentário OPTIMIZATION
- [ ] Ambos os pontos (linha ~143 e ~555) refatorados (ou justificativa por escrito de manter o segundo)
- [ ] Iteração com `for` (não `foreach`) para zero-alloc

## Riscos e mitigação

| Risco | Probabilidade | Mitigação |
|---|---|---|
| Reentrância: função chama outra durante iteração e ambas usam o cache | Baixa | Verificar fluxo antes; se reentrância existir, usar 2 caches |
| Concorrência: Unity é thread-único então OK, mas se houver Job System | Inexistente | Cooldowns rodam em main thread |
| Cache não cresce a tempo se personagem ganhar mais skills runtime | Baixa | Capacidade inicial 8 + List<> auto-cresce |

## Rollback

```powershell
git checkout Assets/Codigo/Characters/AbilitySystem/CommanderAbilityController.cs
```

## Reportar ao orquestrador (template)

```
Item: G7
Status: completed
Arquivos modificados: Assets/Codigo/Characters/AbilitySystem/CommanderAbilityController.cs
Build: PASS (0 erros, 52 warnings)
Validacao in-game: PASS (cooldowns OK em 4 personagens) | NOT_RUN
Metrica medida: List<Ability> alocacoes/frame em hot path — antes: 2/frame, depois: 0
Riscos detectados: nenhum
Proximo item liberado: true (E6 paralelo se ainda nao executado)
Notas: cache compartilhado entre dois pontos (linha 143 e 555) é seguro pois ambos rodam serialmente sem reentrancia. Se G7 for revisitado, considerar mover dictionary `abilityCooldowns` para `Dictionary<int, float>` com IDs em vez de ScriptableObject keys.
```

## Notas finais

GC pressure é um problema "silencioso" — não aparece em logs, mas causa stutters perceptíveis em frames de coleção. Eliminar alocações em hot paths é uma das melhorias mais alto-ROI possível em projetos Unity.

Próximo passo natural após G7: auditar **outros** Updates em personagens/inimigos por padrões `new List<>()` similares. Não fazer nesta sprint, mas anotar no relatório se encontrar.
