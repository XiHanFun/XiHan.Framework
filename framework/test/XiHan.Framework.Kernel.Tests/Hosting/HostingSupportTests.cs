// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Tests.Fixtures;

namespace XiHan.Framework.Kernel.Tests.Hosting;

/// <summary>
/// 宿主支撑类型的行为
/// </summary>
/// <remarks>
/// <see cref="KernelOptions"/> 的显式配置读取与 <see cref="BindingResolver"/> 对目录与配置两种来源的合并
/// </remarks>
public sealed class HostingSupportTests
{
    [Fact]
    public void KernelOptions_FromConfiguration_ParsesAndValidates()
    {
        var options = KernelOptions.FromConfiguration(Hosts.Configuration(
            ("XiHan:Kernel:StartTimeoutSeconds", "2.5"),
            ("XiHan:Kernel:StopTimeoutSeconds", "10"),
            ("XiHan:Kernel:FailurePolicy", "isolateFeature"),
            ("XiHan:Kernel:UseBuiltInMiddleware", "false")));

        Assert.Equal(TimeSpan.FromSeconds(2.5), options.StartTimeout);
        Assert.Equal(TimeSpan.FromSeconds(10), options.StopTimeout);
        Assert.Equal(FailurePolicy.IsolateFeature, options.FailurePolicy);
        Assert.False(options.UseBuiltInMiddleware);

        var defaults = KernelOptions.FromConfiguration(Hosts.Configuration());
        Assert.Equal(TimeSpan.FromSeconds(30), defaults.StartTimeout);
        Assert.Equal(FailurePolicy.TerminateStartup, defaults.FailurePolicy);

        Assert.Throws<ArgumentException>(() => KernelOptions.FromConfiguration(Hosts.Configuration(("XiHan:Kernel:StartTimeoutSeconds", "-1"))));
        Assert.Throws<ArgumentException>(() => KernelOptions.FromConfiguration(Hosts.Configuration(("XiHan:Kernel:FailurePolicy", "explode"))));
        Assert.Throws<ArgumentException>(() => KernelOptions.FromConfiguration(Hosts.Configuration(("XiHan:Kernel:UseBuiltInMiddleware", "maybe"))));
    }

    [Fact]
    public void BindingResolver_ReadsServiceBindingDirectoryAndConfigurationOverrides()
    {
        var root = Path.Combine(Path.GetTempPath(), "xihan-bindings-" + Guid.NewGuid().ToString("N"));
        var dbDirectory = Path.Combine(root, "db");
        Directory.CreateDirectory(dbDirectory);
        try
        {
            File.WriteAllText(Path.Combine(dbDirectory, "type"), "connection:relational\n");
            File.WriteAllText(Path.Combine(dbDirectory, "uri"), "postgres://db/app");
            File.WriteAllText(Path.Combine(dbDirectory, "username"), "app");
            File.WriteAllText(Path.Combine(dbDirectory, ".metadata"), "ignored");

            var fromDirectory = BindingResolver.Resolve(Hosts.Configuration(), root)["db"];
            Assert.Equal("connection:relational", fromDirectory.Type);
            Assert.Equal("postgres://db/app", fromDirectory.Value);
            Assert.Equal("app", fromDirectory.Property("username").ValueOrThrow());
            Assert.False(fromDirectory.Properties.ContainsKey(".metadata"));

            // 配置里的同名绑定整体覆盖目录来源：目录里的 type 不会合并进来
            var overridden = BindingResolver.Resolve(Hosts.Configuration(("XiHan:Bindings:db:value", "Host=override")), root)["db"];
            Assert.Equal("Host=override", overridden.Value);
            Assert.Equal(string.Empty, overridden.Type);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BindingValue_IsBuiltByInitializer_AndMasksItsValueWhenPrinted()
    {
        var binding = new BindingValue
        {
            Name = "db",
            Type = "connection:relational",
            Value = "Host=db;Password=s3cret",
            Properties = new Dictionary<string, string>(StringComparer.Ordinal) { ["schema"] = "public" },
        };

        // with 只换主值，名字、类型与属性照旧（Provider 按租户换连接串时就是这么用的）
        var rebound = binding with { Value = "Host=shard-1" };
        Assert.Equal(("db", "connection:relational", "Host=shard-1"), (rebound.Name, rebound.Type, rebound.Value));
        Assert.Same(binding.Properties, rebound.Properties);
        Assert.Equal("public", rebound.Property("schema").ValueOrThrow());

        // 主值可能是带口令的连接串：打印时遮蔽，其余成员照常打印
        var text = binding.ToString();
        Assert.DoesNotContain("s3cret", text, StringComparison.Ordinal);
        Assert.Contains("Name = db, Type = connection:relational, Value = ***", text, StringComparison.Ordinal);

        // 没有主值的绑定（只给凭据属性）是合法的
        var credentialsOnly = new BindingValue { Name = "queue", Type = string.Empty, Value = null, Properties = new Dictionary<string, string>(StringComparer.Ordinal) };
        Assert.Null(credentialsOnly.Value);
        Assert.False(credentialsOnly.Property("username").HasValue);
    }

    [Fact]
    public void BindingResolver_ReadsScalarConfigurationValue()
    {
        var bindings = BindingResolver.Resolve(Hosts.Configuration(("XiHan:Bindings:cache", "localhost:6379")), serviceBindingRoot: Path.Combine(Path.GetTempPath(), "does-not-exist"));

        Assert.Equal("localhost:6379", bindings["cache"].Value);
    }
}
