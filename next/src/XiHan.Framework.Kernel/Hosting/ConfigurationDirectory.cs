// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 挂载的配置目录约定（RFC-0016）：环境变量 <c>XIHAN_CONFIG_PATH</c> 指向一个目录（k8s 里是 ConfigMap 的挂载点），宿主把其中的 JSON 文件追加为配置源并监听变化。
/// 内核只负责找文件；装载由各宿主（<c>Hosting.AspNetCore</c>、<c>Hosting.CommandLine</c>）的 <c>AddXiHan</c> 完成。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class ConfigurationDirectory
{
    /// <summary>
    /// 环境变量名。
    /// </summary>
    public const string Variable = "XIHAN_CONFIG_PATH";

    /// <summary>
    /// 基础文件名：总是最先装载，其余文件按序数排序随后覆盖。
    /// </summary>
    public const string BaseFileName = "appsettings.json";

    /// <summary>
    /// 列出目录里要装载的 JSON 文件（完整路径，装载顺序）；<paramref name="directory"/> 为空时读环境变量；没有目录或目录不存在时为空。
    /// </summary>
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
}
