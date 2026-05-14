using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Metadata;

namespace NodiumGraph.Controls;

/// <summary>
/// IDataTemplate variant that pairs a per-CLR-type visual template with declarative port
/// topology. On XAML load, registers <see cref="DataType"/> → <see cref="Ports"/> into
/// <see cref="NodePortRegistry"/> when both are populated.
/// </summary>
/// <remarks>
/// <para>
/// Use inside <c>&lt;Window.DataTemplates&gt;</c> (or any <c>DataTemplates</c> collection) the
/// same way you would use a <c>&lt;DataTemplate&gt;</c>, but with a nested
/// <c>&lt;NodeTemplate.Ports&gt;</c> element containing <see cref="PortDefinition"/> entries.
/// </para>
/// <para>
/// <see cref="Match"/> uses <b>exact type identity</b> — not <c>IsInstanceOfType</c>. This
/// keeps the visual selection aligned with <see cref="NodePortRegistry"/>'s exact-type
/// lookup so port topology and visual cannot drift apart.
/// </para>
/// <para>
/// Registration is idempotent and fires on the first of <c>ISupportInitialize.EndInit</c>
/// (invoked by the XAML loader after all properties are set) or <see cref="Match"/>/<see cref="Build"/>
/// (defensive fallback). A template with <see cref="DataType"/> == null or an empty
/// <see cref="Ports"/> list does not register — it remains a visual-only template.
/// </para>
/// </remarks>
public sealed class NodeTemplate : IDataTemplate, ISupportInitialize
{
    private bool _registered;

    /// <summary>The exact CLR type this template targets.</summary>
    public Type? DataType { get; set; }

    /// <summary>Declarative port topology applied to nodes of <see cref="DataType"/>.</summary>
    public IList<PortDefinition> Ports { get; } = new List<PortDefinition>();

    /// <summary>The visual produced by <see cref="Build"/>. Populated from the XAML content slot.</summary>
    [Content]
    [TemplateContent(TemplateResultType = typeof(Control))]
    public object? Content { get; set; }

    /// <inheritdoc />
    public bool Match(object? data)
    {
        // Defensive registration on first Match — works whether ISupportInitialize.EndInit
        // is invoked by the XAML loader or not. EnsureRegistered() is idempotent.
        EnsureRegistered();
        return data is not null && DataType == data.GetType();
    }

    /// <inheritdoc />
    public Control? Build(object? param)
    {
        EnsureRegistered();
        return Content is null ? null : TemplateContent.Load<Control>(Content)?.Result;
    }

    void ISupportInitialize.BeginInit() { }
    void ISupportInitialize.EndInit() => EnsureRegistered();

    private void EnsureRegistered()
    {
        if (_registered) return;
        if (DataType is null) return;
        if (Ports.Count == 0) return;
        NodePortRegistry.Register(this);
        _registered = true;
    }
}
