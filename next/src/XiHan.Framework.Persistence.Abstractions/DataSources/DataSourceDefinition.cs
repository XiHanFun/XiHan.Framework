// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.DataSources;

/// <summary>
/// 一个运行时数据源的定义
/// </summary>
/// <remarks>
/// 名字（<c>^[a-z0-9][a-z0-9-]{0,63}$</c>，<see cref="DefaultName"/> 保留给应用的缺省连接）、数据库类型（不区分大小写，常用名见 <see cref="DatabaseTypes"/>，Provider 认得哪些由 Provider 决定）与连接串。定义通常来自应用自己存在库里的数据源记录（口令由应用解密后放进连接串）。连接串是机密：<see cref="ToString"/> 不含它，错误与健康详情里的驱动消息经 <see cref="ConnectionStrings.Redact"/> 去掉它
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class DataSourceDefinition
{
    /// <summary>
    /// 保留的名字：应用的缺省连接
    /// </summary>
    /// <remarks>
    /// 库结构读取以 <see langword="null"/> 表示缺省连接，错误消息里以这个名字称呼它；运行时数据源不能用这个名字
    /// </remarks>
    public const string DefaultName = "default";

    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="name">数据源名</param>
    /// <param name="databaseType">数据库类型</param>
    /// <param name="connectionString">连接串</param>
    /// <exception cref="ArgumentException">名字不合法或是保留名，数据库类型或连接串为空白</exception>
    public DataSourceDefinition(string name, string databaseType, string connectionString)
    {
        Name = IsValidName(name)
            ? name
            : throw new ArgumentException($"Data source name '{name}' is invalid: it must be 1 to 64 lowercase letters, digits or hyphens, starting with a letter or digit, and must not be '{DefaultName}'.", nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseType);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        DatabaseType = databaseType;
        ConnectionString = connectionString;
    }

    /// <summary>
    /// 数据源名
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 数据库类型（不区分大小写）
    /// </summary>
    public string DatabaseType { get; }

    /// <summary>
    /// 连接串（机密）
    /// </summary>
    public string ConnectionString { get; }

    /// <summary>
    /// 名字是否合法
    /// </summary>
    /// <remarks>
    /// 合乎文法且不是 <see cref="DefaultName"/>
    /// </remarks>
    public static bool IsValidName(string? name) => name is not null && NamePattern().IsMatch(name) && !string.Equals(name, DefaultName, StringComparison.Ordinal);

    /// <summary>
    /// 是否与另一个定义描述同一个连接
    /// </summary>
    /// <remarks>
    /// 名字、数据库类型（不区分大小写）与连接串（区分大小写）都相同
    /// </remarks>
    public bool Describes(DataSourceDefinition other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return string.Equals(Name, other.Name, StringComparison.Ordinal)
            && string.Equals(DatabaseType, other.DatabaseType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(ConnectionString, other.ConnectionString, StringComparison.Ordinal);
    }

    /// <summary>
    /// 把文本里的连接串与口令类键的值换成 <c>***</c>
    /// </summary>
    /// <remarks>
    /// 见 <see cref="ConnectionStrings.Redact"/>
    /// </remarks>
    public string Redact(string text) => ConnectionStrings.Redact(text, ConnectionString);

    /// <summary>
    /// 名字与数据库类型，不含连接串
    /// </summary>
    public override string ToString() => $"DataSourceDefinition {{ Name = {Name}, DatabaseType = {DatabaseType} }}";

    /// <summary>
    /// 名字的匹配
    /// </summary>
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$")]
    private static partial Regex NamePattern();
}
