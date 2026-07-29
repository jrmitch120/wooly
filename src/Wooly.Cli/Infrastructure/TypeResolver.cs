using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace Wooly.Cli.Infrastructure;

/// <summary>Resolves command types and their dependencies out of the built container.</summary>
internal sealed class TypeResolver(ServiceProvider provider) : ITypeResolver, IDisposable
{
    public object? Resolve(Type? type) => type is null ? null : provider.GetService(type);

    public void Dispose() => provider.Dispose();
}
