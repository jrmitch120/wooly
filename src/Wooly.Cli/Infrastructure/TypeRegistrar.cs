using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace Wooly.Cli.Infrastructure;

/// <summary>
///     Bridges Spectre.Console.Cli's type registration onto <see cref="IServiceCollection" />, so commands take their
///     dependencies through constructor injection from the same container the TUI uses.
/// </summary>
internal sealed class TypeRegistrar(IServiceCollection services) : ITypeRegistrar
{
    public void Register(Type service, Type implementation) => services.AddSingleton(service, implementation);

    public void RegisterInstance(Type service, object implementation) => services.AddSingleton(service, implementation);

    public void RegisterLazy(Type service, Func<object> factory)
    {
        // Guarded here, unlike the two above, because a null factory would otherwise surface as a null reference at
        // resolution time — long after the registration that caused it.
        ArgumentNullException.ThrowIfNull(factory);

        services.AddSingleton(service, _ => factory());
    }

    public ITypeResolver Build() => new TypeResolver(services.BuildServiceProvider());
}
