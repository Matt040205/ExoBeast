using System;
using System.Collections.Generic;
using NUnit.Framework;
using ExoBeasts.ExoConfig.Core;

public class ExoPickerItemBuilderTests
{
    [Test]
    public void BuildItems_NullInput_ReturnsEmptyListWithoutThrowing()
    {
        List<ExoPickerItem> itens = ExoPickerItemBuilder.BuildItems(null);
        Assert.That(itens, Is.Not.Null);
        Assert.That(itens, Is.Empty);
    }

    [Test]
    public void BuildItems_EmptyInput_ReturnsEmptyList()
    {
        List<ExoPickerItem> itens = ExoPickerItemBuilder.BuildItems(new List<ExoEntityDefinition>());
        Assert.That(itens, Is.Empty);
    }

    [Test]
    public void BuildItems_GroupsByCategoryInEnumDeclarationOrder_RegardlessOfInputOrder()
    {
        // Input deliberadamente fora de ordem (Environment primeiro,
        // Personagens por ultimo) para provar que o agrupamento usa a ORDEM
        // DE DECLARACAO do enum ExoCategory (Personagens, Monstros,
        // Environment - via Enum.GetValues), nao a ordem de "entidades".
        List<ExoEntityDefinition> definicoes = new List<ExoEntityDefinition>
        {
            new ExoEntityDefinition { Nome = "Ponte", Categoria = "Environment" },
            new ExoEntityDefinition { Nome = "Aranha", Categoria = "Monstros" },
            new ExoEntityDefinition { Nome = "Ayame", Categoria = "Personagens" },
        };

        List<ExoPickerItem> itens = ExoPickerItemBuilder.BuildItems(definicoes);

        Assert.That(itens.Count, Is.EqualTo(3));
        Assert.That(itens[0].Categoria, Is.EqualTo(ExoCategory.Personagens));
        Assert.That(itens[1].Categoria, Is.EqualTo(ExoCategory.Monstros));
        Assert.That(itens[2].Categoria, Is.EqualTo(ExoCategory.Environment));
    }

    [Test]
    public void BuildItems_SortsWithinCategoryOrdinally()
    {
        // Fora de ordem de proposito - mesmo criterio Ordinal que
        // ExoToolConfig.SortCategoria usa para "A-Z" (ver comentario em
        // ExoPickerItemBuilder.BuildItems).
        List<ExoEntityDefinition> definicoes = new List<ExoEntityDefinition>
        {
            new ExoEntityDefinition { Nome = "Sylvie", Categoria = "Personagens" },
            new ExoEntityDefinition { Nome = "Ayame", Categoria = "Personagens" },
            new ExoEntityDefinition { Nome = "Coral", Categoria = "Personagens" },
            new ExoEntityDefinition { Nome = "Brunhilde", Categoria = "Personagens" },
        };

        List<ExoPickerItem> itens = ExoPickerItemBuilder.BuildItems(definicoes);

        Assert.That(itens.Count, Is.EqualTo(4));
        Assert.That(itens[0].Nome, Is.EqualTo("Ayame"));
        Assert.That(itens[1].Nome, Is.EqualTo("Brunhilde"));
        Assert.That(itens[2].Nome, Is.EqualTo("Coral"));
        Assert.That(itens[3].Nome, Is.EqualTo("Sylvie"));
    }

    [Test]
    public void BuildItems_OrdinalSort_AccentedUppercaseSortsAfterAsciiAlphabet()
    {
        // Documenta (e trava) a mesma consequencia de StringComparison.Ordinal
        // que ExoConfigWindow/ExoToolConfig.SortCategoria "A-Z" ja tem: 'Á'
        // (U+00C1) e maior, em valor de code unit, que qualquer letra ASCII
        // maiuscula - entao "Águia" ordena DEPOIS de "Monstro" mesmo sendo
        // "alfabeticamente" anterior para um humano. Deliberado, nao um bug -
        // ver comentario em ExoPickerItemBuilder.BuildItems.
        List<ExoEntityDefinition> definicoes = new List<ExoEntityDefinition>
        {
            new ExoEntityDefinition { Nome = "Águia", Categoria = "Monstros" },
            new ExoEntityDefinition { Nome = "Aranha", Categoria = "Monstros" },
            new ExoEntityDefinition { Nome = "Capanga", Categoria = "Monstros" },
            new ExoEntityDefinition { Nome = "Escorpião", Categoria = "Monstros" },
            new ExoEntityDefinition { Nome = "Monstro", Categoria = "Monstros" },
        };

        List<ExoPickerItem> itens = ExoPickerItemBuilder.BuildItems(definicoes);

        Assert.That(itens.Count, Is.EqualTo(5));
        Assert.That(itens[0].Nome, Is.EqualTo("Aranha"));
        Assert.That(itens[1].Nome, Is.EqualTo("Capanga"));
        Assert.That(itens[2].Nome, Is.EqualTo("Escorpião"));
        Assert.That(itens[3].Nome, Is.EqualTo("Monstro"));
        Assert.That(itens[4].Nome, Is.EqualTo("Águia"));
    }

    [Test]
    public void BuildItems_PreservesAccentedNamesIntactInNome()
    {
        // Nome precisa chegar EXATAMENTE como cadastrado - e o valor que
        // ExoPrefabMenu.ExecutarOrganizar usa para achar a entrada via
        // ExoToolConfig.FindEntry (igualdade ordinal exata).
        List<ExoEntityDefinition> definicoes = new List<ExoEntityDefinition>
        {
            new ExoEntityDefinition { Nome = "Águia", Categoria = "Monstros" },
            new ExoEntityDefinition { Nome = "Escorpião", Categoria = "Monstros" },
        };

        List<ExoPickerItem> itens = ExoPickerItemBuilder.BuildItems(definicoes);

        Assert.That(itens.Exists(i => i.Nome == "Águia"), Is.True);
        Assert.That(itens.Exists(i => i.Nome == "Escorpião"), Is.True);
    }

    [Test]
    public void BuildItems_MenuPathIsCategoriaSlashNome_ForNamesWithoutSlash()
    {
        List<ExoEntityDefinition> definicoes = new List<ExoEntityDefinition>
        {
            new ExoEntityDefinition { Nome = "Ayame", Categoria = "Personagens" },
            new ExoEntityDefinition { Nome = "Águia", Categoria = "Monstros" },
            new ExoEntityDefinition { Nome = "Ponte", Categoria = "Environment" },
        };

        List<ExoPickerItem> itens = ExoPickerItemBuilder.BuildItems(definicoes);

        Assert.That(itens.Find(i => i.Nome == "Ayame").MenuPath, Is.EqualTo("Personagens/Ayame"));
        Assert.That(itens.Find(i => i.Nome == "Águia").MenuPath, Is.EqualTo("Monstros/Águia"));
        Assert.That(itens.Find(i => i.Nome == "Ponte").MenuPath, Is.EqualTo("Environment/Ponte"));
    }

    [Test]
    public void BuildItems_SanitizesSlashInNameForMenuPath_ButKeepsNomeIntact()
    {
        // UnityEditor.GenericMenu trata '/' como separador de submenu - um
        // nome de entidade com '/' nao pode vazar cru para MenuPath (ver
        // ExoPickerItemBuilder.SanitizeMenuSegment). "Nome" continua exato,
        // so MenuPath e afetado.
        List<ExoEntityDefinition> definicoes = new List<ExoEntityDefinition>
        {
            new ExoEntityDefinition { Nome = "Foo/Bar", Categoria = "Personagens" },
        };

        List<ExoPickerItem> itens = ExoPickerItemBuilder.BuildItems(definicoes);

        Assert.That(itens.Count, Is.EqualTo(1));
        Assert.That(itens[0].Nome, Is.EqualTo("Foo/Bar"));
        Assert.That(itens[0].MenuPath, Is.EqualTo("Personagens/Foo∕Bar"));
        Assert.That(itens[0].MenuPath, Does.Not.Contain("Foo/Bar"));
    }

    [Test]
    public void BuildItems_SkipsNullElementsAndEmptyOrNullNome()
    {
        List<ExoEntityDefinition> definicoes = new List<ExoEntityDefinition>
        {
            null,
            new ExoEntityDefinition { Nome = "", Categoria = "Personagens" },
            new ExoEntityDefinition { Nome = null, Categoria = "Personagens" },
            new ExoEntityDefinition { Nome = "Ayame", Categoria = "Personagens" },
        };

        List<ExoPickerItem> itens = ExoPickerItemBuilder.BuildItems(definicoes);

        Assert.That(itens.Count, Is.EqualTo(1));
        Assert.That(itens[0].Nome, Is.EqualTo("Ayame"));
    }

    [Test]
    public void BuildItems_SkipsUnknownCategoryAndReportsWarning()
    {
        List<ExoEntityDefinition> definicoes = new List<ExoEntityDefinition>
        {
            new ExoEntityDefinition { Nome = "X", Categoria = "NaoExisteEssaCategoria" },
            new ExoEntityDefinition { Nome = "Ayame", Categoria = "Personagens" },
        };

        ExoBuildReport report = new ExoBuildReport();
        List<ExoPickerItem> itens = ExoPickerItemBuilder.BuildItems(definicoes, report);

        Assert.That(itens.Count, Is.EqualTo(1));
        Assert.That(itens[0].Nome, Is.EqualTo("Ayame"));
        Assert.That(report.HasWarnings, Is.True);
        Assert.That(report.HasErrors, Is.False);
    }

    [Test]
    public void BuildItems_NoReportProvided_DoesNotThrowForUnknownCategory()
    {
        List<ExoEntityDefinition> definicoes = new List<ExoEntityDefinition>
        {
            new ExoEntityDefinition { Nome = "X", Categoria = "NaoExisteEssaCategoria" },
        };

        Assert.DoesNotThrow(() => ExoPickerItemBuilder.BuildItems(definicoes));
    }

    /// <summary>
    /// Espelha o conteudo real de Assets/Editor/ExoConfig/ExoToolConfig.asset
    /// (10 entidades semeadas na Fase 2: Ayame, Brunhilde, Coral, Sylvie /
    /// Águia, Aranha, Capanga, Escorpião, Monstro / Ponte) - se o asset for
    /// re-semeado com outras entidades no futuro, este teste precisa ser
    /// atualizado a mao (nao ha leitura do asset aqui: este assembly nao
    /// pode referenciar UnityEditor/AssetDatabase). Ordem esperada calculada
    /// por Enum.GetValues (Personagens, Monstros, Environment) + Ordinal
    /// dentro de cada categoria.
    /// </summary>
    [Test]
    public void BuildItems_RealSeedData_ProducesExpectedTenItemsInExpectedOrder()
    {
        List<ExoEntityDefinition> definicoes = new List<ExoEntityDefinition>
        {
            new ExoEntityDefinition { Nome = "Ayame", Categoria = "Personagens" },
            new ExoEntityDefinition { Nome = "Brunhilde", Categoria = "Personagens" },
            new ExoEntityDefinition { Nome = "Coral", Categoria = "Personagens" },
            new ExoEntityDefinition { Nome = "Sylvie", Categoria = "Personagens" },
            new ExoEntityDefinition { Nome = "Águia", Categoria = "Monstros" },
            new ExoEntityDefinition { Nome = "Aranha", Categoria = "Monstros" },
            new ExoEntityDefinition { Nome = "Capanga", Categoria = "Monstros" },
            new ExoEntityDefinition { Nome = "Escorpião", Categoria = "Monstros" },
            new ExoEntityDefinition { Nome = "Monstro", Categoria = "Monstros" },
            new ExoEntityDefinition { Nome = "Ponte", Categoria = "Environment" },
        };

        List<ExoPickerItem> itens = ExoPickerItemBuilder.BuildItems(definicoes);

        (ExoCategory, string)[] esperado =
        {
            (ExoCategory.Personagens, "Ayame"),
            (ExoCategory.Personagens, "Brunhilde"),
            (ExoCategory.Personagens, "Coral"),
            (ExoCategory.Personagens, "Sylvie"),
            (ExoCategory.Monstros, "Aranha"),
            (ExoCategory.Monstros, "Capanga"),
            (ExoCategory.Monstros, "Escorpião"),
            (ExoCategory.Monstros, "Monstro"),
            (ExoCategory.Monstros, "Águia"),
            (ExoCategory.Environment, "Ponte"),
        };

        Assert.That(itens.Count, Is.EqualTo(esperado.Length));
        for (int i = 0; i < esperado.Length; i++)
        {
            Assert.That(itens[i].Categoria, Is.EqualTo(esperado[i].Item1), "Categoria no indice " + i);
            Assert.That(itens[i].Nome, Is.EqualTo(esperado[i].Item2), "Nome no indice " + i);
        }
    }

    [Test]
    public void SanitizeMenuSegment_ReplacesSlashWithLookalikeChar()
    {
        Assert.That(ExoPickerItemBuilder.SanitizeMenuSegment("Foo/Bar"), Is.EqualTo("Foo∕Bar"));
    }

    [Test]
    public void SanitizeMenuSegment_NoSlash_ReturnsSameString()
    {
        Assert.That(ExoPickerItemBuilder.SanitizeMenuSegment("Águia"), Is.EqualTo("Águia"));
    }

    [TestCase("")]
    [TestCase(null)]
    public void SanitizeMenuSegment_NullOrEmpty_Throws(string nome)
    {
        Assert.Throws<ArgumentException>(() => ExoPickerItemBuilder.SanitizeMenuSegment(nome));
    }
}
