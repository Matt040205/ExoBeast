using System;
using System.Collections.Generic;
using UnityEditor;

/// <summary>
/// Executa uma sequencia de IExoBuildStep sobre um ExoBuildContext
/// compartilhado, na ordem em que foram adicionados (via Add - fluent),
/// parando no primeiro step que deixar context.Report.HasErrors true (nao
/// tenta compensar nem rodar steps seguintes sobre um estado que ja falhou -
/// ex.: nao monta prefab se o material nao pode ser criado).
///
/// Start/StopAssetEditing em try/finally: sem isso, uma excecao inesperada no
/// meio de um step deixaria o AssetDatabase em modo "batch" (StartAssetEditing)
/// para sempre, sem o StopAssetEditing correspondente - import/refresh de
/// asset fica com comportamento estranho ate o proximo dominio reload. Isso
/// NAO existia no ExoPrefabMenu.ExecutarOrganizar de antes da Fase 4 (nao
/// usava Start/StopAssetEditing nenhum); usar o par aqui tambem e uma
/// melhoria de performance incidental - Unity recomenda esse par
/// exatamente para ferramentas que fazem varias operacoes de asset em
/// sequencia (mover FBX, mover textura, criar material, criar prefab),
/// evitando reimport redundante entre cada uma.
/// </summary>
public sealed class ExoBuildPipeline
{
    private readonly List<IExoBuildStep> _steps = new List<IExoBuildStep>();

    public IReadOnlyList<IExoBuildStep> Steps => _steps;

    public ExoBuildPipeline Add(IExoBuildStep step)
    {
        if (step == null)
            throw new ArgumentNullException(nameof(step));

        _steps.Add(step);
        return this;
    }

    public void Run(ExoBuildContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (IExoBuildStep step in _steps)
            {
                step.Execute(context);

                if (context.Report.HasErrors)
                {
                    context.Report.Info("Pipeline interrompido: step \"" + step.Name + "\" reportou erro.");
                    break;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
    }
}
