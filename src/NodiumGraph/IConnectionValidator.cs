namespace NodiumGraph;

public interface IConnectionValidator
{
    bool CanConnect(Port source, Port target);
}
