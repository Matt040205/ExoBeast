# Git Workflow - Branch Multiplayer

## 🎯 Objetivo

Criar branch `Multiplayer` com todo o sistema multiplayer, mantendo a `main` intacta.

---

## ⚠️ ANTES DE COMEÇAR

### 1. Criar Arquivo de Credenciais

**Localização:** `PI3D/EOSCredentials.json` (raiz do projeto, NÃO em Assets/)

**Template:** Use `Assets/Codigo/Multiplayer/EOSCredentials.json.example`

```json
{
  "ProductId": "sua_product_id_aqui",
  "SandboxId": "sua_sandbox_id_aqui",
  "DeploymentId": "seu_deployment_id_aqui",
  "ClientId": "sua_client_id_aqui",
  "ClientSecret": "sua_client_secret_aqui",
  "EncryptionKey": "sua_encryption_key_64_chars"
}
```

### 2. Verificar .gitignore

```bash
git status
```

**✅ EOSCredentials.json NÃO deve aparecer na lista!**

Se aparecer, PARE! Algo está errado com o `.gitignore`.

---

## 📝 Passo a Passo

### Passo 1: Verificar Estado Atual

```bash
cd C:\Users\zegil\Documents\GitHub\ExoBeasts_V3\PI3D
git status
```

Deve mostrar:
- ✅ Branch atual: `main`
- ✅ Vários arquivos novos/modificados
- ❌ `EOSCredentials.json` NÃO deve aparecer

---

### Passo 2: Adicionar Arquivos

```bash
git add .
```

---

### Passo 3: Verificar o Que Será Commitado

```bash
git status
```

**CRÍTICO:** Certifique-se que `EOSCredentials.json` **NÃO** está na lista!

---

### Passo 4: Criar Branch Multiplayer

```bash
git checkout -b Multiplayer
```

Isso cria a branch `Multiplayer` a partir da `main` atual.

---

### Passo 5: Fazer Commit

```bash
git commit -m "feat: Sistema multiplayer P2P completo

- Implementada arquitetura P2P com Unity Netcode for GameObjects
- Integração com Epic Online Services (Lobbies + P2P)
- Sistema seguro de credenciais (não commitado)
- 17 scripts base implementados:
  * Core: NetworkBootstrap, EOSManager, EOSConfig, HostManager
  * Auth: EOSAuthenticator, SessionManager
  * Lobby: LobbyManager, LobbyUI, LobbyData, LobbyItemUI
  * GameServer: GameServerManager, MatchManager, PlayerRegistry
  * Sync: NetworkedPlayerController, Currency, Building, Horde
- Documentação completa em português
- Cenas: NetworkBootstrap, LobbyScene
- Pacotes adicionados: Netcode, Transport, Multiplayer Tools
- .gitignore atualizado para proteger credenciais

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

### Passo 6: Enviar para GitHub

```bash
git push -u origin Multiplayer
```

Isso cria a branch `Multiplayer` no GitHub e faz o push.

---

### Passo 7: Verificar Branch Main Intacta

```bash
git checkout main
git status
```

A `main` deve estar limpa, sem as mudanças do multiplayer.

---

## ✅ Resultado Final

Após executar todos os passos:

- ✅ Branch `main` intacta (sem multiplayer)
- ✅ Branch `Multiplayer` criada com todo o sistema
- ✅ Credenciais protegidas (não commitadas)
- ✅ Push feito para o GitHub

---

## 🔄 Trabalhando na Branch Multiplayer Depois

```bash
# Mudar para branch Multiplayer
git checkout Multiplayer

# Fazer suas mudanças...

# Adicionar e commitar
git add .
git commit -m "descrição da mudança"

# Enviar para GitHub
git push
```

---

## 🔀 Mesclar Multiplayer na Main (Futuro)

**Quando o sistema estiver pronto:**

```bash
# Ir para main
git checkout main

# Mesclar Multiplayer
git merge Multiplayer

# Resolver conflitos (se houver)

# Enviar para GitHub
git push
```

---

## 🆘 Troubleshooting

### EOSCredentials.json aparece no git status

**Problema:** `.gitignore` não está funcionando

**Solução:**
```bash
git rm --cached EOSCredentials.json
git status  # Não deve mais aparecer
```

### Esqueci de criar EOSCredentials.json

**Não tem problema!** Crie agora antes de fazer commit.

### Commitei credenciais por engano

**URGENTE:**
1. Revocar credenciais no Epic Developer Portal
2. Gerar novas credenciais
3. Limpar histórico Git (use BFG Repo-Cleaner)

---

## 📞 Suporte

Em caso de dúvidas sobre Git, consulte:
- [Git Documentation](https://git-scm.com/doc)
- Equipe de desenvolvimento
