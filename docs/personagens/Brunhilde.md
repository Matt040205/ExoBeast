# Brunhilde

Status: ativo, documentacao inicial.

## Estrutura

- Raiz: `Assets/aaPasta/Personagens/Brunhilde`.
- Scripts principais: `CoreScripts/HabilidadeAquiNao.cs`, `CoreScripts/HabilidadeTemorSismico.cs`.
- Caminhos conhecidos: Controle de Grupo, Suporte, Tank.
- Audio conhecido: `event:/SFX/HammerSwing`, `event:/SFX/SeismicSlam`.

## Validacao Obrigatoria

- Testar stun, knockback, defesa, suporte e tanque em host e cliente.
- Confirmar que o audio 3D toca uma vez por ativacao.
- Confirmar que efeitos de area nao aplicam dano/buff duplicado em clientes.

## Pendencias

- Registrar prefab principal, torre e ScriptableObject canonico.
- Documentar todas as passivas de caminho e seus limites.
- Mapear animacoes usadas pelas habilidades.
