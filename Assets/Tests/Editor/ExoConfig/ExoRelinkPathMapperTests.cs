using NUnit.Framework;
using ExoBeasts.ExoConfig.Core;

public class ExoRelinkPathMapperTests
{
    [Test]
    public void MapRelativePath_ReturnsNewModelNameWhenPathIsExactlyTheModelNode()
    {
        // Referencia que aponta para o proprio GameObject do modelo (ex.: um
        // campo apontando para o FBX inteiro, nao para algo dentro dele).
        Assert.That(ExoRelinkPathMapper.MapRelativePath("Samurai", "Samurai", "Samurai 2"), Is.EqualTo("Samurai 2"));
    }

    [Test]
    public void MapRelativePath_RemapsFirstSegmentAndPreservesRest()
    {
        // Referencia para algo DENTRO do modelo (ex.: um osso) - so o
        // primeiro segmento (nome do modelo) troca, o resto do caminho
        // (estrutura interna do FBX, que nao muda entre reexportacoes) fica
        // igual.
        Assert.That(
            ExoRelinkPathMapper.MapRelativePath("Samurai/Mesh/Bone01", "Samurai", "Samurai 2"),
            Is.EqualTo("Samurai 2/Mesh/Bone01"));
    }

    [Test]
    public void MapRelativePath_DoesNotAssumeAnyFixedFolderName()
    {
        // O bug corrigido nesta fase: o mecanismo antigo so remapeava
        // caminhos comecando literalmente com "Pivot/". Torre e Monstro
        // nunca tem "Pivot" - o modelo e filho direto do root. Este teste
        // prova que o novo mecanismo funciona com QUALQUER nome de no-modelo,
        // sem nenhuma referencia a "Pivot".
        Assert.That(
            ExoRelinkPathMapper.MapRelativePath("Aranha/Armature/Spine", "Aranha", "Aranha 2"),
            Is.EqualTo("Aranha 2/Armature/Spine"));
    }

    [Test]
    public void MapRelativePath_LeavesUnrelatedPathsUntouched()
    {
        // Um caminho que nao comeca pelo no-modelo (ex.: um objeto irmao do
        // modelo, como "CirculoSeletor" em ConfigureAsTower ou
        // "DamagePopupPosition" em ConfigureAsEnemy) nao deve ser alterado -
        // esses objetos existem igual nas duas hierarquias, por nome fixo,
        // sem relacao com o nome do FBX.
        Assert.That(
            ExoRelinkPathMapper.MapRelativePath("CirculoSeletor", "Samurai", "Samurai 2"),
            Is.EqualTo("CirculoSeletor"));
    }

    [Test]
    public void MapRelativePath_DoesNotMatchAsSubstringPrefix()
    {
        // "SamuraiExtra" nao deve ser tratado como "dentro" de "Samurai" so
        // porque comeca com a mesma sequencia de caracteres - o token de
        // comparacao inclui a barra ("Samurai/"), entao um nome de irmao que
        // por acaso comeca com o mesmo texto nao e confundido com um filho.
        Assert.That(
            ExoRelinkPathMapper.MapRelativePath("SamuraiExtra", "Samurai", "Samurai 2"),
            Is.EqualTo("SamuraiExtra"));
    }

    [TestCase(null, "Samurai")]
    [TestCase("", "Samurai")]
    public void MapRelativePath_ReturnsOrigPathWhenOrigModelNameMissing(string origModelName, string newModelName)
    {
        Assert.That(ExoRelinkPathMapper.MapRelativePath("Samurai/Mesh", origModelName, newModelName), Is.EqualTo("Samurai/Mesh"));
    }

    [TestCase(null, "Samurai")]
    [TestCase("Samurai", "")]
    public void MapRelativePath_ReturnsOrigPathWhenNewModelNameMissing(string origModelName, string newModelName)
    {
        Assert.That(ExoRelinkPathMapper.MapRelativePath("Samurai/Mesh", origModelName, newModelName), Is.EqualTo("Samurai/Mesh"));
    }

    [Test]
    public void MapRelativePath_ReturnsNullWhenOrigPathIsNull()
    {
        Assert.That(ExoRelinkPathMapper.MapRelativePath(null, "Samurai", "Samurai 2"), Is.Null);
    }

    [Test]
    public void MapRelativePath_ComparisonIsCultureInvariant()
    {
        // Mesma motivacao de ExoNaming.TowerBaseName (Fase 1): o resultado
        // nao pode depender de CultureInfo.CurrentCulture. "Águia" com A
        // maiusculo acentuado precisa comparar de forma ordinal/estavel.
        Assert.That(
            ExoRelinkPathMapper.MapRelativePath("Águia/Asa", "Águia", "Águia 2"),
            Is.EqualTo("Águia 2/Asa"));
    }
}
