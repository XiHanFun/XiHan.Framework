// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Script.Tests.Extensions;

/// <summary>
/// 进程级度量测试集合
/// </summary>
/// <remarks>
/// 基准测试的分配量取自进程级累计计数，耗时取自墙钟；并行执行时其他用例（尤其是真实 Roslyn 编译）
/// 的分配与 CPU 争用会直接计入读数，让上界断言随机红。
/// 因此凡是对进程级分配量或耗时做区间断言的测试类都归入本集合，禁用并行。
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public class ProcessWideMeasurementCollection
{
    /// <summary>
    /// 集合名称
    /// </summary>
    public const string Name = "ProcessWideMeasurement";
}
