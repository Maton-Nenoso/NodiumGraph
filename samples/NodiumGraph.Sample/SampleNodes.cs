namespace NodiumGraph.Sample;

using NodiumGraph.Model;

public class InputSourceNode : Node { }

public class TransformNode : Node { }

public class FilterNode : Node { }

public class MergeNode : Node { }

public class OutputSinkNode : Node { }

// Ports for ConstantNode are declared entirely in MainWindow.axaml via <ng:NodeTemplate>.
public class ConstantNode : Node { }
