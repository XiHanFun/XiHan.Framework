// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Running;
using XiHan.Framework.Benchmarks;

// 常驻内存要在一个干净的进程里测，不走 BenchmarkDotNet
if (args is ["--resident-memory", var output])
{
    return await ResidentMemoryProbe.RunAsync(output).ConfigureAwait(false);
}

Program.RunBenchmarks(args);
return 0;

/// <summary>
/// 基准入口
/// </summary>
/// <remarks>
/// 运行：<c>dotnet run -c Release --project framework/benchmarks/XiHan.Framework.Benchmarks -- --filter '*'</c>；
/// 常驻内存探针：<c>-- --resident-memory &lt;输出文件&gt;</c>；CI 的性能门禁由 <c>framework/scripts/Test-Benchmarks.ps1</c> 驱动
/// </remarks>
internal static partial class Program
{
    /// <summary>
    /// 交给 BenchmarkDotNet 按命令行运行基准
    /// </summary>
    /// <remarks>
    /// 单独一个不内联的方法：入口方法编译时不引用 BenchmarkDotNet 的类型，探针进程就不会装载它，测到的常驻内存只含框架自己
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunBenchmarks(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
