// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using BenchmarkDotNet.Attributes;
using XiHan.Framework.Kernel.Operations;

namespace XiHan.Framework.Benchmarks;

/// <summary>
/// 操作响应的写出开销
/// </summary>
/// <remarks>
/// XGI 入口与幂等记录都经 <see cref="OperationDescriptor.EncodeResponse"/> 把响应对象写成载荷（ADR-0342）
/// </remarks>
[MemoryDiagnoser]
public class OperationBenchmarks
{
    /// <summary>
    /// 回声操作的描述
    /// </summary>
    /// <remarks>
    /// 响应的元数据来自选项与生成时一致的源生成上下文
    /// </remarks>
    private OperationDescriptor _descriptor = null!;

    /// <summary>
    /// 每次写出的响应
    /// </summary>
    private OrderSummary _response = null!;

    /// <summary>
    /// 建描述与响应
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var info = BenchmarkJsonContext.Default.OrderSummary;
        _descriptor = new OperationDescriptor<OrderEchoOperation, OrderSummary, OrderSummary>(new(OrderEchoOperation.Id), "xihan.benchmark.orders", info, info);
        _response = new OrderSummary("ord-20261127-0001", "Contoso Ltd.", 1234.56m, 2, ["express", "gift", "priority"]);
    }

    /// <summary>
    /// 把一个类型化的响应写成 XGI 载荷
    /// </summary>
    [Benchmark]
    public ReadOnlyMemory<byte> EncodeResponse() => _descriptor.EncodeResponse(_response);
}
