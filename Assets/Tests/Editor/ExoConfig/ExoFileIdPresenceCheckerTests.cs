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

    // Fase 6, item 4 do escopo: as duas amostras abaixo NAO sao YAML
    // inventado - sao trechos REAIS, capturados via
    // File.ReadAllText/script de scratch, de prefabs de Torre e Monstro
    // gerados de verdade pelo pipeline corrigido nesta fase
    // (ExoPrefabBuilder.ConfigureAsTower/ConfigureAsEnemy +
    // CopySerializedValuesAndRelink + FindModelChild + ExoRelinkPathMapper),
    // no cenario que antes quebrava (FBX renomeado entre duas execucoes,
    // ex.: "ScratchTower" -> "ScratchTower 2"). Cada "&<fileId> stripped"
    // e a forma como Unity serializa, dentro do YAML do prefab CONTAINER,
    // uma referencia para um objeto que vive DENTRO de um NESTED PREFAB
    // (aqui, o modelo/FBX relinkado) - exatamente o tipo de referencia que
    // a regra duravel do projeto ("fileID precisa aparecer literalmente no
    // YAML, nao so resolver via AssetDatabase no Editor") existe para
    // proteger. Provam que ExoFileIdPresenceChecker se aplica ao cenario
    // real que esta fase introduz - nao so a amostras sinteticas.
    [Test]
    public void ContainsFileId_FindsRealRelinkedTowerModelReferenceFromFase6Scratch()
    {
        string yaml =
            "fileID: 0}\n" +
            "--- !u!1 &5761318791579385680 stripped\n" +
            "GameObject:\n" +
            "  m_CorrespondingSourceObject: {fileID: 919132149155446097, guid: bb9169f4834045c4c945cd8b939d21aa, type: 3}\n" +
            "  m_PrefabInstance: {fileID: 4842839901715169793}\n" +
            "  m_PrefabAsset: {fileID: 0}\n" +
            "--- !u!95 &6863327163324087805\n" +
            "Animator:\n" +
            "  seriali";

        Assert.That(ExoFileIdPresenceChecker.ContainsFileId(yaml, "5761318791579385680"), Is.True);
    }

    [Test]
    public void ContainsFileId_FindsRealRelinkedEnemyModelReferenceFromFase6Scratch()
    {
        string yaml =
            ", type: 3}\n" +
            "--- !u!1 &554868142279659221 stripped\n" +
            "GameObject:\n" +
            "  m_CorrespondingSourceObject: {fileID: 919132149155446097, guid: 0e48889b6bcce3c48b0920468ec675a2, type: 3}\n" +
            "  m_PrefabInstance: {fileID: 824757361428116356}\n" +
            "  m_PrefabAsset: {fileID: 0}\n" +
            "--- !u!4 &934665208884024431 stripped\n" +
            "Transform:\n" +
            "  m";

        Assert.That(ExoFileIdPresenceChecker.ContainsFileId(yaml, "554868142279659221"), Is.True);
    }
}
