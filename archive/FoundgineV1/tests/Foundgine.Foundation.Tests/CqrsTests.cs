using Foundgine.Foundation.CQRS;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Foundgine.Foundation.Tests;

public class CommandDispatcherTests
{
    private sealed record Params(int Amount);

    private sealed class DoubleCommand : ICommand<Params, int>
    {
        public Task<int> ExecuteAsync(Params parameters, CancellationToken cancellationToken) =>
            Task.FromResult(parameters.Amount * 2);
    }

    [Fact]
    public async Task DispatchAsync_ResolvesHandlerFromContainer_AndExecutesIt()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICommand<Params, int>, DoubleCommand>();
        var provider = services.BuildServiceProvider();
        var dispatcher = new CommandDispatcher(provider);

        var result = await dispatcher.DispatchAsync<Params, int>(new Params(21), CancellationToken.None);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task DispatchAsync_Throws_WhenNoHandlerRegistered()
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var dispatcher = new CommandDispatcher(provider);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.DispatchAsync<Params, int>(new Params(1), CancellationToken.None));
    }
}

public class QueryDispatcherTests
{
    private sealed record Params(string Name);

    private sealed class GreetQuery : IQuery<Params, string>
    {
        public Task<string> ExecuteAsync(Params parameters, CancellationToken cancellationToken) =>
            Task.FromResult($"Hello, {parameters.Name}!");
    }

    [Fact]
    public async Task DispatchAsync_ResolvesHandlerFromContainer_AndExecutesIt()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IQuery<Params, string>, GreetQuery>();
        var provider = services.BuildServiceProvider();
        var dispatcher = new QueryDispatcher(provider);

        var result = await dispatcher.DispatchAsync<Params, string>(new Params("World"), CancellationToken.None);

        Assert.Equal("Hello, World!", result);
    }

    [Fact]
    public async Task DispatchAsync_Throws_WhenNoHandlerRegistered()
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var dispatcher = new QueryDispatcher(provider);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.DispatchAsync<Params, string>(new Params("x"), CancellationToken.None));
    }
}
