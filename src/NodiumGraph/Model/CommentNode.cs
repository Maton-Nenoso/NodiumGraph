namespace NodiumGraph.Model;

public class CommentNode : Node
{
    private string _comment = string.Empty;

    public string Comment
    {
        get => _comment;
        set => SetField(ref _comment, value);
    }
}
