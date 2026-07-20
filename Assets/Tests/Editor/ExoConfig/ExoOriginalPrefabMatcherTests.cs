using NUnit.Framework;
using ExoBeasts.ExoConfig.Core;

public class ExoOriginalPrefabMatcherTests
{
    [Test]
    public void Classify_ReturnsExact_WhenCandidateNameFullyMatchesAfterCleaning()
    {
        Assert.That(ExoOriginalPrefabMatcher.Classify("Aranha", "Aranha 2"), Is.EqualTo(ExoOriginalPrefabMatchKind.Exact));
    }

    [Test]
    public void Classify_ReturnsFuzzy_WhenCandidateOnlyContainsSearchTerm()
    {
        Assert.That(ExoOriginalPrefabMatcher.Classify("Aranhaa", "Aranha 2"), Is.EqualTo(ExoOriginalPrefabMatchKind.Fuzzy));
    }

    [Test]
    public void Classify_DisambiguatesRealAguiaAguiaaCollision()
    {
        // Assets/Entidades/Inimigos/ tem tanto "Aguia.prefab" quanto
        // "Aguiaa.prefab" (confirmado no disco, Fase 6). Buscando por
        // "Aguia" (ou uma variante renomeada, ex.: "Aguia 3"), o candidato
        // exato ("Aguia") precisa ser distinguivel do candidato aproximado
        // ("Aguiaa") sem depender de qual foi encontrado primeiro por
        // AssetDatabase.FindAssets.
        Assert.That(ExoOriginalPrefabMatcher.Classify("Aguia", "Aguia"), Is.EqualTo(ExoOriginalPrefabMatchKind.Exact));
        Assert.That(ExoOriginalPrefabMatcher.Classify("Aguiaa", "Aguia"), Is.EqualTo(ExoOriginalPrefabMatchKind.Fuzzy));
    }

    [Test]
    public void Classify_DisambiguatesRealAranhaAranhaaCollision()
    {
        Assert.That(ExoOriginalPrefabMatcher.Classify("Aranha", "Aranha"), Is.EqualTo(ExoOriginalPrefabMatchKind.Exact));
        Assert.That(ExoOriginalPrefabMatcher.Classify("Aranhaa", "Aranha"), Is.EqualTo(ExoOriginalPrefabMatchKind.Fuzzy));
    }

    [Test]
    public void Classify_MatchesTowerPrefixedCandidateAsExact()
    {
        // Candidatos de Torre tem "Torreta" no nome de ARQUIVO real (ex.:
        // "TorretaSamurai.prefab", ver Assets/Personagens/Ayame/Prefabs/).
        // Limpando os dois lados (candidato E busca) com ExoNaming.CleanEntityName,
        // "TorretaSamurai" -> "Samurai" bate EXATO contra a busca
        // "TorretaSamurai 2" -> "Samurai" (FBX reimportado com sufixo).
        Assert.That(ExoOriginalPrefabMatcher.Classify("TorretaSamurai", "TorretaSamurai 2"), Is.EqualTo(ExoOriginalPrefabMatchKind.Exact));
    }

    [Test]
    public void Classify_ReturnsNone_WhenNoRelationBetweenNames()
    {
        Assert.That(ExoOriginalPrefabMatcher.Classify("Escorpiao", "Aranha"), Is.EqualTo(ExoOriginalPrefabMatchKind.None));
    }

    [Test]
    public void Classify_ExactMatchIsCaseInsensitive()
    {
        Assert.That(ExoOriginalPrefabMatcher.Classify("aranha", "Aranha"), Is.EqualTo(ExoOriginalPrefabMatchKind.Exact));
    }

    [TestCase(null, "Aranha")]
    [TestCase("", "Aranha")]
    [TestCase("Aranha", null)]
    [TestCase("Aranha", "")]
    [TestCase(null, null)]
    public void Classify_ReturnsNone_ForNullOrEmptyInputs(string candidate, string search)
    {
        Assert.That(ExoOriginalPrefabMatcher.Classify(candidate, search), Is.EqualTo(ExoOriginalPrefabMatchKind.None));
    }

    [Test]
    public void Classify_HandlesCompletoMarkerLikeRealEscorpiaoEntity()
    {
        // Assets/Entidades/Inimigos/EscorpiaoCompleto.prefab e
        // Assets/Entidades/Inimigos/Escorpião.prefab sao ambos reais - o
        // marcador "Completo" precisa ser removido dos dois lados para bater
        // exato quando fizer sentido.
        Assert.That(ExoOriginalPrefabMatcher.Classify("EscorpiaoCompleto", "Escorpiao"), Is.EqualTo(ExoOriginalPrefabMatchKind.Exact));
    }
}
