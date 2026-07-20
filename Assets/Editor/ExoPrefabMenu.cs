using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System;
using ExoBeasts.ExoConfig.Core;

public class ExoPrefabMenu
{
    /// <summary>
    /// Ponto de entrada publico - assinatura preservada intacta pela Fase 4
    /// (o picker "Assets/Exo Prefabs/Organizar..." depende dela, ver
    /// AbrirOrganizarPicker mais abaixo). A partir da Fase 4, so monta o
    /// ExoBuildContext e delega para RunPipeline com DryRun=false; toda a
    /// logica de organizar/montar em si mora agora em
    /// Assets/Editor/ExoConfig/Pipeline/ (ExoBuildPipeline + os steps em
    /// Assets/Editor/ExoConfig/Pipeline/Steps/ - ResolvePathsStep,
    /// ImportAssetsStep, MaterialStep, BuildPrefabStep da Fase 4;
    /// AnimatorStep, NetworkRegistrationStep, ValidateStep acrescentados na
    /// Fase 7 - ver RunPipeline logo abaixo para a ordem e a justificativa).
    /// </summary>
    public static void ExecutarOrganizar(string categoria, string nome)
    {
        RunPipeline(categoria, nome, dryRun: false);
    }

    /// <summary>
    /// Nucleo comum de ExecutarOrganizar, com DryRun explicito. "internal"
    /// (nao private) pelo mesmo motivo de BuildPickerItems logo abaixo:
    /// existe para permitir exercitar o pipeline em modo diagnostico (ver
    /// Assets/Diretrizes_Multiagente.md - nunca mover assets reais do
    /// projeto so para testar) sem duplicar a montagem do ExoBuildPipeline
    /// em outro arquivo, inclusive por um script de diagnostico temporario
    /// no mesmo assembly implicito. A assinatura PUBLICA que o picker
    /// depende (ExecutarOrganizar(string, string)) fica intocada acima.
    ///
    /// Devolve o ExoBuildReport da execucao (nunca null) para quem chamar
    /// programaticamente poder inspecionar Messages/HasErrors/HasWarnings
    /// sem depender so do que foi parar no console. Devolve null apenas no
    /// caso defensivo de nada estar selecionado no Project (mesmo guard
    /// silencioso que ExecutarOrganizar sempre teve - no uso normal via
    /// menu, ValidarAbrirOrganizarPicker ja impede isso).
    ///
    /// Fase 7: acrescenta AnimatorStep, NetworkRegistrationStep e
    /// ValidateStep, NESSA ORDEM, depois de BuildPrefabStep. Justificativa da
    /// ordem (ver o comentario de cada step para o detalhe completo):
    ///   - AnimatorStep so pode rodar DEPOIS de BuildPrefabStep porque o
    ///     componente Animator so existe depois que o prefab e montado/salvo
    ///     (confirmado lendo ExoPrefabBuilder nesta fase, nao presumido).
    ///   - NetworkRegistrationStep tambem depende de BuiltPrefabPaths
    ///     (populado por BuildPrefabStep) e nao tem nenhuma dependencia de
    ///     dado em relacao a AnimatorStep - a ordem entre os dois nao afeta
    ///     corretude, mas "terminar de configurar o prefab (Animator) antes
    ///     de registra-lo para uso em rede" e a sequencia mais legivel.
    ///   - ValidateStep roda por ULTIMO deliberadamente: e um GATE de
    ///     verificacao, nao uma etapa de montagem - so faz sentido conferir o
    ///     estado final (fileID no YAML do prefab JA salvo, incluindo
    ///     qualquer efeito colateral dos steps anteriores) depois que tudo o
    ///     mais rodou.
    /// </summary>
    internal static ExoBuildReport RunPipeline(string categoria, string nome, bool dryRun)
    {
        UnityEngine.Object selected = Selection.activeObject;
        if (selected == null) return null;

        string sourcePath = AssetDatabase.GetAssetPath(selected);

        ExoBuildReport report = new ExoBuildReport();
        ExoBuildContext context = new ExoBuildContext(categoria, nome, sourcePath, dryRun, report);

        ExoBuildPipeline pipeline = new ExoBuildPipeline()
            .Add(new ResolvePathsStep())
            .Add(new ImportAssetsStep())
            .Add(new MaterialStep())
            .Add(new BuildPrefabStep())
            .Add(new AnimatorStep())
            .Add(new NetworkRegistrationStep())
            .Add(new ValidateStep());

        pipeline.Run(context);

        DumpReport(report);
        return report;
    }

    /// <summary>
    /// Despeja o ExoBuildReport no console Unity de uma vez, no fim da
    /// execucao, preservando a severidade de cada mensagem (Info -> Debug.Log,
    /// Warning -> Debug.LogWarning, Error -> Debug.LogError). Nenhum step do
    /// pipeline chama Debug.* diretamente (ver IExoBuildStep) - só este
    /// metodo, no lado do menu, decide como exibir o relatorio.
    /// </summary>
    private static void DumpReport(ExoBuildReport report)
    {
        foreach (ExoBuildMessage msg in report.Messages)
        {
            switch (msg.Severity)
            {
                case ExoBuildMessageSeverity.Error:
                    Debug.LogError("[ExoConfig] " + msg);
                    break;
                case ExoBuildMessageSeverity.Warning:
                    Debug.LogWarning("[ExoConfig] " + msg);
                    break;
                default:
                    Debug.Log("[ExoConfig] " + msg);
                    break;
            }
        }
    }

    public static ExoPrefabProfile LoadProfile(string categoria, string nome)
    {
        if (!ExoCategoryParser.TryParse(categoria, out ExoCategory categoriaEnum))
            return null;

        ExoToolConfig config = ExoToolConfig.Load();
        ExoToolConfigEntry entry = config?.FindEntry(categoriaEnum, nome);
        if (entry == null || string.IsNullOrEmpty(entry.ProfileAssetPath))
            return null;

        return AssetDatabase.LoadAssetAtPath<ExoPrefabProfile>(entry.ProfileAssetPath);
    }

    /// <summary>
    /// Validacao do item de menu unico "Assets/Exo Prefabs/Organizar..."
    /// (Fase 3 - substitui os N pares de [MenuItem] que
    /// Assets/Editor/ExoGeneratedMenus.cs gerava em disco). Mesma regra que
    /// o codegen usava para cada entidade: so habilita quando o objeto
    /// selecionado no Project e um .fbx.
    /// </summary>
    [MenuItem("Assets/Exo Prefabs/Organizar...", true)]
    private static bool ValidarAbrirOrganizarPicker()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        return !string.IsNullOrEmpty(path) && Path.GetExtension(path).ToLower() == ".fbx";
    }

    /// <summary>
    /// Item de menu unico que abre um picker (GenericMenu) com todas as
    /// entidades do ExoToolConfig, agrupadas por categoria - a substituicao
    /// da Fase 3 para os N [MenuItem] hardcoded que
    /// Assets/Editor/ExoGeneratedMenus.cs mantinha em disco (um par
    /// validate+execute por entidade, regenerado via GenerateMenus toda vez
    /// que a config mudava). GenericMenu le a config em tempo de execucao a
    /// cada clique: nao ha arquivo gerado para divergir do gerador, nem
    /// recompilacao necessaria quando uma entidade e adicionada/removida em
    /// ExoConfigWindow.
    ///
    /// "Organizar..." (com reticencias) segue a convencao do Editor da
    /// Unity para itens que abrem uma escolha adicional antes de executar
    /// (ex.: "File/Build Settings...") - sinaliza que clicar aqui nao
    /// executa a acao direto, primeiro pede qual entidade. O verbo
    /// "Organizar" (em vez de "Configurar") foi escolhido para bater com o
    /// nome do metodo que a acao de fato dispara (ExecutarOrganizar) e com o
    /// botao "Organizar v" que ja existe em ExoConfigWindow - evita
    /// confusao com o menu "Exo Config > Edit", que configura a FERRAMENTA
    /// (cadastro de entidades/pastas), nao organiza um asset selecionado.
    /// </summary>
    [MenuItem("Assets/Exo Prefabs/Organizar...", false, 20)]
    private static void AbrirOrganizarPicker()
    {
        List<ExoPickerItem> itens = BuildPickerItems();

        if (itens.Count == 0)
        {
            Debug.LogWarning("[ExoConfig] Nenhuma entidade cadastrada em ExoToolConfig. Abra o menu \"Exo Config > Edit\" e cadastre pelo menos uma entidade antes de organizar um FBX.");
            EditorUtility.DisplayDialog(
                "Exo Config",
                "Nenhuma entidade cadastrada no Exo Config.\n\nAbra o menu \"Exo Config > Edit\" e cadastre pelo menos uma entidade antes de organizar este FBX.",
                "OK");
            return;
        }

        GenericMenu menu = new GenericMenu();
        foreach (ExoPickerItem item in itens)
        {
            menu.AddItem(new GUIContent(item.MenuPath), false, () => ExecutarOrganizar(item.Categoria.ToString(), item.Nome));
        }
        menu.ShowAsContext();
    }

    /// <summary>
    /// Le o ExoToolConfig atual e monta a lista ordenada/agrupada de itens do
    /// picker (delegando a montagem em si para ExoPickerItemBuilder.BuildItems,
    /// no Core - puro, testado). Extraido como metodo separado (em vez de
    /// inline em AbrirOrganizarPicker) para poder ser exercitado - inclusive
    /// por um [MenuItem] de diagnostico temporario, como na Fase 3 - sem
    /// precisar renderizar o GenericMenu de fato (popup nativo do SO, fora
    /// do alcance de automacao/inspecao programatica).
    ///
    /// "internal" (nao private): visivel para outros scripts do mesmo
    /// assembly implicito (Assembly-CSharp-Editor, que cobre todo
    /// Assets/Editor/ sem asmdef proprio) - inclusive um script de
    /// diagnostico temporario usado para provar o picker em execucao.
    /// </summary>
    internal static List<ExoPickerItem> BuildPickerItems()
    {
        ExoToolConfig config = ExoToolConfig.Load();
        IEnumerable<ExoEntityDefinition> definicoes = config?.Entries.Select(e => e?.Definition);
        return ExoPickerItemBuilder.BuildItems(definicoes);
    }
}
