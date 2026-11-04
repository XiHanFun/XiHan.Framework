// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Persistence.EntityFrameworkCore;

/// <summary>
/// 把字符串匹配的翻译插件登记到 EF Core 内部服务容器的选项扩展
/// </summary>
/// <remarks>
/// 方法翻译插件只能经选项扩展登记：EF Core 按选项扩展建内部服务容器，插件先于 Provider 内置的翻译被询问
/// 扩展不带配置，任何两个实例等价，共用同一个内部服务容器。应用自己用 <c>UseInternalServiceProvider</c> 提供内部服务容器时扩展的服务不会登记，这种用法不受支持
/// </remarks>
internal sealed class SqliteStringMatchingExtension : IDbContextOptionsExtension
{
    /// <summary>
    /// 扩展的描述
    /// </summary>
    /// <remarks>
    /// EF Core 频繁读取，建一次
    /// </remarks>
    private DbContextOptionsExtensionInfo? _info;

    /// <inheritdoc />
    public DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

    /// <inheritdoc />
    public void ApplyServices(IServiceCollection services)
        => new EntityFrameworkRelationalServicesBuilder(services).TryAdd<IMethodCallTranslatorPlugin, SqliteStringMatchingTranslator>();

    /// <inheritdoc />
    public void Validate(IDbContextOptions options)
    {
    }

    /// <summary>
    /// 扩展的描述：不是数据库 Provider、没有配置
    /// </summary>
    private sealed class ExtensionInfo : DbContextOptionsExtensionInfo
    {
        /// <summary>
        /// 创建
        /// </summary>
        /// <param name="extension">所描述的扩展</param>
        public ExtensionInfo(IDbContextOptionsExtension extension)
            : base(extension)
        {
        }

        /// <inheritdoc />
        public override bool IsDatabaseProvider => false;

        /// <inheritdoc />
        public override string LogFragment => string.Empty;

        /// <inheritdoc />
        public override int GetServiceProviderHashCode() => 0;

        /// <inheritdoc />
        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => other is ExtensionInfo;

        /// <inheritdoc />
        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo) => debugInfo["XiHan:SqliteStringMatching"] = "1";
    }
}
