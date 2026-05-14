using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using NodiumGraph.Interactions;
using NodiumGraph.Model;

namespace UnrealBlueprintSample;

public partial class MainWindow : Window
{
    // UE-style pin colors keyed by Port.DataType. The library uses object.Equals
    // on the opaque DataType token; strings are convenient for AXAML.
    private static readonly IBrush ExecBrush   = Brushes.White;
    private static readonly IBrush BoolBrush   = new SolidColorBrush(Color.Parse("#C8313D"));
    private static readonly IBrush IntBrush    = new SolidColorBrush(Color.Parse("#1FB67F"));
    private static readonly IBrush ObjectBrush = new SolidColorBrush(Color.Parse("#3DA0FF"));
    private static readonly IBrush StructBrush = new SolidColorBrush(Color.Parse("#3D6FFF"));

    public MainWindow()
    {
        InitializeComponent();

        var graph = new Graph();

        var beginOverlap = new BeginOverlapNode
        {
            Title = "On Component Begin Overlap (Box)",
            X = 40, Y = 40,
        };
        var branch = new BranchNode
        {
            Title = "Branch",
            X = 900, Y = 60,
        };
        var getPlayerPawn = new GetPlayerPawnNode
        {
            Title = "Get Player Pawn",
            X = 40, Y = 540,
        };
        var equals = new EqualsNode
        {
            Title = "==",
            X = 620, Y = 280,
        };
        var castToCharacter = new CastToCharacterNode
        {
            Title = "Cast To Character",
            X = 660, Y = 540,
        };
        var launchCharacter = new LaunchCharacterNode
        {
            Title = "Launch Character",
            X = 1180, Y = 320,
        };

        graph.AddNode(beginOverlap);
        graph.AddNode(branch);
        graph.AddNode(getPlayerPawn);
        graph.AddNode(equals);
        graph.AddNode(castToCharacter);
        graph.AddNode(launchCharacter);

        // Port topology is declared in MainWindow.axaml. Accessing .Ports triggers
        // lazy materialization from NodePortRegistry, after which we can apply
        // per-pin styles by DataType — there's no Style slot on <ng:PortDefinition>.
        foreach (var node in graph.Nodes)
            StyleBlueprintPorts(node);

        // Wire the connections from the screenshot.
        // BeginOverlap.exec       -> Branch.exec        (exec flow)
        // BeginOverlap.OtherActor -> Equals.A           (object compare)
        // GetPlayerPawn.Return    -> Equals.B           (object compare)
        // Equals.Result           -> Branch.Condition   (bool drives branch)
        // Branch.True             -> LaunchCharacter.exec
        // GetPlayerPawn.Return    -> CastToCharacter.Object
        // CastToCharacter.AsChar  -> LaunchCharacter.Target
        Connect(graph, beginOverlap,    "exec",         branch,          "exec");
        Connect(graph, beginOverlap,    "OtherActor",   equals,          "A");
        Connect(graph, getPlayerPawn,   "ReturnValue",  equals,          "B");
        Connect(graph, equals,          "Result",       branch,          "Condition");
        Connect(graph, branch,          "True",         launchCharacter, "exec");
        Connect(graph, getPlayerPawn,   "ReturnValue",  castToCharacter, "Object");
        Connect(graph, castToCharacter, "AsCharacter",  launchCharacter, "Target");

        Canvas.Graph = graph;

        // Fidelity gap, called out: UE colors each wire by source-pin DataType.
        // NodiumGraphCanvas currently uses a single DefaultConnectionStyle for
        // every connection — no per-connection style hook in the render loop.
        // Stylistic compromise: one light-gray wire color for all connections;
        // the colored port endpoints + headers carry the blueprint identity.
        Canvas.DefaultConnectionStyle = new ConnectionStyle(
            stroke: new SolidColorBrush(Color.Parse("#B0B0B0")),
            thickness: 2.5);

        // Default validator would reject the cross-DataType drags (e.g. object vs.
        // bool) during interactive editing. Null disables validation so a user can
        // freely rewire the demo without immediate rejections from the strict default.
        Canvas.ConnectionValidator = null;
    }

    private static void StyleBlueprintPorts(Node node)
    {
        foreach (var port in node.Ports)
        {
            port.Style = (port.DataType as string) switch
            {
                "exec"   => new PortStyle { Shape = PortShape.Triangle, Fill = ExecBrush,   Size = 6, Stroke = Brushes.Black, StrokeWidth = 1 },
                "bool"   => new PortStyle { Shape = PortShape.Circle,   Fill = BoolBrush,   Size = 5 },
                "int"    => new PortStyle { Shape = PortShape.Circle,   Fill = IntBrush,    Size = 5 },
                "object" => new PortStyle { Shape = PortShape.Circle,   Fill = ObjectBrush, Size = 5 },
                "struct" => new PortStyle { Shape = PortShape.Circle,   Fill = StructBrush, Size = 5 },
                _        => new PortStyle { Shape = PortShape.Circle,   Fill = Brushes.Gray, Size = 5 },
            };
        }
    }

    private static void Connect(Graph graph, Node sourceNode, string sourcePort, Node targetNode, string targetPort)
    {
        var s = sourceNode.Ports.First(p => p.Name == sourcePort);
        var t = targetNode.Ports.First(p => p.Name == targetPort);
        graph.AddConnection(new Connection(s, t));
    }
}
