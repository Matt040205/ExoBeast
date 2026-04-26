# Configuracao Segura de Credenciais EOS

Status: ativo
Publico: quem cria ou valida `EOSCredentials.json`
Ler primeiro: [../Docs/Estado_Atual_Multiplayer.md](../Docs/Estado_Atual_Multiplayer.md)
Nao usar como fonte de verdade: valores de credenciais reais

As credenciais do Epic Online Services sao secretas e nao devem ser commitadas.
Este arquivo fala apenas do arquivo de credenciais e de como o projeto o consome.

## Arquivo esperado

- Crie `EOSCredentials.json` na raiz do projeto, ao lado de `Assets/`.
- Mantenha esse arquivo fora do Git.

## Campos esperados

```json
{
  "ProductId": "seu_product_id",
  "SandboxId": "seu_sandbox_id",
  "DeploymentId": "seu_deployment_id",
  "ClientId": "seu_client_id",
  "ClientSecret": "seu_client_secret",
  "EncryptionKey": "sua_encryption_key_64_caracteres"
}
```

## Como o projeto usa isso

- `Core/EOSConfig.cs` carrega o arquivo de credenciais.
- `Core/EOSManagerWrapper.cs` valida e usa esses dados na inicializacao do EOS.
- O plugin externo continua sendo `PlayEveryWare.EpicOnlineServices.EOSManager`.

## Boas praticas

- Nunca hardcode credenciais em script.
- Nunca compartilhe o arquivo em chat ou print.
- Use credenciais de desenvolvimento separadas das credenciais de producao.
- Revogue e recrie as credenciais se elas vazarem.

## Erros comuns

- Arquivo nao encontrado: confira se ele esta na raiz do projeto.
- JSON invalido: confira virgulas, aspas e chaves.
- Credenciais incompletas: confira todos os campos do template.
