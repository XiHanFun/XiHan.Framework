// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Hosting.CommandLine.Tests;

/// <summary>
/// 改进程环境变量的用例。
/// </summary>
/// <remarks>
/// 环境变量是进程级的：设置期间同一程序集里并行的命令都会拿到同一份凭据。这个集合不与其他集合并行。
/// </remarks>
[CollectionDefinition(nameof(ProcessEnvironmentGroup), DisableParallelization = true)]
public sealed class ProcessEnvironmentGroup;
