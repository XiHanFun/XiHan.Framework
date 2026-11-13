// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Benchmarks;

// 冷启动的 Native AOT 口径（ADR-0227）：只有探针，不带 BenchmarkDotNet（它要在运行时生成代码，AOT 下不能用）
if (args is ["--resident-memory", var output])
{
    return await ResidentMemoryProbe.RunAsync(output).ConfigureAwait(false);
}

await Console.Error.WriteLineAsync("Usage: XiHan.Framework.StartupProbe --resident-memory <output file>").ConfigureAwait(false);
return 64;
