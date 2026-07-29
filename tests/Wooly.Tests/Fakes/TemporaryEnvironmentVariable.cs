namespace Wooly.Tests.Fakes;

/// <summary>
///     An environment variable set for the length of a test and put back afterwards. The process environment is
///     shared by every test in the run, so a test that writes one has to own restoring it.
/// </summary>
internal sealed class TemporaryEnvironmentVariable : IDisposable
{
    private readonly string _name;
    private readonly string? _original;

    public TemporaryEnvironmentVariable(string name)
    {
        _name = name;
        _original = Environment.GetEnvironmentVariable(name);
    }

    public string? Value
    {
        get => Environment.GetEnvironmentVariable(_name);
        set => Environment.SetEnvironmentVariable(_name, value);
    }

    public void Dispose() => Environment.SetEnvironmentVariable(_name, _original);
}
