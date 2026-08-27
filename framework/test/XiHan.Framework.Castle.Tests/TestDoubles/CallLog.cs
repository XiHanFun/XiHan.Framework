// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Castle.Tests.TestDoubles;

/// <summary>
/// 调用轨迹记录器
/// </summary>
/// <remarks>
/// 拦截器链的执行顺序是本项目最核心的契约，用一个共享的有序列表把"进入/离开"逐条记下来，
/// 断言时直接比对下标，比只统计次数更能暴露顺序错乱。
/// </remarks>
public sealed class CallLog
{
    private readonly List<string> _entries = [];

    /// <summary>
    /// 已记录的轨迹条目，按写入顺序排列
    /// </summary>
    public IReadOnlyList<string> Entries => _entries;

    /// <summary>
    /// 追加一条轨迹
    /// </summary>
    /// <param name="entry">轨迹内容</param>
    public void Add(string entry)
    {
        lock (_entries)
        {
            _entries.Add(entry);
        }
    }
}
