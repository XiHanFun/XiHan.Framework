// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace XiHan.Framework.Cli.Tests.Knowledge;

/// <summary>
/// 记录读取路径的配置包装
/// </summary>
/// <remarks>
/// 经索引器或 <c>Value</c> 读值都记下完整路径；取子节与子项时返回同样的包装，按名字展开的子节也记得到
/// </remarks>
/// <param name="inner">被包装的配置（根或节）</param>
/// <param name="log">读取记录</param>
internal sealed class RecordingConfiguration(IConfiguration inner, ReadLog log) : IConfigurationSection
{
    /// <inheritdoc />
    public string? this[string key]
    {
        get
        {
            // 根的路径是空串，直接取键；节下才拼接
            log.Read(Path.Length == 0 ? key : ConfigurationPath.Combine(Path, key));
            return inner[key];
        }

        set => inner[key] = value;
    }

    /// <inheritdoc />
    public string Key => (inner as IConfigurationSection)?.Key ?? string.Empty;

    /// <inheritdoc />
    public string Path => (inner as IConfigurationSection)?.Path ?? string.Empty;

    /// <inheritdoc />
    public string? Value
    {
        get
        {
            log.Read(Path);
            return (inner as IConfigurationSection)?.Value;
        }

        set => ((IConfigurationSection)inner).Value = value;
    }

    /// <inheritdoc />
    public IEnumerable<IConfigurationSection> GetChildren() => inner.GetChildren().Select(c => new RecordingConfiguration(c, log)).ToList();

    /// <inheritdoc />
    public IChangeToken GetReloadToken() => inner.GetReloadToken();

    /// <inheritdoc />
    public IConfigurationSection GetSection(string key) => new RecordingConfiguration(inner.GetSection(key), log);
}
