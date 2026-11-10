// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Hosting.CommandLine.Tests;

/// <summary>
/// 替换进程标准输出与标准错误的用例
/// </summary>
/// <remarks>
/// <see cref="Console.SetOut"/> 与 <see cref="Console.SetError"/> 是进程级的：替换期间并行用例的控制台输出也会写进来。这个集合不与其他集合并行
/// </remarks>
[CollectionDefinition(nameof(ProcessConsoleGroup), DisableParallelization = true)]
public sealed class ProcessConsoleGroup;
