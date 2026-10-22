// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

/// <summary>
/// 基准入口。运行：<c>dotnet run -c Release --project next/benchmarks/XiHan.Framework.Benchmarks -- --filter '*'</c>。
/// </summary>
internal static partial class Program;
