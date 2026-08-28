// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace XiHan.Framework.Bot.Tests.Fakes;

/// <summary>
/// 最小 <see cref="IOptions{TOptions}"/> 包装
/// </summary>
/// <remarks>
/// 不用 Microsoft.Extensions.Options.Options.Create，因为测试命名空间嵌套在 XiHan.Framework.Bot 之下，
/// 裸写 Options.Create 会被解析到 XiHan.Framework.Bot.Options 命名空间上。
/// </remarks>
/// <typeparam name="TOptions">选项类型</typeparam>
public sealed class TestOptionsWrapper<TOptions> : IOptions<TOptions>
    where TOptions : class
{
    /// <summary>
    /// 创建包装
    /// </summary>
    /// <param name="value">选项实例</param>
    public TestOptionsWrapper(TOptions value)
    {
        Value = value;
    }

    /// <summary>
    /// 选项实例
    /// </summary>
    public TOptions Value { get; }
}
