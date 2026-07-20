using System;
using System.Collections.Generic;
using NUnit.Framework;
using ExoBeasts.ExoConfig.Core;

public class ExoInputActionsResolverTests
{
    [Test]
    public void Resolve_AccentedExists_ReturnsAccentedPath()
    {
        string result = ExoInputActionsResolver.Resolve(p => p == ExoInputActionsResolver.AccentedPath);
        Assert.That(result, Is.EqualTo(ExoInputActionsResolver.AccentedPath));
    }

    [Test]
    public void Resolve_OnlyAsciiExists_ReturnsAsciiPath()
    {
        string result = ExoInputActionsResolver.Resolve(p => p == ExoInputActionsResolver.AsciiPath);
        Assert.That(result, Is.EqualTo(ExoInputActionsResolver.AsciiPath));
    }

    [Test]
    public void Resolve_BothExist_PrefersAccentedPath()
    {
        string result = ExoInputActionsResolver.Resolve(p => true);
        Assert.That(result, Is.EqualTo(ExoInputActionsResolver.AccentedPath));
    }

    [Test]
    public void Resolve_NeitherExists_ReturnsNull()
    {
        string result = ExoInputActionsResolver.Resolve(p => false);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Resolve_NullPredicate_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ExoInputActionsResolver.Resolve(null));
    }

    [Test]
    public void Resolve_QueriesAccentedPathBeforeAsciiPath()
    {
        // Prova a ORDEM de tentativa (acentuado primeiro), nao so o
        // resultado final - se a ordem for invertida por engano no futuro,
        // este teste falha mesmo que os outros (que so checam o path
        // retornado) continuem passando.
        List<string> queried = new List<string>();
        ExoInputActionsResolver.Resolve(p => { queried.Add(p); return false; });

        Assert.That(queried, Is.EqualTo(new[] { ExoInputActionsResolver.AccentedPath, ExoInputActionsResolver.AsciiPath }));
    }

    [Test]
    public void Resolve_ShortCircuits_DoesNotQueryAsciiWhenAccentedMatches()
    {
        List<string> queried = new List<string>();
        ExoInputActionsResolver.Resolve(p => { queried.Add(p); return p == ExoInputActionsResolver.AccentedPath; });

        Assert.That(queried, Is.EqualTo(new[] { ExoInputActionsResolver.AccentedPath }));
    }
}
