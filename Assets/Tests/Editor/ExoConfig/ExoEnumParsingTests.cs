using System;
using NUnit.Framework;
using ExoBeasts.ExoConfig.Core;

public class ExoCategoryParserTests
{
    /// <summary>
    /// Regressao da Fase 2: ExoCategoryParser costumava manter um array
    /// hardcoded (AllCategories) espelhando ExoCategory a mao, que podia
    /// divergir do enum em silencio se alguem adicionasse um membro e
    /// esquecesse de atualizar o array. Corrigido derivando o array de
    /// Enum.GetValues(typeof(ExoCategory)) - ver o comentario em
    /// ExoEnumParsing.cs para a justificativa completa. Este teste enumera os
    /// membros de forma independente da implementacao (via Enum.GetValues,
    /// nao reimportando AllCategories) e confirma que todo membro atual
    /// bate ida-e-volta por TryParse(membro.ToString()); serve de
    /// documentacao executavel da propriedade que o fix garante por
    /// construcao, e pegaria uma regressao caso alguem reintroduza um array
    /// hardcoded no futuro e esqueca de um membro.
    /// </summary>
    [Test]
    public void TryParse_AllCurrentEnumMembersRoundTrip()
    {
        foreach (ExoCategory valor in (ExoCategory[])Enum.GetValues(typeof(ExoCategory)))
        {
            bool ok = ExoCategoryParser.TryParse(valor.ToString(), out ExoCategory categoria);
            Assert.That(ok, Is.True, "TryParse falhou para o membro " + valor);
            Assert.That(categoria, Is.EqualTo(valor));
        }
    }

    [TestCase("Personagens", ExoCategory.Personagens)]
    [TestCase("Monstros", ExoCategory.Monstros)]
    [TestCase("Environment", ExoCategory.Environment)]
    public void TryParse_ExactMemberName_ReturnsTrueWithValue(string valor, ExoCategory esperado)
    {
        bool ok = ExoCategoryParser.TryParse(valor, out ExoCategory categoria);
        Assert.That(ok, Is.True);
        Assert.That(categoria, Is.EqualTo(esperado));
    }

    [TestCase("personagens", ExoCategory.Personagens)]
    [TestCase("MONSTROS", ExoCategory.Monstros)]
    [TestCase("eNVIRONMENT", ExoCategory.Environment)]
    public void TryParse_IsCaseInsensitive(string valor, ExoCategory esperado)
    {
        bool ok = ExoCategoryParser.TryParse(valor, out ExoCategory categoria);
        Assert.That(ok, Is.True);
        Assert.That(categoria, Is.EqualTo(esperado));
    }

    [TestCase("")]
    [TestCase(null)]
    public void TryParse_NullOrEmpty_ReturnsFalseWithoutThrowing(string valor)
    {
        bool ok = ExoCategoryParser.TryParse(valor, out ExoCategory categoria);
        Assert.That(ok, Is.False);
        Assert.That(categoria, Is.EqualTo(default(ExoCategory)));
    }

    [TestCase("Personagem")]
    [TestCase("Desconhecido")]
    [TestCase("Personagens ")]
    public void TryParse_UnknownValue_ReturnsFalse(string valor)
    {
        bool ok = ExoCategoryParser.TryParse(valor, out ExoCategory categoria);
        Assert.That(ok, Is.False);
    }
}

public class ExoAssetTypeParserTests
{
    /// <summary>
    /// Mesma regressao/justificativa de
    /// ExoCategoryParserTests.TryParse_AllCurrentEnumMembersRoundTrip, agora
    /// para ExoAssetTypeParser/ExoAssetType.
    /// </summary>
    [Test]
    public void TryParse_AllCurrentEnumMembersRoundTrip()
    {
        foreach (ExoAssetType valor in (ExoAssetType[])Enum.GetValues(typeof(ExoAssetType)))
        {
            bool ok = ExoAssetTypeParser.TryParse(valor.ToString(), out ExoAssetType tipo);
            Assert.That(ok, Is.True, "TryParse falhou para o membro " + valor);
            Assert.That(tipo, Is.EqualTo(valor));
        }
    }

    [TestCase("Materiais", ExoAssetType.Materiais)]
    [TestCase("Modelos", ExoAssetType.Modelos)]
    [TestCase("Texturas", ExoAssetType.Texturas)]
    [TestCase("Prefabs", ExoAssetType.Prefabs)]
    [TestCase("Animacao", ExoAssetType.Animacao)]
    public void TryParse_ExactMemberName_ReturnsTrueWithValue(string valor, ExoAssetType esperado)
    {
        bool ok = ExoAssetTypeParser.TryParse(valor, out ExoAssetType tipo);
        Assert.That(ok, Is.True);
        Assert.That(tipo, Is.EqualTo(esperado));
    }

    [TestCase("materiais", ExoAssetType.Materiais)]
    [TestCase("MODELOS", ExoAssetType.Modelos)]
    [TestCase("aNIMACAO", ExoAssetType.Animacao)]
    public void TryParse_IsCaseInsensitive(string valor, ExoAssetType esperado)
    {
        bool ok = ExoAssetTypeParser.TryParse(valor, out ExoAssetType tipo);
        Assert.That(ok, Is.True);
        Assert.That(tipo, Is.EqualTo(esperado));
    }

    [TestCase("")]
    [TestCase(null)]
    public void TryParse_NullOrEmpty_ReturnsFalseWithoutThrowing(string valor)
    {
        bool ok = ExoAssetTypeParser.TryParse(valor, out ExoAssetType tipo);
        Assert.That(ok, Is.False);
        Assert.That(tipo, Is.EqualTo(default(ExoAssetType)));
    }

    [TestCase("Animação")]
    [TestCase("Desconhecido")]
    public void TryParse_UnknownValue_ReturnsFalse(string valor)
    {
        bool ok = ExoAssetTypeParser.TryParse(valor, out ExoAssetType tipo);
        Assert.That(ok, Is.False);
    }
}
