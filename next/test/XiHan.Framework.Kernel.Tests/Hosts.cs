// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests;

internal static class Hosts
{
    public static IConfiguration Configuration(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    public static (ServiceProvider Provider, App App) BuildApp(Action<AppBuilder> configure, IConfiguration? configuration = null)
    {
        var services = new ServiceCollection();
        var builder = new AppBuilder(services, configuration ?? Configuration());
        configure(builder);
        builder.Build();
        var provider = services.BuildServiceProvider();
        return (provider, provider.GetRequiredService<App>());
    }

    public static InvocationContext Context(IServiceProvider services, DateTimeOffset? deadline = null, CancellationToken cancellationToken = default)
        => App.CreateContext(services, cancellationToken, deadline: deadline);
}
