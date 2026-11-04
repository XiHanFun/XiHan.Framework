// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 挂载的配置目录约定（RFC-0016）
/// </summary>
/// <remarks>
/// 环境变量 <c>XIHAN_CONFIG_PATH</c> 指向一个目录（k8s 里是 ConfigMap 的挂载点），宿主把其中的 JSON 文件作为配置源插在 <c>appsettings.{环境}.json</c> 之后、环境变量之前，并监听变化。内核只负责找文件与认出 appsettings 文件；装载由各宿主（<c>Hosting.AspNetCore</c>、<c>Hosting.CommandLine</c>）的 <c>AddXiHan</c> 完成
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class ConfigurationDirectory
{
    /// <summary>
    /// 环境变量名
    /// </summary>
    public const string Variable = "XIHAN_CONFIG_PATH";

    /// <summary>
    /// 基础文件名：总是最先装载，其余文件按序数排序随后覆盖
    /// </summary>
    public const string BaseFileName = "appsettings.json";

    /// <summary>
    /// 列出目录里要装载的 JSON 文件（完整路径，装载顺序）
    /// </summary>
    /// <remarks>
    /// <paramref name="directory"/> 为空时读环境变量；没有目录或目录不存在时为空
    /// </remarks>
    public static IReadOnlyList<string> Resolve(string? directory = null)
    {
        directory ??= Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return [];
        }

        // 只取顶层 *.json；ConfigMap 挂载工具放置的 ..data 之类以点开头的条目不是配置文件
        return Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .Where(static file => !Path.GetFileName(file).StartsWith('.'))
            .OrderBy(static file => string.Equals(Path.GetFileName(file), BaseFileName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(static file => Path.GetFileName(file), StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// 判断文件配置源的路径是否是宿主默认装载的 appsettings 文件
    /// </summary>
    /// <remarks>
    /// 文件名（不看目录）是 <c>appsettings.json</c> 或 <c>appsettings.{<paramref name="environmentName"/>}.json</c>，不区分大小写。宿主把目录里的文件紧接最后一个这样的配置源插入
    /// </remarks>
    public static bool IsApplicationSettings(string path, string environmentName)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(environmentName);
        var name = Path.GetFileName(path);
        return string.Equals(name, BaseFileName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, $"appsettings.{environmentName}.json", StringComparison.OrdinalIgnoreCase);
    }
}
