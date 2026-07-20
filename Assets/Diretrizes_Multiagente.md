# Diretrizes Multiagente

Status: ativo
Publico: Claude, Codex e Gemini
Ler primeiro: `Assets/CoreScripts/Docs/Estado_Atual_Multiplayer.md`
Nao usar como fonte de verdade: docs historicas, planos antigos e nomes nao confirmados

## Contrato

- Ler o estado atual antes de comentar ou editar.
- Confirmar nomes reais de arquivos, classes, cenas e pastas antes de citar ou editar.
- Se algo nao puder ser confirmado no repositorio, trate como hipotese e marque a incerteza.
- Preservar mudancas ja existentes; nao reverter trabalho alheio.
- Se um comportamento mudar, atualizar a documentacao afetada.
- Separar fato atual, hipotese e contexto historico.
- Se docs antigas conflitam com o codigo atual, vale `codigo atual > docs atuais > docs historicas`.

## Multiplayer

- A referencia primaria e `Assets/CoreScripts/Docs/Estado_Atual_Multiplayer.md`.
- Nao usar `EOSManager.cs`, `NetworkedCurrency.cs` ou `NetworkedHorde.cs` como verdade atual.
- Distinguir Editor/MPPM de builds ao descrever conexao, Relay e inicio de partida.

## Exo Config (ferramenta de automacao de prefabs)

- A referencia primaria e `Assets/CoreScripts/Docs/Estado_Atual_ExoConfig.md` — leia inteiro antes de tocar em `Assets/Editor/Exo*.cs` ou `Assets/Editor/ExoConfig/`.
- Refatoracao em andamento (branch `exo-config-refactor`), fases 1-5 de 9 concluidas na sessao anterior.
- Nao usar o dossie externo `message.txt` (fora do repo) como fonte de verdade — descreve o comportamento ANTIGO do plugin.

## Resposta final

- Dizer o que mudou.
- Citar os arquivos afetados.
- Explicar riscos ou dependencias quando existirem.
- Marcar o que ficou historico.
