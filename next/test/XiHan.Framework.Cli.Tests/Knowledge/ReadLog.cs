// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Tests.Knowledge;

/// <summary>
/// 一次调用读过的配置路径
/// </summary>
/// <remarks>
/// 配置键不区分大小写
/// </remarks>
internal sealed class ReadLog
{
    /// <summary>
    /// 读过值的路径
    /// </summary>
    public HashSet<string> Reads { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 记一次读取
    /// </summary>
    public void Read(string path) => Reads.Add(path);
}
