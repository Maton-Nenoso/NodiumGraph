using NodiumGraph.Model;
using Xunit;

namespace NodiumGraph.Tests;

public class PortFlowTests
{
    [Fact]
    public void PortFlow_has_Input_and_Output_values()
    {
        Assert.Equal(0, (int)PortFlow.Input);
        Assert.Equal(1, (int)PortFlow.Output);
    }
}
