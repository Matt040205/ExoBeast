/// <summary>
/// Um passo do pipeline de organizacao/montagem de prefab (Fase 4 da
/// refatoracao Exo Config - ver ExoBuildPipeline para a orquestracao e
/// Assets/Editor/ExoConfig/Pipeline/Steps/ para os steps concretos desta
/// fase).
/// </summary>
public interface IExoBuildStep
{
    /// <summary>Nome curto do step, usado em mensagens de diagnostico do pipeline (ex.: qual step abortou a execucao - ver ExoBuildPipeline.Run).</summary>
    string Name { get; }

    /// <summary>
    /// Executa o step sobre o contexto compartilhado. Erros de negocio
    /// esperados (ex.: entidade nao encontrada em ExoToolConfig, shader
    /// ausente) devem ser reportados via context.Report.Error(...) - nunca
    /// Debug.LogError direto (quem exibe o relatorio e o chamador do
    /// pipeline, ver ExoPrefabMenu.ExecutarOrganizar) e nunca excecao para
    /// fluxo de controle esperado. Excecoes inesperadas (bug genuino) ainda
    /// podem escapar deste metodo; ExoBuildPipeline.Run garante que
    /// AssetDatabase.StopAssetEditing roda mesmo nesse caso (try/finally).
    /// </summary>
    void Execute(ExoBuildContext context);
}
