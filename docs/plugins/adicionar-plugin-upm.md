# Checklist Para Adicionar Plugin UPM

1. Registrar o motivo do plugin e o subsistema afetado.
2. Preferir tag ou commit fixo; nao usar branch movel quando o projeto depende do pacote.
3. Adicionar a dependencia em `Packages/manifest.json`.
4. Abrir Unity e confirmar `packages-lock.json`.
5. Verificar se o plugin cria assets, copia DLLs ou instala dependencias duplicadas.
6. Criar uma camada propria do projeto antes de chamar plugin em gameplay.
7. Se precisar embutir o pacote para patch local, registrar origem, commit/tag e motivo.
8. Documentar rollback.
9. Rodar smoke test do subsistema afetado.

## Criterio De Recusa

- Plugin exige versao de Unity acima da usada no projeto.
- Plugin duplica dependencia ja instalada.
- Plugin altera assets de projeto automaticamente sem revisao.
- Plugin exige segredo, token ou credencial no repositorio.
