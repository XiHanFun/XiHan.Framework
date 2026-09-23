// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Script.Core;

/// <summary>
/// 内存使用情况
/// </summary>
/// <remarks>
/// 记录一段执行期间的内存分配量与各代 GC 回收次数。
/// <para>
/// 分配量取自 <see cref="GC.GetTotalAllocatedBytes(bool)"/>：这是进程自启动以来的累计分配字节数，
/// 单调不减，两次采样相减即为这段期间的分配量，恒为非负。
/// 此前用的是 <see cref="GC.GetTotalMemory(bool)"/> 的堆占用读数，有两个问题：
/// 一是执行期间只要发生回收，差值就为负，无法反映真实消耗；
/// 二是该读数在 regions GC 下可能整体为负（dotnet/runtime#130888），
/// 差值随之失真——框架在 v4.4.0 已因同一原因调整过 Observability 的内存诊断读数。
/// </para>
/// <para>
/// 注意这是<b>进程级</b>计数，并发执行的多段代码会互相计入对方的分配量。
/// 顺序执行时数值准确；需要精确归因到单次执行时，请在调用方串行化或改用专门的性能剖析工具。
/// </para>
/// <para>
/// 采样用 <c>precise: false</c>，不统计各线程尚未结算的分配上下文，
/// 因此读数可能比实际分配量偏低若干 KB（每个参与线程至多一个分配量子，实测约 8 KB）。
/// 这个误差对"这段执行消耗了多少内存"的量级判断无影响，换来的是可以逐次执行采样的低开销。
/// </para>
/// </remarks>
public class MemoryUsage
{
    /// <summary>
    /// 执行前的进程累计分配字节数
    /// </summary>
    /// <remarks>绝对值本身没有意义，只用于与 <see cref="AllocatedBytesAfter"/> 相减。</remarks>
    public long AllocatedBytesBefore { get; set; }

    /// <summary>
    /// 执行后的进程累计分配字节数
    /// </summary>
    /// <remarks>绝对值本身没有意义，只用于与 <see cref="AllocatedBytesBefore"/> 相减。</remarks>
    public long AllocatedBytesAfter { get; set; }

    /// <summary>
    /// 本次执行期间分配的字节数
    /// </summary>
    /// <remarks>累计分配量单调不减，因此该值恒为非负；并发执行时会包含其他执行的分配量。</remarks>
    public long AllocatedBytes => AllocatedBytesAfter - AllocatedBytesBefore;

    /// <summary>
    /// 垃圾回收次数
    /// </summary>
    /// <remarks>
    /// 键为 GC 代数(0/1/2)。含义随生命周期切换：
    /// <see cref="Create"/> 之后是采样基线上的绝对回收次数，
    /// <see cref="Complete"/> 之后被原地换算成本次执行期间的回收次数增量。
    /// 增量恒为非负，但完全可以是 0——执行期间不发生回收是正常情况。
    /// </remarks>
    public Dictionary<int, int> GcCollections { get; set; } = [];

    /// <summary>
    /// 创建内存使用记录
    /// </summary>
    /// <returns>内存使用记录</returns>
    public static MemoryUsage Create()
    {
        return new MemoryUsage
        {
            // precise: false 只读各线程已结算的计数，开销小；实测在并发分配与强制回收下仍单调不减
            AllocatedBytesBefore = GC.GetTotalAllocatedBytes(false),
            GcCollections = new Dictionary<int, int>
            {
                { 0, GC.CollectionCount(0) },
                { 1, GC.CollectionCount(1) },
                { 2, GC.CollectionCount(2) }
            }
        };
    }

    /// <summary>
    /// 完成内存使用记录
    /// </summary>
    /// <remarks>
    /// 采样 <see cref="AllocatedBytesAfter"/>，并把 <see cref="GcCollections"/> 里的绝对回收次数原地换算成增量。
    /// 因为是原地换算，同一实例上重复调用会把上一次的增量当成基线，得到无意义的数值；每次采样只调用一次。
    /// </remarks>
    public void Complete()
    {
        AllocatedBytesAfter = GC.GetTotalAllocatedBytes(false);

        var currentGcCollections = new Dictionary<int, int>
        {
            { 0, GC.CollectionCount(0) },
            { 1, GC.CollectionCount(1) },
            { 2, GC.CollectionCount(2) }
        };

        // 计算GC增量
        foreach (var kvp in GcCollections.ToList())
        {
            GcCollections[kvp.Key] = currentGcCollections[kvp.Key] - kvp.Value;
        }
    }
}
