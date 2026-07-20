using System;
using System.Collections.Generic;

namespace ExoBeasts.ExoConfig.Core
{
    /// <summary>
    /// Tipos de asset organizados por entidade. "Animacao" so se aplica as
    /// categorias Personagens e Monstros - a convencao de Environment nao tem
    /// pasta de animacao (ver ExoPathResolver.SupportsAssetType).
    /// </summary>
    public enum ExoAssetType
    {
        Materiais,
        Modelos,
        Texturas,
        Prefabs,
        Animacao
    }

    /// <summary>
    /// Chave de override: identifica uma pasta customizada para uma combinacao
    /// especifica de categoria + nome de entidade + tipo de asset, sobrepondo a
    /// convencao padrao de ExoPathResolver.ResolveFolder.
    ///
    /// Equivalente puro ao prefixo de chave usado hoje no EditorPrefs (ex.:
    /// "Personagens_Ayame_Mat", ver ExoConfigWindow.DrawEntityConfig), so que
    /// injetado pelo chamador em vez de lido do registro do Windows.
    /// </summary>
    public readonly struct ExoPathOverrideKey : IEquatable<ExoPathOverrideKey>
    {
        public ExoCategory Categoria { get; }
        public string Nome { get; }
        public ExoAssetType Tipo { get; }

        public ExoPathOverrideKey(ExoCategory categoria, string nome, ExoAssetType tipo)
        {
            Categoria = categoria;
            Nome = nome ?? string.Empty;
            Tipo = tipo;
        }

        public bool Equals(ExoPathOverrideKey other)
        {
            return Categoria == other.Categoria
                && Tipo == other.Tipo
                && string.Equals(Nome, other.Nome, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ExoPathOverrideKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + Categoria.GetHashCode();
                hash = (hash * 31) + Tipo.GetHashCode();
                hash = (hash * 31) + (Nome != null ? Nome.GetHashCode() : 0);
                return hash;
            }
        }
    }

    /// <summary>
    /// Resolve pastas de destino a partir de (categoria, nome, tipo de asset),
    /// seguindo a convencao real da ferramenta Exo Config.
    ///
    /// Funcao pura: nao le EditorPrefs, nao usa UnityEngine/UnityEditor (o asmdef
    /// deste assembly tem noEngineReferences=true, entao isso e garantido em
    /// tempo de compilacao) e nao acessa disco. Overrides sao injetados pelo
    /// chamador atraves do parametro "overrides" - quem le o EditorPrefs de fato
    /// vive em uma camada acima, fora deste assembly.
    ///
    /// Convencao (fonte: registro EditorPrefs do dev anterior, decodificada
    /// manualmente porque a config esta vazia nesta maquina e o unico registro
    /// sobrevivente no repositorio - Assets/Editor/ExoGeneratedMenus.cs - guarda
    /// so nomes de entidade, nao caminhos de pasta):
    ///
    ///   Personagens  vira Assets/Personagens/{nome}        (Materiais, Modelos, Texturas, Prefabs, Animacao)
    ///   Monstros     vira Assets/Entidades/Inimigos/{nome} (Materiais, Modelos, Texturas, Prefabs)
    ///   Environment  vira Assets/Mapas/{nome}               (Materiais, Modelos, Texturas, Prefabs)
    ///
    /// Confirmado batendo com pastas reais do projeto: Assets/Personagens/Brunhilde/
    /// tem Materiais, Modelos, Texturas, Prefabs e Animacao; Assets/Mapas/Futuro/
    /// tem Materiais, Modelos, Texturas, Prefabs (sem Animacao).
    /// </summary>
    public static class ExoPathResolver
    {
        public static string GetCategoryRoot(ExoCategory categoria)
        {
            switch (categoria)
            {
                case ExoCategory.Personagens: return "Assets/Personagens";
                case ExoCategory.Monstros: return "Assets/Entidades/Inimigos";
                case ExoCategory.Environment: return "Assets/Mapas";
                default:
                    throw new ArgumentOutOfRangeException(nameof(categoria), categoria, "Categoria Exo Config desconhecida.");
            }
        }

        public static string GetEntityRoot(ExoCategory categoria, string nome)
        {
            RequireNome(nome);
            return Normalize(GetCategoryRoot(categoria) + "/" + nome);
        }

        public static string GetSubfolderName(ExoAssetType tipo)
        {
            switch (tipo)
            {
                case ExoAssetType.Materiais: return "Materiais";
                case ExoAssetType.Modelos: return "Modelos";
                case ExoAssetType.Texturas: return "Texturas";
                case ExoAssetType.Prefabs: return "Prefabs";
                case ExoAssetType.Animacao: return "Animação";
                default:
                    throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de asset Exo Config desconhecido.");
            }
        }

        /// <summary>
        /// Animacao so existe para Personagens e Monstros. Environment nao tem
        /// essa subpasta na convencao real do projeto (confirmado: nenhum mapa em
        /// Assets/Mapas tem pasta de animacao hoje).
        /// </summary>
        public static bool SupportsAssetType(ExoCategory categoria, ExoAssetType tipo)
        {
            if (tipo == ExoAssetType.Animacao)
                return categoria == ExoCategory.Personagens || categoria == ExoCategory.Monstros;
            return true;
        }

        /// <summary>
        /// Resolve a pasta de destino para (categoria, nome, tipo). Um override
        /// presente em "overrides" para a chave exata (categoria, nome, tipo) tem
        /// precedencia total sobre a convencao. Passe null (ou omita) quando nao
        /// houver overrides.
        /// </summary>
        public static string ResolveFolder(
            ExoCategory categoria,
            string nome,
            ExoAssetType tipo,
            IReadOnlyDictionary<ExoPathOverrideKey, string> overrides = null)
        {
            RequireNome(nome);

            if (!SupportsAssetType(categoria, tipo))
                throw new InvalidOperationException("[ExoConfig] " + tipo + " nao se aplica a categoria " + categoria + ".");

            if (overrides != null)
            {
                ExoPathOverrideKey key = new ExoPathOverrideKey(categoria, nome, tipo);
                if (overrides.TryGetValue(key, out string overridden) && !string.IsNullOrEmpty(overridden))
                    return Normalize(overridden);
            }

            return Normalize(GetEntityRoot(categoria, nome) + "/" + GetSubfolderName(tipo));
        }

        /// <summary>
        /// Normaliza separadores de caminho para '/'. Espelha o padrao ja usado em
        /// ExoPrefabBuilder/ExoPrefabMenu (Replace de barra invertida por barra normal).
        /// </summary>
        public static string Normalize(string path)
        {
            return path == null ? null : path.Replace("\\", "/");
        }

        private static void RequireNome(string nome)
        {
            if (string.IsNullOrEmpty(nome))
                throw new ArgumentException("[ExoConfig] nome da entidade nao pode ser nulo ou vazio.", nameof(nome));
        }
    }
}
