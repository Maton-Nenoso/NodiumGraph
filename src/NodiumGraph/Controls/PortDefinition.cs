using NodiumGraph.Model;

namespace NodiumGraph.Controls;

/// <summary>
/// XAML-side construction recipe for a single port. A list of <see cref="PortDefinition"/>
/// appears under <c>&lt;ng:NodeTemplate.Ports&gt;</c>; <c>NodePortRegistry</c> projects each
/// instance into a <see cref="PortSpec"/> at registration time.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Fraction"/> is nullable. <c>null</c> declares intent to auto-layout — the
/// owning <c>FixedPortProvider</c> will distribute auto ports evenly along their edge.
/// A non-null value pins the port at that fraction; the value must lie in <c>[0, 1]</c>.
/// </para>
/// </remarks>
public sealed class PortDefinition
{
    public string Name { get; set; } = string.Empty;
    public PortFlow Flow { get; set; } = PortFlow.Input;
    public PortEdge Edge { get; set; } = PortEdge.Left;
    public double? Fraction { get; set; }

    public string? Label { get; set; }
    public uint? MaxConnections { get; set; }
    public object? DataType { get; set; }
}
