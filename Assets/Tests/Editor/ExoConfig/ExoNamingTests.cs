using System;
using NUnit.Framework;
using ExoBeasts.ExoConfig.Core;

public class ExoNamingTests
{
    [TestCase("samurai 3", "samurai 3.fbx")]
    [TestCase("Ayame", "Ayame.fbx")]
    public void ModelFileName_AppendsFbxExtension(string fbxName, string expected)
    {
        Assert.That(ExoNaming.ModelFileName(fbxName), Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase(null)]
    public void ModelFileName_ThrowsOnNullOrEmpty(string fbxName)
    {
        Assert.Throws<ArgumentException>(() => ExoNaming.ModelFileName(fbxName));
    }

    [TestCase("samurai 3", "samurai 3T.png")]
    [TestCase("Ayame", "AyameT.png")]
    public void TextureFileName_AppendsTPng(string fbxName, string expected)
    {
        Assert.That(ExoNaming.TextureFileName(fbxName), Is.EqualTo(expected));
    }

    [TestCase("samurai 3", "samurai 3_Mat.mat")]
    [TestCase("Ayame", "Ayame_Mat.mat")]
    public void MaterialFileName_AppendsMatSuffix(string fbxName, string expected)
    {
        Assert.That(ExoNaming.MaterialFileName(fbxName), Is.EqualTo(expected));
    }

    [TestCase("samurai 3", "samurai 3 Variant.prefab")]
    [TestCase("Ayame", "Ayame Variant.prefab")]
    public void CharacterPrefabFileName_AppendsVariantSuffix(string fbxName, string expected)
    {
        Assert.That(ExoNaming.CharacterPrefabFileName(fbxName), Is.EqualTo(expected));
    }

    [TestCase("Ponte", "Ponte.prefab")]
    [TestCase("Aranha", "Aranha.prefab")]
    public void GenericPrefabFileName_JustAppendsExtension(string fbxName, string expected)
    {
        Assert.That(ExoNaming.GenericPrefabFileName(fbxName), Is.EqualTo(expected));
    }

    [Test]
    public void GenericPrefabFileName_AccentedEntityNameMatchesRealMonstroPrefab()
    {
        // "Escorpião" == "Escorpiao" com til - ver
        // Assets/Entidades/Inimigos/Escorpião.prefab (arquivo real do repositorio).
        Assert.That(ExoNaming.GenericPrefabFileName("Escorpião"), Is.EqualTo("Escorpião.prefab"));
    }

    // Regra confirmada em Assets/Editor/ExoPrefabBuilder.cs, linha ~71:
    //   string towerName = "Torreta" + char.ToUpper(entityName[0]) + entityName.Substring(1);
    // Apenas a primeira letra do fbxName vira maiuscula; o resto (espacos,
    // digitos, demais letras) e copiado sem alteracao nenhuma. Note que isso
    // captura "Samurai" com S maiusculo, nao "samurai" minusculo.
    [TestCase("samurai 3", "TorretaSamurai 3")]
    [TestCase("Ayame", "TorretaAyame")]
    [TestCase("ayame", "TorretaAyame")]
    public void TowerBaseName_UppercasesOnlyTheFirstLetter(string fbxName, string expected)
    {
        Assert.That(ExoNaming.TowerBaseName(fbxName), Is.EqualTo(expected));
    }

    [Test]
    public void TowerBaseName_UppercasesAccentedFirstLetter()
    {
        // "águia" == "aguia" com "a" acentuado minusculo.
        // char.ToUpper('á') == 'Á' ("A" acentuado maiusculo).
        Assert.That(ExoNaming.TowerBaseName("águia"), Is.EqualTo("TorretaÁguia"));
    }

    [Test]
    public void TowerBaseName_UsesInvariantCultureForUppercasing()
    {
        // Fixa a decisao deliberada de TowerBaseName usar
        // char.ToUpperInvariant em vez de char.ToUpper: o resultado nao pode
        // depender da CultureInfo.CurrentCulture da thread que roda o teste
        // (ou a ferramenta, em producao). Em locale tr-TR, por exemplo,
        // char.ToUpper('i') produz 'İ' (I com ponto) em vez de 'I' - com
        // ToUpperInvariant, "inimigo" sempre vira "TorretaInimigo",
        // independente de maquina/regiao.
        Assert.That(ExoNaming.TowerBaseName("inimigo"), Is.EqualTo("TorretaInimigo"));
    }

    [TestCase("samurai 3", "TorretaSamurai 3.prefab")]
    [TestCase("Ayame", "TorretaAyame.prefab")]
    public void TowerPrefabFileName_AppendsPrefabExtension(string fbxName, string expected)
    {
        Assert.That(ExoNaming.TowerPrefabFileName(fbxName), Is.EqualTo(expected));
    }

    [TestCase("samurai 3", "samurai")]
    [TestCase("TorretaSamurai", "Samurai")]
    [TestCase("EscorpiaoCompleto", "Escorpiao")]
    public void CleanEntityName_RemovesMarkersAndTrailingDigits(string entityName, string expected)
    {
        Assert.That(ExoNaming.CleanEntityName(entityName), Is.EqualTo(expected));
    }

    [Test]
    public void CleanEntityName_DoesNotDestroyAccentedLetters()
    {
        // "Águia" == "Aguia" com "A" acentuado maiusculo (entidade Monstros
        // real, ver Assets/Editor/ExoConfig/ExoToolConfig.asset).
        Assert.That(ExoNaming.CleanEntityName("Águia"), Is.EqualTo("Águia"));

        // "Escorpião" == "Escorpiao" com til (entidade Monstros real).
        Assert.That(ExoNaming.CleanEntityName("Escorpião"), Is.EqualTo("Escorpião"));
    }

    [Test]
    public void CleanEntityName_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => ExoNaming.CleanEntityName(null));
    }

    [Test]
    public void CleanEntityName_AllowsEmptyString()
    {
        Assert.That(ExoNaming.CleanEntityName(""), Is.EqualTo(""));
    }

    [TestCase("")]
    [TestCase(null)]
    public void TextureFileName_ThrowsOnNullOrEmpty(string fbxName)
    {
        Assert.Throws<ArgumentException>(() => ExoNaming.TextureFileName(fbxName));
    }

    // Fase 7 (AnimatorStep): confirmado contra o unico Animator Controller
    // real do projeto - Assets/Personagens/Ayame/Animação/AyameAnimator.controller.
    [TestCase("Ayame", "AyameAnimator.controller")]
    [TestCase("Águia", "ÁguiaAnimator.controller")]
    public void AnimatorControllerFileName_AppendsAnimatorControllerSuffix(string nome, string expected)
    {
        Assert.That(ExoNaming.AnimatorControllerFileName(nome), Is.EqualTo(expected));
    }

    [Test]
    public void AnimatorControllerFileName_UsesEntityNameNotFbxName()
    {
        // Divergencia deliberada dos demais metodos desta classe: ao
        // contrario de ModelFileName/TextureFileName/etc. (que recebem o
        // nome TRANSIENTE do arquivo FBX sendo importado, ex.: "samurai 3"),
        // AnimatorControllerFileName recebe o nome ESTAVEL da entidade (ex.:
        // "Ayame"). Nao ha "samurai 3Animator.controller" nem
        // "SamuraiAnimator.controller" em lugar nenhum do projeto - so
        // "AyameAnimator.controller", nomeado a partir do nome cadastrado em
        // ExoToolConfig, nao do FBX de origem daquela execucao especifica.
        Assert.That(ExoNaming.AnimatorControllerFileName("Ayame"), Is.EqualTo("AyameAnimator.controller"));
        Assert.That(ExoNaming.AnimatorControllerFileName("samurai 3"), Is.EqualTo("samurai 3Animator.controller"));
    }

    [TestCase("")]
    [TestCase(null)]
    public void AnimatorControllerFileName_ThrowsOnNullOrEmpty(string nome)
    {
        Assert.Throws<ArgumentException>(() => ExoNaming.AnimatorControllerFileName(nome));
    }
}
