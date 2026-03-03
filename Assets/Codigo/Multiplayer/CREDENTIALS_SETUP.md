# Configuração Segura de Credenciais EOS

## ⚠️ IMPORTANTE - SEGURANÇA

As credenciais do Epic Online Services são **CONFIDENCIAIS** e **NÃO DEVEM** ser commitadas no Git!

## 📋 Passo a Passo

### 1. Criar Arquivo de Credenciais

Na **raiz do projeto** (mesmo nível que Assets/), crie um arquivo chamado:
```
EOSCredentials.json
```

### 2. Preencher com Suas Credenciais

Use o template `EOSCredentials.json.example` como base:

```json
{
  "ProductId": "seu_product_id_real",
  "SandboxId": "seu_sandbox_id_real",
  "DeploymentId": "seu_deployment_id_real",
  "ClientId": "seu_client_id_real",
  "ClientSecret": "seu_client_secret_real",
  "EncryptionKey": "sua_encryption_key_64_caracteres"
}
```

### 3. Obter Credenciais do Epic Developer Portal

1. Acesse: https://dev.epicgames.com/portal
2. Selecione seu produto
3. Anote os valores:
   - **Product Settings** → Product ID
   - **Product Settings** → Sandbox ID (Development)
   - **Deployments** → Deployment ID
   - **Clients** → Client ID e Client Secret
   - **Game Services** → Encryption Key (se aplicável)

### 4. Verificar .gitignore

O arquivo `.gitignore` já está configurado para **ignorar** o arquivo de credenciais:

```
EOSCredentials.json
**/EOSCredentials.json
```

✅ Isso garante que suas credenciais **NUNCA** serão enviadas para o GitHub.

### 5. Usar no Unity

O script `EOSConfig.cs` carrega automaticamente as credenciais:

```csharp
// No EOSManager.cs
EOSConfig config = // referência ao ScriptableObject
config.LoadCredentialsFromFile();

if (config.ValidateCredentials())
{
    // Inicializar EOS com as credenciais
}
```

## 🔒 Boas Práticas de Segurança

### ✅ FAZER:
- Manter `EOSCredentials.json` apenas localmente
- Usar credenciais diferentes para Development/Staging/Production
- Rotacionar Client Secret periodicamente
- Compartilhar credenciais apenas via canais seguros (não email/Discord)

### ❌ NÃO FAZER:
- Commitar credenciais no Git
- Compartilhar credenciais em prints de tela
- Hardcodar credenciais em código
- Usar credenciais de produção em desenvolvimento

## 🚀 Para Outros Desenvolvedores do Time

Se outro desenvolvedor clonar o projeto:

1. Pedir as credenciais de **forma segura** (Signal, 1Password, etc)
2. Criar seu próprio arquivo `EOSCredentials.json` local
3. Nunca commitar esse arquivo

## 🔧 Troubleshooting

### Erro: "Arquivo de credenciais não encontrado"
- Verifique se `EOSCredentials.json` está na **raiz do projeto**
- Caminho esperado: `PI3D/EOSCredentials.json`

### Erro: "Credenciais incompletas"
- Verifique se todos os campos estão preenchidos
- Formato JSON deve estar correto (sem vírgulas extras)

### Credenciais vazaram no Git?
1. **Revogar** imediatamente o Client Secret no portal Epic
2. Gerar novas credenciais
3. Usar `git filter-branch` ou BFG Repo-Cleaner para limpar histórico

## 📞 Suporte

Em caso de dúvidas sobre segurança de credenciais, consulte:
- [Epic Online Services Documentation](https://dev.epicgames.com/docs/epic-online-services)
- Equipe de desenvolvimento do projeto
