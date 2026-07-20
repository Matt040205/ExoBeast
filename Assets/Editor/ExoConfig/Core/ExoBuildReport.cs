using System;
using System.Collections.Generic;

namespace ExoBeasts.ExoConfig.Core
{
    /// <summary>
    /// Nivel de severidade de uma mensagem de ExoBuildReport.
    /// </summary>
    public enum ExoBuildMessageSeverity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Uma entrada individual do relatorio: severidade + mensagem + contexto
    /// opcional (ex.: caminho do asset ou nome da entidade envolvida).
    /// </summary>
    public readonly struct ExoBuildMessage
    {
        public ExoBuildMessageSeverity Severity { get; }
        public string Message { get; }
        public string Context { get; }

        public ExoBuildMessage(ExoBuildMessageSeverity severity, string message, string context)
        {
            Severity = severity;
            Message = message ?? string.Empty;
            Context = context ?? string.Empty;
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Context)
                ? "[" + Severity + "] " + Message
                : "[" + Severity + "] " + Message + " (" + Context + ")";
        }
    }

    /// <summary>
    /// Coleta estruturada de mensagens Info/Warning/Error, com contexto opcional,
    /// para substituir os Debug.Log/LogWarning/LogError espalhados hoje em
    /// ExoPrefabBuilder e ExoPrefabMenu nas proximas fases da refatoracao.
    ///
    /// Nao chama Debug.* nem nenhuma API de engine: quem consumir o relatorio
    /// decide como exibir as mensagens (console, janela do Exo Config, arquivo de
    /// log, etc.). Isso e garantido em tempo de compilacao pelo
    /// noEngineReferences=true do asmdef deste assembly.
    /// </summary>
    public sealed class ExoBuildReport
    {
        private readonly List<ExoBuildMessage> _messages = new List<ExoBuildMessage>();

        public IReadOnlyList<ExoBuildMessage> Messages => _messages;

        public bool HasErrors { get; private set; }

        public bool HasWarnings { get; private set; }

        public void Info(string message, string context = null)
        {
            Add(ExoBuildMessageSeverity.Info, message, context);
        }

        public void Warning(string message, string context = null)
        {
            Add(ExoBuildMessageSeverity.Warning, message, context);
        }

        public void Error(string message, string context = null)
        {
            Add(ExoBuildMessageSeverity.Error, message, context);
        }

        public void Add(ExoBuildMessageSeverity severity, string message, string context = null)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            _messages.Add(new ExoBuildMessage(severity, message, context));

            if (severity == ExoBuildMessageSeverity.Error) HasErrors = true;
            else if (severity == ExoBuildMessageSeverity.Warning) HasWarnings = true;
        }

        public void Clear()
        {
            _messages.Clear();
            HasErrors = false;
            HasWarnings = false;
        }
    }
}
