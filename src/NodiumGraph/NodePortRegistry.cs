using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using NodiumGraph.Controls;
using NodiumGraph.Model;

namespace NodiumGraph;

/// <summary>
/// Static, process-wide registry mapping a concrete <see cref="Node"/> subtype to its
/// declared port topology. Populated by NodeTemplate at XAML parse time and consulted by
/// <see cref="Node.PortProvider"/>/<c>Node.Ports</c> on first read.
/// </summary>
public static class NodePortRegistry
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<PortSpec>> _store = new();
    private static readonly object _writeLock = new();

    public static void Register(Type nodeType, IEnumerable<PortDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(nodeType);
        ArgumentNullException.ThrowIfNull(definitions);
        if (!typeof(Node).IsAssignableFrom(nodeType))
            throw new ArgumentException($"{nodeType.FullName} is not assignable to {nameof(Node)}.", nameof(nodeType));

        var snapshot = BuildSnapshot(definitions);

        lock (_writeLock)
        {
            if (_store.TryGetValue(nodeType, out var existing))
            {
                if (!StructurallyEqual(existing, snapshot))
                    throw new InvalidOperationException(BuildConflictMessage(nodeType, existing, snapshot));
                return;
            }
            _store.TryAdd(nodeType, snapshot);
        }
    }

    /// <summary>
    /// Convenience overload used by <see cref="Controls.NodeTemplate"/>. Equivalent to
    /// <c>Register(template.DataType, template.Ports)</c> with null checks.
    /// </summary>
    public static void Register(Controls.NodeTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (template.DataType is null)
            throw new ArgumentException("NodeTemplate.DataType must be set before registration.", nameof(template));
        Register(template.DataType, template.Ports);
    }

    public static bool TryGet(Type nodeType, out IReadOnlyList<PortSpec> snapshot)
    {
        if (_store.TryGetValue(nodeType, out var stored))
        {
            snapshot = stored;
            return true;
        }
        snapshot = Array.Empty<PortSpec>();
        return false;
    }

    public static void Clear()
    {
        lock (_writeLock) _store.Clear();
    }

    private static IReadOnlyList<PortSpec> BuildSnapshot(IEnumerable<PortDefinition> definitions)
    {
        var specs = new List<PortSpec>();
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var d in definitions)
        {
            ArgumentNullException.ThrowIfNull(d, nameof(definitions));
            if (string.IsNullOrEmpty(d.Name))
                throw new ArgumentException("PortDefinition.Name must be non-null and non-empty.", nameof(definitions));
            if (!names.Add(d.Name))
                throw new ArgumentException($"Duplicate port name '{d.Name}'.", nameof(definitions));
            if (double.IsNaN(d.Fraction) || d.Fraction < 0.0 || d.Fraction > 1.0)
                throw new ArgumentOutOfRangeException(nameof(definitions), $"Fraction {d.Fraction} for '{d.Name}' is not in [0,1].");
            if (d.Edge is not (PortEdge.Left or PortEdge.Top or PortEdge.Right or PortEdge.Bottom))
                throw new ArgumentOutOfRangeException(nameof(definitions), $"Undefined PortEdge value {(int)d.Edge} for '{d.Name}'.");
            ValidateDataType(d.DataType, d.Name);

            specs.Add(new PortSpec(d.Name, d.Flow, d.Edge, d.Fraction, d.Label, d.MaxConnections, d.DataType));
        }

        return new ReadOnlyCollection<PortSpec>(specs);
    }

    private static void ValidateDataType(object? dataType, string portName)
    {
        if (dataType is null) return;
        if (dataType is string or Type) return;
        if (dataType.GetType().IsValueType) return;
        throw new ArgumentException(
            $"PortDefinition.DataType for '{portName}' must be null, a string, a System.Type, or a value type; got {dataType.GetType().FullName}.",
            nameof(dataType));
    }

    private static bool StructurallyEqual(IReadOnlyList<PortSpec> a, IReadOnlyList<PortSpec> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (!a[i].Equals(b[i])) return false;
        return true;
    }

    private static string BuildConflictMessage(Type nodeType, IReadOnlyList<PortSpec> existing, IReadOnlyList<PortSpec> incoming)
    {
        var sb = new StringBuilder();
        sb.Append("Conflicting NodePortRegistry registration for ").Append(nodeType.FullName).AppendLine(".");
        sb.AppendLine("Existing:");
        foreach (var s in existing) sb.Append("  ").AppendLine(s.ToString());
        sb.AppendLine("Incoming:");
        foreach (var s in incoming) sb.Append("  ").AppendLine(s.ToString());
        return sb.ToString();
    }
}
