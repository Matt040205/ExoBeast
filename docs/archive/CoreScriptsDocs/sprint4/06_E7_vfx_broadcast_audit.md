# Sprint 4 — Item E7: Auditoria de VFX/SFX Broadcast vs Owner-Only

> **Tempo estimado**: ~1-2 horas. **Risco**: 🟡 Médio. **Pré-requisitos**: G3 (Sprint 3) mergeado.
> **Pré-leitura**: `01_padroes.md` — em especial **Padrão 5 (Owner-Proxy)** e **Padrão 6 (Local-only feedback)**.

## Contexto

O projeto usa três modelos para sincronizar efeitos visuais/sonoros em multiplayer:

1. **ClientRpc broadcast** — pacote enviado pelo server para TODOS os clientes (`[ClientRpc]` sem TargetClientIds)
2. **ClientRpc owner-targeted** — pacote enviado apenas para o owner (`ClientRpcParams.Send.TargetClientIds = {OwnerClientId}`)
3. **Owner-Proxy local** — host invoca método local que dispara VFX no owner-cliente quando ability não tem `NetworkObject.Spawn` (documentado em MEMORY.md como "Owner-Proxy pattern")

E para feedback puramente local (sem network):
4. **Eventos C# locais** — cada cliente assina `OnDamageDealt`, `OnEnemyKilled` etc. e dispara seu próprio VFX

Sprint 4 / E7 audita se o **modelo correto** está sendo usado em cada lugar. Especificamente:
- VFX que devem ser **vistos por todos** (explosão grande, ult de personagem) → ClientRpc broadcast OK
- VFX que devem ser **vistos só pelo owner** (UI hint, "cooldown ready" indicator) → owner-targeted ClientRpc ou local
- VFX/SFX **puramente reativos** (hit feedback, "+50 ouro" notification) → local via evento

## Objetivo

Auditar `[ClientRpc]` em pasta `Characters/` e `Abilities/`. Para cada um:
- Confirmar **modelo correto** (broadcast vs owner-targeted vs local)
- Documentar achado no relatório
- **Refatorar** apenas casos com escolha errada **óbvia** (sem ambiguidade)
- Casos ambíguos: **listar no relatório** para revisão pelo orquestrador

## Investigação prévia (obrigatória)

### 1. Mapear todos os ClientRpcs em Characters e Abilities

```
Grep: pattern="\[ClientRpc\]" path="Assets/Codigo/Characters" -n
Grep: pattern="\[ClientRpc\]" path="Assets/Codigo/Abilities" -n
```

Esperar encontrar dezenas. Cada ocorrência tem:
- Arquivo + linha
- Método declarado abaixo
- Possíveis chamadas com ClientRpcParams ou broadcast puro

### 2. Para cada ClientRpc encontrado, classificar:

Criar uma tabela mental:

| Arquivo:linha | Método | É broadcast? | Conteúdo (resumo) | Categoria |
|---|---|---|---|---|
| ex: CommanderAbilityController.cs:187 | ActivateAbilityVisualClientRpc | broadcast | Instancia VFX em todos | "Globalmente visível" → OK |
| ex: PlayerHealthSystem.cs:???? | ShowHealVisualClientRpc | broadcast | Particula de cura local | "Apenas owner" → REFATORAR |
| ... | ... | ... | ... | ... |

### 3. Categorias de classificação

#### Categoria A — Globalmente Visível (broadcast OK)
- VFX/SFX que outros jogadores devem ver
- Ex: ult de Coruja (todos veem garras), explosão de armadilha, dano em torre, marcado por habilidade
- Mantém ClientRpc broadcast atual

#### Categoria B — Apenas Owner (owner-targeted)
- Feedback que só o owner deve ver/ouvir
- Ex: UI hint, "low HP" warning, cooldown ready beep, "+50 ouro" notification
- Refatorar para `ClientRpcParams.Send.TargetClientIds = {OwnerClientId}`

#### Categoria C — Local-only (sem network)
- Feedback que cada cliente decide localmente baseado em evento
- Ex: hit feedback (cliente sabe quando bateu), camera shake (cliente sabe quando levou hit), particle de coleta de ouro
- Refatorar para evento local + `OnXxxLocal()` listener

#### Categoria D — Ambíguo (REPORTAR)
- Não fica claro qual o uso correto sem contexto de design
- Ex: "anel de ult charging" — broadcast (todos veem que ult está pronta) ou owner-only (só o owner vê)?
- Não refatorar; documentar no relatório

## Plano de mudança

### Sub-passo A — Discovery (~30 min)

Criar arquivo temporário `audit_e7.md` em `/tmp/` (ou no clipboard) com tabela completa:

```markdown
# E7 Audit — descoberta de ClientRpcs

## Categoria A (mantém broadcast)
- CommanderAbilityController.cs:187 ActivateAbilityVisualClientRpc — VFX de ult, todos veem
- ...

## Categoria B (refatorar para owner-only)
- ArquivoX.cs:NN MethodNameClientRpc — UI hint só do owner

## Categoria C (refatorar para local)
- ArquivoY.cs:NN HitFeedbackClientRpc — pode ser evento local OnDamageTaken

## Categoria D (ambíguo, reportar)
- ArquivoZ.cs:NN UltimateReadyClientRpc — broadcast OK ou owner-only?
```

**Tempo**: 30 min se você for cuidadoso. Não pular.

### Sub-passo B — Refatorações Categoria B (~30 min)

Para cada Categoria B:

**Antes**:
```csharp
[ClientRpc]
private void ShowHintClientRpc()
{
    if (hintUI != null) hintUI.Show();
}
```

**Depois**:
```csharp
// OPTIMIZATION (Sprint 4 / Item E7 - 2026-MM-DD): hint UI e exclusiva do owner.
// Antes: broadcast para todos os clientes (3 pacotes desperdicados em 4-player lobby).
// Agora: owner-targeted - apenas o jogador relevante recebe.
// Sem isso: outros jogadores recebem packets que silenciosamente ignoram.
[ClientRpc]
private void ShowHintClientRpc(ClientRpcParams clientRpcParams = default)
{
    if (hintUI != null) hintUI.Show();
}

// Caller deve usar:
// var rpcParams = new ClientRpcParams {
//     Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
// };
// ShowHintClientRpc(rpcParams);
```

E atualizar **todas as chamadas** no servidor para passar `rpcParams`.

### Sub-passo C — Refatorações Categoria C (~30 min)

Para cada Categoria C:

**Antes**:
```csharp
// Server-side:
private void OnTakeDamage(float damage) {
    HandleHitFeedbackClientRpc(damage);
}

[ClientRpc]
private void HandleHitFeedbackClientRpc(float damage) {
    // efeito visual local
    if (CameraShakeManager.Instance != null) CameraShakeManager.Instance.Shake(0.2f);
}
```

**Depois**:
```csharp
// Server-side:
private void OnTakeDamage(float damage) {
    OnDamageTaken?.Invoke(damage); // evento local
    // ClientRpc REMOVIDO
}

// Cada cliente assina:
void OnEnable() {
    healthSystem.OnDamageTaken += HandleLocalHitFeedback;
}

private void HandleLocalHitFeedback(float damage) {
    if (CameraShakeManager.Instance != null) CameraShakeManager.Instance.Shake(0.2f);
}
```

**Cuidado**: para hit feedback de **tomar** dano, isso só funciona se o NetworkVariable de HP já está sincronizado e o cliente "vê" o delta. Verificar que `currentHealth.OnValueChanged` é assinado, e que feedback é disparado no listener:

```csharp
private void OnHealthChanged(float oldValue, float newValue) {
    if (newValue < oldValue) {
        // Tomei dano!
        HandleLocalHitFeedback(oldValue - newValue);
    }
}
```

Esse padrão **elimina** ClientRpc inteiro — cliente reage ao NetworkVariable change que já tinha que existir.

### Sub-passo D — Ambíguos (Categoria D)

Para cada Categoria D, **NÃO refatorar**. Apenas documentar no relatório:

```
Categoria D / decisao pendente:
- Arquivo:linha — descricao do ClientRpc
- Pergunta: deve ser broadcast ou owner-only?
- Caso 1 (broadcast): ganho =0, perda potencial = X
- Caso 2 (owner-only): ganho = Y, perda potencial = Z
- Recomendacao: <opiniao do agente>
```

## Validação

### 1. Build limpo
```powershell
dotnet build PI3D.sln
```

### 2. Validação funcional

**Não pode haver regressão visual**. Para cada arquivo modificado:

1. Iniciar editor + 1 MPPM cliente
2. Host inicia partida em `CenaMapaTeste`
3. Reproduzir o cenário onde o VFX deveria aparecer
4. Validar que:
   - **Categoria B refatorada**: VFX aparece para owner, NÃO aparece para outros
   - **Categoria C refatorada**: VFX aparece localmente quando evento dispara

Cenários típicos a testar:
- Tomar dano (host + cliente)
- Usar ability normal (host + cliente)
- Usar ult (host + cliente)
- Matar inimigo (host + cliente)
- Ganhar ouro (host + cliente)

### 3. Validação Network Profiler (se disponível)

Antes do fix vs depois: redução em packets/s durante combate.

Estimativa: 5-15% de redução em packets/s (depende de quantos foram refatorados).

## Critérios de aceitação

- [ ] Build limpo (0 erros)
- [ ] Audit completo documentado (todos ClientRpcs em Characters/Abilities classificados)
- [ ] Categoria B (owner-only): refatorados sem regressão visual
- [ ] Categoria C (local): refatorados sem regressão visual
- [ ] Categoria D (ambíguo): listados no relatório com recomendação
- [ ] Comentários OPTIMIZATION em cada refatoração
- [ ] Tabela de audit anexada ao relatório

## Riscos e mitigação

| Risco | Probabilidade | Mitigação |
|---|---|---|
| Refatorar ClientRpc para evento local quebra timing (cliente reage antes do servidor confirmar) | Possível | Manter NetworkVariable sync; evento dispara no `OnValueChanged` (já é "depois do server confirmar") |
| Owner-targeted falha em modo singleplayer (sem NetworkManager) | Baixa | Code path já tem `if (!IsServer) return` + `IsSpawned` guards |
| Categoria classificada errado (algo da Categoria A vai para B) | Médio | Validação manual passo 2 cobre — se VFX desaparece para outros, voltar para broadcast |
| Caller esquecido — método refatorado mas chamada não atualizada | Médio | `dotnet build` falha por compilation error |

## Rollback

Listar todos os arquivos modificados no audit. Para reverter um específico:
```powershell
git checkout <arquivo>
```

Para reverter tudo:
```powershell
git diff --name-only | xargs -I {} git checkout {}
```

## Reportar ao orquestrador (template)

```
Item: E7
Status: completed | partial
Arquivos modificados: <lista>
Build: PASS (0 erros, 52 warnings)
Validacao in-game: PASS (X cenarios) | PARTIAL (Y refatorados, Z pendentes)
Metrica medida: ClientRpcs broadcast em combate ativo — antes: ~A/s, depois: ~B/s

# Audit completo:
## Categoria A (broadcast OK, mantido)
<lista>

## Categoria B (refatorado para owner-only)
<lista com diff resumido>

## Categoria C (refatorado para local)
<lista com diff resumido>

## Categoria D (ambíguo, decisao do orquestrador)
<lista com recomendacoes>

Riscos detectados: <lista ou nenhum>
Proximo item liberado: true (V1 paralelo, ja em andamento)
```

## Notas finais

E7 é mais investigação que refactor. Não há "checkpoint quantitativo" simples como nos outros itens — o ganho depende de quantos ClientRpcs foram identificados como mal-categorizados.

**Não tente refatorar tudo em uma sessão**. Se descobrir 30+ ClientRpcs, focar nos **5-10 mais óbvios** (Categoria B clara) e listar resto como "Sprint 5 candidates".

**Princípio**: ClientRpc broadcast é a opção **mais cara**; deve ser usado apenas quando há benefício claro de "todos verem o efeito". Local + evento é sempre preferível quando aplicável.
