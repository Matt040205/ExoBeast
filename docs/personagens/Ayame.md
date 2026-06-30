# Ayame

Status: ativo, documentacao inicial.

## Estrutura

- Raiz: `Assets/aaPasta/Personagens/Ayame`.
- Dados: `DataScripableObjects/Ayame.asset`.
- Scripts principais: `CoreScripts/CuttingBladeAbility.cs`, `CoreScripts/CuttingBladeLogic.cs`, `CoreScripts/NineTailsDanceAbility.cs`, `CoreScripts/NineTailsDanceLogic.cs`, `CoreScripts/PeaceOfMindAbility.cs`, `CoreScripts/PeaceOfMindLogic.cs`.
- Habilidades em assets: `Scriptable Objects/Lamina Cortante.asset`, `Scriptable Objects/Paz de Espirito.asset`, `Scriptable Objects/Danca das Nove Caudas.asset`, `Scriptable Objects/Legado das Nove Caudas.asset`.
- Caminhos conhecidos: dano, protecao, velocidade.
- Audio conhecido: `event:/Player/Dash`, `event:/Player/Heal`.

## Validacao Obrigatoria

- Testar dash, cura, passiva e caminhos em host e cliente.
- Confirmar que loop de cura para em fim de efeito e em despawn.
- Confirmar que eventos de audio passam por `ExoAudioService`.

## Pendencias

- Identificar prefab principal canonico entre variantes de Samurai.
- Registrar valores finais de dano, duracao, cooldown e escalonamento.
- Documentar dependencias de VFX por habilidade.
