using NUnit.Framework;
using ExoBeasts.ExoConfig.Core;

public class ExoScriptableObjectReferenceParserTests
{
    // Amostra REAL, nao inventada: capturada de
    // Assets/Personagens/Ayame/DataScripableObjects/Ayame.asset nesta fase
    // (CharacterBase). "towerPrefab" aponta para um guid orfao
    // (fd0bbd1c417566a43800d83168a82c10 - achado documentado como pendente
    // de correcao na Fase 8, nao desta fase) - irrelevante para este parser,
    // que so extrai o fileID mecanicamente, sem julgar se a referencia e
    // valida.
    private const string RealCharacterBaseYaml =
        "  characterIcon: {fileID: 21300000, guid: 62e12a14edcae4841b8c9577a4116e14, type: 3}\n" +
        "  commanderPrefab: {fileID: 6275176270602792791, guid: 48cbccb27fbbd304397715bf47e3d361, type: 3}\n" +
        "  towerPrefab: {fileID: 5430613467821749649, guid: fd0bbd1c417566a43800d83168a82c10, type: 3}\n" +
        "  magazineSize: 15\n";

    // Amostra REAL, capturada de Assets/CoreScripts/Enemy/Aranha.asset nesta
    // fase (EnemyDataSO).
    private const string RealEnemyDataSoYaml =
        "  m_Name: Aranha\n" +
        "  m_EditorClassIdentifier: \n" +
        "  enemyPrefab: {fileID: 8676188461304220958, guid: e246350e14706304a82d941bf54faf0c, type: 3}\n" +
        "  enemyType: 0\n";

    [Test]
    public void ExtractFileId_FindsCommanderPrefabFromRealCharacterBase()
    {
        Assert.That(ExoScriptableObjectReferenceParser.ExtractFileId(RealCharacterBaseYaml, "commanderPrefab"), Is.EqualTo("6275176270602792791"));
    }

    [Test]
    public void ExtractFileId_FindsTowerPrefabFromRealCharacterBase()
    {
        // Prova que campos DIFERENTES no mesmo documento sao extraidos
        // independentemente - nao "gruda" no primeiro fileID do arquivo.
        Assert.That(ExoScriptableObjectReferenceParser.ExtractFileId(RealCharacterBaseYaml, "towerPrefab"), Is.EqualTo("5430613467821749649"));
    }

    [Test]
    public void ExtractFileId_FindsEnemyPrefabFromRealEnemyDataSo()
    {
        Assert.That(ExoScriptableObjectReferenceParser.ExtractFileId(RealEnemyDataSoYaml, "enemyPrefab"), Is.EqualTo("8676188461304220958"));
    }

    [Test]
    public void ExtractFileId_ReturnsNullWhenFieldNotPresent()
    {
        Assert.That(ExoScriptableObjectReferenceParser.ExtractFileId(RealCharacterBaseYaml, "enemyPrefab"), Is.Null);
    }

    [Test]
    public void ExtractFileId_ReturnsNullForUnassignedReference()
    {
        // fileID 0 e a forma padrao da Unity para "nenhuma referencia" - nao
        // e um fileID valido para procurar em lugar nenhum.
        string yaml = "  commanderPrefab: {fileID: 0}\n";
        Assert.That(ExoScriptableObjectReferenceParser.ExtractFileId(yaml, "commanderPrefab"), Is.Null);
    }

    [Test]
    public void ExtractFileId_DoesNotMatchFieldNameAsSuffix()
    {
        // Buscar "Prefab" nao deve "achar" dentro de "commanderPrefab: {...}"
        // - o campo tem que comecar exatamente no inicio da linha (apos so
        // espacos/tabs).
        Assert.That(ExoScriptableObjectReferenceParser.ExtractFileId(RealCharacterBaseYaml, "Prefab"), Is.Null);
    }

    [Test]
    public void ExtractFileId_SupportsNegativeFileIds()
    {
        string yaml = "  someRef: {fileID: -8535913011432277912, guid: abc, type: 3}\n";
        Assert.That(ExoScriptableObjectReferenceParser.ExtractFileId(yaml, "someRef"), Is.EqualTo("-8535913011432277912"));
    }

    [TestCase(null, "commanderPrefab")]
    [TestCase("", "commanderPrefab")]
    [TestCase(RealCharacterBaseYaml, null)]
    [TestCase(RealCharacterBaseYaml, "")]
    [TestCase(null, null)]
    public void ExtractFileId_ReturnsNullForNullOrEmptyInputs(string yamlText, string fieldName)
    {
        Assert.That(ExoScriptableObjectReferenceParser.ExtractFileId(yamlText, fieldName), Is.Null);
    }

    // Fase 7 - achado real (nao hipotese) durante a verificacao desta fase:
    // fileIDs "bem conhecidos" que a Unity atribui por convencao ao objeto
    // principal de um modelo importado se repetem entre GUIDs DIFERENTES com
    // frequencia real - confirmado ao vivo que o fileID 919132149155446097
    // aparece como raiz de modelo em pelo menos dois FBX distintos deste
    // projeto (tambem visivel nas amostras de YAML real de
    // ExoFileIdPresenceCheckerTests, capturadas na Fase 6, para dois guids
    // diferentes). ExtractGuid existe para que ValidateStep confirme o guid
    // ANTES do fileID, evitando esse falso positivo.

    [Test]
    public void ExtractGuid_FindsCommanderPrefabGuidFromRealCharacterBase()
    {
        Assert.That(ExoScriptableObjectReferenceParser.ExtractGuid(RealCharacterBaseYaml, "commanderPrefab"), Is.EqualTo("48cbccb27fbbd304397715bf47e3d361"));
    }

    [Test]
    public void ExtractGuid_FindsTowerPrefabGuidFromRealCharacterBase()
    {
        Assert.That(ExoScriptableObjectReferenceParser.ExtractGuid(RealCharacterBaseYaml, "towerPrefab"), Is.EqualTo("fd0bbd1c417566a43800d83168a82c10"));
    }

    [Test]
    public void ExtractGuid_FindsEnemyPrefabGuidFromRealEnemyDataSo()
    {
        Assert.That(ExoScriptableObjectReferenceParser.ExtractGuid(RealEnemyDataSoYaml, "enemyPrefab"), Is.EqualTo("e246350e14706304a82d941bf54faf0c"));
    }

    [Test]
    public void ExtractGuid_DistinguishesTwoReferencesThatShareTheSameFileIdButDifferentGuids()
    {
        // O cenario exato do achado desta fase: dois campos, fileIDs IGUAIS
        // (colisao real de convencao da Unity para raiz de modelo), guids
        // DIFERENTES (assets genuinamente distintos). ExtractFileId sozinho
        // nao distingue os dois; ExtractGuid e o que permite ValidateStep
        // perceber que sao referencias para arquivos diferentes.
        string yaml =
            "  enemyPrefab: {fileID: 919132149155446097, guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, type: 3}\n" +
            "  commanderPrefab: {fileID: 919132149155446097, guid: bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, type: 3}\n";

        Assert.That(ExoScriptableObjectReferenceParser.ExtractFileId(yaml, "enemyPrefab"),
            Is.EqualTo(ExoScriptableObjectReferenceParser.ExtractFileId(yaml, "commanderPrefab")),
            "pre-condicao do teste: os fileIDs devem ser identicos para provar que so o guid distingue.");
        Assert.That(ExoScriptableObjectReferenceParser.ExtractGuid(yaml, "enemyPrefab"), Is.EqualTo("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));
        Assert.That(ExoScriptableObjectReferenceParser.ExtractGuid(yaml, "commanderPrefab"), Is.EqualTo("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"));
    }

    [Test]
    public void ExtractGuid_ReturnsNullWhenFieldNotPresent()
    {
        Assert.That(ExoScriptableObjectReferenceParser.ExtractGuid(RealCharacterBaseYaml, "enemyPrefab"), Is.Null);
    }

    [TestCase(null, "commanderPrefab")]
    [TestCase("", "commanderPrefab")]
    [TestCase(RealCharacterBaseYaml, null)]
    [TestCase(RealCharacterBaseYaml, "")]
    [TestCase(null, null)]
    public void ExtractGuid_ReturnsNullForNullOrEmptyInputs(string yamlText, string fieldName)
    {
        Assert.That(ExoScriptableObjectReferenceParser.ExtractGuid(yamlText, fieldName), Is.Null);
    }
}
