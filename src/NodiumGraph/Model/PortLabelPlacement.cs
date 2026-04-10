namespace NodiumGraph.Model;

/// <summary>
/// Controls where a port label is rendered relative to the port visual.
/// When set to null on a port, placement is auto-determined from the port's angle.
/// </summary>
public enum PortLabelPlacement
{
    Left,
    Right,
    Above,
    Below
}
