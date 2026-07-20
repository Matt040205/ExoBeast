using System;
using System.Collections.Generic;
using NUnit.Framework;
using ExoBeasts.ExoConfig.Core;

public class ExoPathResolverTests
{
    [TestCase(ExoCategory.Personagens, "Ayame", ExoAssetType.Materiais, "Assets/Personagens/Ayame/Materiais")]
    [TestCase(ExoCategory.Personagens, "Ayame", ExoAssetType.Modelos, "Assets/Personagens/Ayame/Modelos")]
    [TestCase(ExoCategory.Personagens, "Ayame", ExoAssetType.Texturas, "Assets/Personagens/Ayame/Texturas")]
    [TestCase(ExoCategory.Personagens, "Ayame", ExoAssetType.Prefabs, "Assets/Personagens/Ayame/Prefabs")]
    [TestCase(ExoCategory.Monstros, "Aranha", ExoAssetType.Materiais, "Assets/Entidades/Inimigos/Aranha/Materiais")]
    [TestCase(ExoCategory.Monstros, "Aranha", ExoAssetType.Modelos, "Assets/Entidades/Inimigos/Aranha/Modelos")]
    [TestCase(ExoCategory.Monstros, "Aranha", ExoAssetType.Texturas, "Assets/Entidades/Inimigos/Aranha/Texturas")]
    [TestCase(ExoCategory.Monstros, "Aranha", ExoAssetType.Prefabs, "Assets/Entidades/Inimigos/Aranha/Prefabs")]
    [TestCase(ExoCategory.Environment, "Ponte", ExoAssetType.Materiais, "Assets/Mapas/Ponte/Materiais")]
    [TestCase(ExoCategory.Environment, "Ponte", ExoAssetType.Modelos, "Assets/Mapas/Ponte/Modelos")]
    [TestCase(ExoCategory.Environment, "Ponte", ExoAssetType.Texturas, "Assets/Mapas/Ponte/Texturas")]
    [TestCase(ExoCategory.Environment, "Ponte", ExoAssetType.Prefabs, "Assets/Mapas/Ponte/Prefabs")]
    public void ResolveFolder_UsesConventionForAllThreeCategories(ExoCategory categoria, string nome, ExoAssetType tipo, string expected)
    {
        string result = ExoPathResolver.ResolveFolder(categoria, nome, tipo);
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void ResolveFolder_PersonagensAnimacaoUsesAccentedFolderName()
    {
        // "Animação" == "Animacao" com cedilha e til - pasta real hoje em
        // Assets/Personagens/Brunhilde/Animação (confirmada no repositorio).
        string expected = "Assets/Personagens/Ayame/Animação";
        string result = ExoPathResolver.ResolveFolder(ExoCategory.Personagens, "Ayame", ExoAssetType.Animacao);
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void ResolveFolder_MonstrosAnimacaoUsesConvention()
    {
        string expected = "Assets/Entidades/Inimigos/Aranha/Animação";
        string result = ExoPathResolver.ResolveFolder(ExoCategory.Monstros, "Aranha", ExoAssetType.Animacao);
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void ResolveFolder_EnvironmentDoesNotSupportAnimacao()
    {
        Assert.That(ExoPathResolver.SupportsAssetType(ExoCategory.Environment, ExoAssetType.Animacao), Is.False);
        Assert.Throws<InvalidOperationException>(() =>
            ExoPathResolver.ResolveFolder(ExoCategory.Environment, "Ponte", ExoAssetType.Animacao));
    }

    [Test]
    public void ResolveFolder_PersonagensAndMonstrosSupportAnimacao()
    {
        Assert.That(ExoPathResolver.SupportsAssetType(ExoCategory.Personagens, ExoAssetType.Animacao), Is.True);
        Assert.That(ExoPathResolver.SupportsAssetType(ExoCategory.Monstros, ExoAssetType.Animacao), Is.True);
    }

    [Test]
    public void ResolveFolder_OverrideWinsOverConvention()
    {
        Dictionary<ExoPathOverrideKey, string> overrides = new Dictionary<ExoPathOverrideKey, string>
        {
            { new ExoPathOverrideKey(ExoCategory.Personagens, "Ayame", ExoAssetType.Materiais), "Assets/CaminhoCustomizado/MateriaisDaAyame" }
        };

        string overridden = ExoPathResolver.ResolveFolder(ExoCategory.Personagens, "Ayame", ExoAssetType.Materiais, overrides);
        Assert.That(overridden, Is.EqualTo("Assets/CaminhoCustomizado/MateriaisDaAyame"));

        // Outro tipo da mesma entidade, sem override, continua na convencao.
        string conventional = ExoPathResolver.ResolveFolder(ExoCategory.Personagens, "Ayame", ExoAssetType.Modelos, overrides);
        Assert.That(conventional, Is.EqualTo("Assets/Personagens/Ayame/Modelos"));
    }

    [Test]
    public void ResolveFolder_OverrideKeyIsScopedToExactCategoriaNomeTipo()
    {
        Dictionary<ExoPathOverrideKey, string> overrides = new Dictionary<ExoPathOverrideKey, string>
        {
            { new ExoPathOverrideKey(ExoCategory.Personagens, "Ayame", ExoAssetType.Materiais), "Assets/Custom/Ayame_Mat" }
        };

        // Mesmo tipo, entidade diferente -> nao usa o override.
        string outraEntidade = ExoPathResolver.ResolveFolder(ExoCategory.Personagens, "Brunhilde", ExoAssetType.Materiais, overrides);
        Assert.That(outraEntidade, Is.EqualTo("Assets/Personagens/Brunhilde/Materiais"));

        // Mesmo nome, categoria diferente (nome escolhido de proposito para
        // coincidir com uma entidade Monstros real) -> nao usa o override.
        Dictionary<ExoPathOverrideKey, string> overridesPersonagem = new Dictionary<ExoPathOverrideKey, string>
        {
            { new ExoPathOverrideKey(ExoCategory.Personagens, "Aranha", ExoAssetType.Materiais), "Assets/Custom/PersonagemAranha" }
        };
        string comoMonstro = ExoPathResolver.ResolveFolder(ExoCategory.Monstros, "Aranha", ExoAssetType.Materiais, overridesPersonagem);
        Assert.That(comoMonstro, Is.EqualTo("Assets/Entidades/Inimigos/Aranha/Materiais"));
    }

    [Test]
    public void Normalize_ConvertsBackslashesToForwardSlashes()
    {
        Assert.That(ExoPathResolver.Normalize("Assets\\Personagens\\Ayame\\Materiais"), Is.EqualTo("Assets/Personagens/Ayame/Materiais"));
    }

    [Test]
    public void ResolveFolder_NormalizesBackslashesComingFromOverride()
    {
        Dictionary<ExoPathOverrideKey, string> overrides = new Dictionary<ExoPathOverrideKey, string>
        {
            { new ExoPathOverrideKey(ExoCategory.Environment, "Ponte", ExoAssetType.Prefabs), "Assets\\Custom\\Ponte\\Prefabs" }
        };

        string result = ExoPathResolver.ResolveFolder(ExoCategory.Environment, "Ponte", ExoAssetType.Prefabs, overrides);
        Assert.That(result, Is.EqualTo("Assets/Custom/Ponte/Prefabs"));
    }

    [Test]
    public void ResolveFolder_AccentedEntityNameMatchesRealMonstroFolder()
    {
        // "Escorpião" == "Escorpiao" com til - entidade Monstros real (ver
        // Assets/Editor/ExoConfig/ExoToolConfig.asset e Assets/Entidades/Inimigos/Escorpião.prefab).
        string nome = "Escorpião";
        string expected = "Assets/Entidades/Inimigos/Escorpião/Materiais";
        string result = ExoPathResolver.ResolveFolder(ExoCategory.Monstros, nome, ExoAssetType.Materiais);
        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase(null)]
    public void ResolveFolder_ThrowsForNullOrEmptyNome(string nome)
    {
        Assert.Throws<ArgumentException>(() => ExoPathResolver.ResolveFolder(ExoCategory.Personagens, nome, ExoAssetType.Materiais));
    }
}
