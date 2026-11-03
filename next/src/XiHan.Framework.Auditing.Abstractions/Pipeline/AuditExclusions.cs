// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Auditing.Abstractions.Pipeline;

/// <summary>
/// <see cref="AuditingOptions.Exclude"/> 的匹配器
/// </summary>
/// <remarks>
/// 中间件构造时建一次：精确 ID 进哈希集合，前缀去掉末尾的 <c>*</c> 后按序数比较开头。每次操作调用都要判定，不在调用路径上解析配置
/// </remarks>
internal sealed class AuditExclusions
{
    /// <summary>
    /// 精确排除的操作 ID
    /// </summary>
    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);

    /// <summary>
    /// 排除的操作 ID 前缀（已去掉末尾的 <c>*</c>）
    /// </summary>
    private readonly List<string> _prefixes = [];

    /// <summary>
    /// 按选项里的排除项建匹配器
    /// </summary>
    /// <param name="entries">已校验的排除项</param>
    public AuditExclusions(IReadOnlyList<string> entries)
    {
        foreach (var entry in entries)
        {
            if (entry.EndsWith('*'))
            {
                _prefixes.Add(entry[..^1]);
            }
            else
            {
                _ids.Add(entry);
            }
        }
    }

    /// <summary>
    /// 操作 ID 是否被排除
    /// </summary>
    public bool Excludes(string operationId)
    {
        if (_ids.Contains(operationId))
        {
            return true;
        }

        foreach (var prefix in _prefixes)
        {
            if (operationId.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
