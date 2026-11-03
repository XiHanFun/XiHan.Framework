// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Persistence.Abstractions.DataSources;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

/// <summary>
/// 数据源定义、连接串脱敏与选项读取
/// </summary>
/// <remarks>
/// <see cref="DataSourceDefinition"/>、<see cref="ConnectionStrings"/> 与 <see cref="DataSourceOptions"/>：名字文法与保留名、同一连接的判定、连接串不进字符串表示、脱敏与选项读取
/// </remarks>
public sealed class DataSourceDefinitionTests
{
    [Theory]
    [InlineData("orders")]
    [InlineData("1234567890")]
    [InlineData("codegen-42")]
    public void ValidNames_AreAccepted(string name) => Assert.Equal(name, new DataSourceDefinition(name, "sqlite", "Data Source=x.db").Name);

    [Theory]
    [InlineData("default")]
    [InlineData("Orders")]
    [InlineData("-orders")]
    [InlineData("orders_1")]
    [InlineData("")]
    public void InvalidOrReservedNames_AreRejected(string name)
    {
        Assert.False(DataSourceDefinition.IsValidName(name));
        Assert.Throws<ArgumentException>(() => new DataSourceDefinition(name, "sqlite", "Data Source=x.db"));
    }

    [Fact]
    public void BlankTypeOrConnectionString_IsRejected()
    {
        Assert.Throws<ArgumentException>(static () => new DataSourceDefinition("orders", " ", "Data Source=x.db"));
        Assert.Throws<ArgumentException>(static () => new DataSourceDefinition("orders", "sqlite", " "));
    }

    [Fact]
    public void Describes_ComparesNameTypeIgnoringCase_AndConnectionStringExactly()
    {
        var definition = new DataSourceDefinition("orders", "PostgreSQL", "Host=a;Password=p");

        Assert.True(definition.Describes(new DataSourceDefinition("orders", "postgresql", "Host=a;Password=p")));
        Assert.False(definition.Describes(new DataSourceDefinition("orders", "postgresql", "Host=a;Password=P")));
        Assert.False(definition.Describes(new DataSourceDefinition("reports", "postgresql", "Host=a;Password=p")));
        Assert.False(definition.Describes(new DataSourceDefinition("orders", "mysql", "Host=a;Password=p")));
    }

    [Fact]
    public void ToString_DoesNotContainTheConnectionString()
    {
        var text = new DataSourceDefinition("orders", "postgresql", "Host=db;Password=Top-Secret-1").ToString();

        Assert.Contains("orders", text, StringComparison.Ordinal);
        Assert.Contains("postgresql", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Top-Secret-1", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Host=db", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Redact_MasksTheConnectionStringAndSecretValues_ButKeepsHostAndUser()
    {
        const string ConnectionString = "Host=db.local;Username=app;Password=\"p;w\";SecretKey=k-123;Pwd=xyz";

        var redacted = ConnectionStrings.Redact($"failed for 'app' at db.local using {ConnectionString}; pass p;w, key k-123, pwd xyz", ConnectionString);

        Assert.DoesNotContain("p;w", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("k-123", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("xyz", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(ConnectionString, redacted, StringComparison.Ordinal);
        Assert.Contains("db.local", redacted, StringComparison.Ordinal);
        Assert.Contains("'app'", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void Redact_MalformedConnectionString_StillMasksPasswordSegments()
    {
        const string ConnectionString = "Host=db;Password=abc\"def;Broken=\"x";

        var redacted = ConnectionStrings.Redact("value abc\"def leaked", ConnectionString);

        Assert.Equal($"value {ConnectionStrings.Mask} leaked", redacted);
    }

    [Fact]
    public void Describe_JoinsTheMessageChain_AndRedacts()
    {
        var exception = new InvalidOperationException("Exception while connecting", new IOException("Connection refused (Password=hunter2)"));

        var reason = ConnectionStrings.Describe(exception, "Host=db;Password=hunter2");

        Assert.Equal($"Exception while connecting: Connection refused (Password={ConnectionStrings.Mask})", reason);
    }

    [Fact]
    public void Options_ReadFromConfiguration_AndRejectInvalidValues()
    {
        static IConfiguration Configuration(string key, string value) => new ConfigurationBuilder().AddInMemoryCollection([new($"{DataSourceOptions.ConfigurationSection}:{key}", value)]).Build();

        var options = DataSourceOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection([new($"{DataSourceOptions.ConfigurationSection}:MaxCount", "8"), new($"{DataSourceOptions.ConfigurationSection}:HealthProbeIntervalSeconds", "2.5")]).Build());
        var defaults = DataSourceOptions.FromConfiguration(new ConfigurationBuilder().Build());

        Assert.Equal(8, options.MaxCount);
        Assert.Equal(TimeSpan.FromSeconds(2.5), options.HealthProbeInterval);
        Assert.Equal(new DataSourceOptions(), defaults);
        Assert.Equal(64, defaults.MaxCount);
        Assert.Throws<ArgumentException>(() => DataSourceOptions.FromConfiguration(Configuration("MaxCount", "0")));
        Assert.Throws<ArgumentException>(() => DataSourceOptions.FromConfiguration(Configuration("MaxCount", "1025")));
        Assert.Throws<ArgumentException>(() => DataSourceOptions.FromConfiguration(Configuration("HealthProbeIntervalSeconds", "-1")));
    }
}
