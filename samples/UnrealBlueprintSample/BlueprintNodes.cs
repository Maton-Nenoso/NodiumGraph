using NodiumGraph.Model;

namespace UnrealBlueprintSample;

// One subclass per UE blueprint node so each gets its own DataTemplate /
// NodeTemplate match in MainWindow.axaml. Empty bodies — port topology
// is declared in AXAML via <ng:NodeTemplate.Ports>, materialized lazily.

public sealed class BeginOverlapNode : Node;

public sealed class BranchNode : Node;

public sealed class GetPlayerPawnNode : Node;

public sealed class EqualsNode : Node;

public sealed class CastToCharacterNode : Node;

public sealed class LaunchCharacterNode : Node;
