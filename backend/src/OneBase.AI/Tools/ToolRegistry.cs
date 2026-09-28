namespace OneBase.AI.Tools;

public interface IToolRegistry
{
    IReadOnlyCollection<ITool> All { get; }
    ITool? Find(string name);
}

internal sealed class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, ITool> _tools;

    public ToolRegistry(IEnumerable<ITool> tools)
    {
        _tools = tools.ToDictionary(t => t.Name, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<ITool> All => _tools.Values;

    public ITool? Find(string name) => _tools.GetValueOrDefault(name);
}
