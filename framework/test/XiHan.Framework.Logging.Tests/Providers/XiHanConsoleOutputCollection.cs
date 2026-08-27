// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Logging.Tests.Providers;

/// <summary>
/// 控制台输出测试集合
/// </summary>
/// <remarks>
/// 控制台日志器直接写 Console.Out，而 Console.Out 是进程级共享状态；
/// 断言输出必须先用 Console.SetOut 临时接管，并行执行会互相截流导致随机失败，
/// 因此凡是需要接管控制台输出的测试类都归入本集合，禁用并行。
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public class XiHanConsoleOutputCollection
{
    /// <summary>
    /// 集合名称
    /// </summary>
    public const string Name = "XiHanConsoleOutput";
}
