# Configuracao Segura de Credenciais EOS

Status: ativo
Publico: desenvolvedores do projeto e pipelines CI/CD
Ler primeiro: [../Docs/Estado_Atual_Multiplayer.md](../Docs/Estado_Atual_Multiplayer.md)

## Como funciona

O sistema de credenciais EOS usa auto-geracao: os configs que o plugin PlayEveryWare
precisa em `StreamingAssets/EOS/` sao gerados automaticamente a cada build e play mode,
a partir de uma fonte segura de credenciais.

### Cadeia de prioridade

1. **Variaveis de ambiente** (prioridade maxima — usado em CI/CD)
2. **EOSCredentials.json** na raiz do projeto (fallback local — gitignored)
3. **Erro claro** se nenhuma fonte encontrada

### Gatilhos de geracao

| Gatilho | Quando |
|---------|--------|
| Pre-build | Automatico via `IPreprocessBuildWithReport` — gera configs antes de cada build |
| Play Mode | Automatico ao entrar em Play Mode no editor |
| Menu manual | `Tools > ExoBeasts > Generate EOS Config` |

---

## Opcao A: Arquivo local (desenvolvimento)

1. Copie `EOSCredentials.json.template` para `EOSCredentials.json` na raiz do projeto
2. Preencha com suas credenciais do [Epic Developer Portal](https://dev.epicgames.com/portal/)
3. O arquivo ja esta no `.gitignore` — nunca sera commitado

```json
{
  "ProductId": "seu_product_id",
  "SandboxId": "seu_sandbox_id",
  "DeploymentId": "seu_deployment_id",
  "ClientId": "seu_client_id",
  "ClientSecret": "seu_client_secret",
  "EncryptionKey": "sua_encryption_key_64_chars_hex",
  "Environment": "Development"
}
```

## Opcao B: Variaveis de ambiente (CI/CD e producao)

Defina as seguintes variaveis no seu sistema ou pipeline:

| Variavel | Obrigatoria | Descricao |
|----------|-------------|-----------|
| `EOS_PRODUCT_ID` | Sim | Product ID do Epic Dev Portal |
| `EOS_SANDBOX_ID` | Sim | Sandbox ID (especifico do ambiente) |
| `EOS_DEPLOYMENT_ID` | Sim | Deployment ID (especifico do ambiente) |
| `EOS_CLIENT_ID` | Sim | Client ID |
| `EOS_CLIENT_SECRET` | Sim | Client Secret |
| `EOS_ENCRYPTION_KEY` | Nao | Chave de criptografia (64 caracteres hex) |
| `EOS_ENVIRONMENT` | Nao | Development / Staging / Live (default: Development) |

### Exemplo: GitHub Actions

```yaml
jobs:
  build:
    runs-on: ubuntu-latest
    env:
      EOS_PRODUCT_ID: ${{ secrets.EOS_PRODUCT_ID }}
      EOS_SANDBOX_ID: ${{ secrets.EOS_SANDBOX_ID }}
      EOS_DEPLOYMENT_ID: ${{ secrets.EOS_DEPLOYMENT_ID }}
      EOS_CLIENT_ID: ${{ secrets.EOS_CLIENT_ID }}
      EOS_CLIENT_SECRET: ${{ secrets.EOS_CLIENT_SECRET }}
      EOS_ENCRYPTION_KEY: ${{ secrets.EOS_ENCRYPTION_KEY }}
      EOS_ENVIRONMENT: Development
```

### Exemplo: variavel local no Windows (PowerShell)

```powershell
$env:EOS_PRODUCT_ID = "seu_product_id"
$env:EOS_SANDBOX_ID = "seu_sandbox_id"
# ... etc
```

---

## Erros comuns

| Erro | Causa | Solucao |
|------|-------|---------|
| "Nenhuma fonte de credenciais EOS encontrada" | Nem env vars nem JSON local | Crie EOSCredentials.json ou defina env vars |
| "ProductId ausente" | Campo vazio na fonte | Verifique o JSON ou a variavel de ambiente |
| "EncryptionKey deve ter 64 caracteres" | Chave com tamanho errado | Gere uma chave hex de 64 chars |
| "Build cancelada" | IPreprocessBuild falhou validacao | Corrija a fonte de credenciais e tente novamente |

---

## Seguranca

- **Nunca** commitar `EOSCredentials.json` (ja esta no `.gitignore`)
- **Nunca** commitar os JSONs gerados em `StreamingAssets/EOS/` (ja estao no `.gitignore`)
- Logs do sistema nunca imprimem ClientSecret ou EncryptionKey
- Use credenciais diferentes para Development, Staging e Live
- O `ClientSecret` do EOS e uma client credential publica (viaja no binario do jogo), mas ainda assim nao deve ficar no source control

## Rotacao de credenciais

Se credenciais vazarem (ex.: commitadas acidentalmente no historico git):

1. Acesse [Epic Developer Portal](https://dev.epicgames.com/portal/)
2. Em Product Settings > Clients > delete o client comprometido
3. Crie um novo client e copie o novo ClientId e ClientSecret
4. Atualize `EOSCredentials.json` local e/ou secrets do CI
5. Teste autenticacao
6. (Opcional) Limpe o historico git com `git filter-repo` ou BFG Repo Cleaner

## Arquitetura

```
                    Prioridade
                        |
    Env vars ──────────►|
                        ├──► EOSConfigGenerator.cs ──► StreamingAssets/EOS/*.json
    EOSCredentials.json►|         (Editor)                      |
                                                                ▼
                                                    PlayEveryWare EOS Plugin
                                                         (Runtime)
                                                                |
                                                                ▼
                                                    EOSConfig.cs (ScriptableObject)
                                                    EOSManagerWrapper.cs
```
