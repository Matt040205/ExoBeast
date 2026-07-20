using NUnit.Framework;
using ExoBeasts.ExoConfig.Core;

public class ExoFileIdPresenceCheckerTests
{
    // Amostra minima, mas realista, de YAML de prefab: um objeto definido
    // (ancora "&123456") que referencia outro objeto por fileID
    // ("someRef: {fileID: 987654}").
    private const string SampleYaml =
        "--- !u!114 &123456\n" +
        "MonoBehaviour:\n" +
        "  m_GameObject: {fileID: 0}\n" +
        "  someRef: {fileID: 987654}\n";

    [Test]
    public void ContainsFileId_FindsReferenceForm()
    {
        // "987654" so aparece como referencia ("fileID: 987654"), nao como
        // ancora de definicao - ainda assim deve ser encontrado.
        Assert.That(ExoFileIdPresenceChecker.ContainsFileId(SampleYaml, "987654"), Is.True);
    }

    [Test]
    public void ContainsFileId_FindsAnchorForm()
    {
        // "123456" so aparece como ancora de definicao ("&123456"), nunca
        // como "fileID: 123456" neste YAML de amostra - ainda assim deve ser
        // encontrado.
        Assert.That(ExoFileIdPresenceChecker.ContainsFileId(SampleYaml, "123456"), Is.True);
    }

    [Test]
    public void ContainsFileId_ReturnsFalseWhenAbsent()
    {
        Assert.That(ExoFileIdPresenceChecker.ContainsFileId(SampleYaml, "111111"), Is.False);
    }

    [Test]
    public void ContainsFileId_DoesNotMatchAsNumericPrefixOfReferenceForm()
    {
        // "98765" e prefixo de "987654" - "fileID: 98765" NAO deve "achar"
        // dentro de "fileID: 987654" (o digito seguinte, "4", desqualifica).
        Assert.That(ExoFileIdPresenceChecker.ContainsFileId(SampleYaml, "98765"), Is.False);
    }

    [Test]
    public void ContainsFileId_DoesNotMatchAsNumericPrefixOfAnchorForm()
    {
        // "12345" e prefixo de "123456" - "&12345" NAO deve "achar" dentro de
        // "&123456".
        Assert.That(ExoFileIdPresenceChecker.ContainsFileId(SampleYaml, "12345"), Is.False);
    }

    [Test]
    public void ContainsFileId_SupportsNegativeFileIds()
    {
        // fileIDs negativos sao comuns em componentes Unity - ex.: o
        // NetworkObject de Assets/Personagens/Player 1.prefab usa
        // "&-8535913011432277912" (confirmado nesta fase).
        string yaml = "--- !u!114 &-8535913011432277912\nMonoBehaviour:\n";
        Assert.That(ExoFileIdPresenceChecker.ContainsFileId(yaml, "-8535913011432277912"), Is.True);
    }

    [Test]
    public void ContainsFileId_FindsTokenNotAtEndOfString()
    {
        // Garante que o boundary check (caractere seguinte ao numero) nao
        // exige que o token esteja no fim da string - so que o proximo
        // caractere nao seja outro digito.
        string yaml = "a: {fileID: 42}\nb: {fileID: 43}\n";
        Assert.That(ExoFileIdPresenceChecker.ContainsFileId(yaml, "42"), Is.True);
        Assert.That(ExoFileIdPresenceChecker.ContainsFileId(yaml, "43"), Is.True);
    }

    [TestCase(null, "123")]
    [TestCase("", "123")]
    [TestCase("--- !u!114 &123", null)]
    [TestCase("--- !u!114 &123", "")]
    [TestCase(null, null)]
    [TestCase("", "")]
    public void ContainsFileId_ReturnsFalseForNullOrEmptyInputs(string yamlText, string fileId)
    {
        Assert.That(ExoFileIdPresenceChecker.ContainsFileId(yamlText, fileId), Is.False);
    }
}
