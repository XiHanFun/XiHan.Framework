// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.ObjectStorage.FileSystem.Tests;

/// <summary>
/// 让并发的读取方与一个写入方确定地交错
/// </summary>
/// <remarks>
/// 每个读取方先完成一次读取，写入方才开始写；写入方写满规定的轮数后，还要写到每个读取方都有一次在写入期间开始并读完的读取才停下。
/// 读取方与写入方每轮都经线程池的全局队列让出线程。线程池线程少时（CI 的 4 核运行器上同一程序集的别的测试类也在跑），晚排上的读取方让写入方多写几轮，而不是一次都没读到；不靠 sleep，也不阻塞线程池线程
/// </remarks>
internal sealed class Interleaving
{
    /// <summary>
    /// 写入方还没开始
    /// </summary>
    private const int NotStarted = 0;

    /// <summary>
    /// 写入方正在写
    /// </summary>
    private const int Writing = 1;

    /// <summary>
    /// 写入方已停下
    /// </summary>
    private const int Stopped = 2;

    /// <summary>
    /// 各读取方读完了第一次
    /// </summary>
    private readonly TaskCompletionSource[] _readOnce;

    /// <summary>
    /// 各读取方读完了一次在写入期间开始的读取
    /// </summary>
    private readonly TaskCompletionSource[] _readWhileWriting;

    /// <summary>
    /// 写入方所处的阶段
    /// </summary>
    /// <remarks>
    /// 只由写入方推进，只向前：<see cref="NotStarted"/> → <see cref="Writing"/> → <see cref="Stopped"/>
    /// </remarks>
    private int _phase = NotStarted;

    /// <summary>
    /// 为 <paramref name="readers"/> 个读取方创建
    /// </summary>
    public Interleaving(int readers)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(readers);
        _readOnce = Signals(readers);
        _readWhileWriting = Signals(readers);
    }

    /// <summary>
    /// 读取方的个数
    /// </summary>
    public int Readers => _readOnce.Length;

    /// <summary>
    /// 写入方：等每个读取方起跑，再至少写 <paramref name="rounds"/> 轮
    /// </summary>
    /// <remarks>
    /// <paramref name="write"/> 收到从 0 起的轮次。写满轮数后接着写，直到每个读取方都有一次在写入期间开始并读完的读取；读取方失败时不再等它。无论怎样结束都标记停下，读取方据此退出
    /// </remarks>
    public async Task WriteAsync(int rounds, Func<int, Task> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        try
        {
            // 读取方都在跑了才开始写：线程池忙时晚排上的读取方不会错过整个写入期
            await Task.WhenAll(_readOnce.Select(static signal => signal.Task)).ConfigureAwait(false);
            Volatile.Write(ref _phase, Writing);
            var overlapped = Task.WhenAll(_readWhileWriting.Select(static signal => signal.Task));
            // 轮数只是下限：还有读取方没在写入期间读完一次就接着写，不靠加轮数或等待碰运气
            for (var round = 0; round < rounds || !overlapped.IsCompleted; round++)
            {
                // 每轮让出：写入的续体排在本线程的局部队列里，不让出时排在全局队列里的读取方可能一直轮不到
                await Task.Yield();
                await write(round).ConfigureAwait(false);
            }
        }
        finally
        {
            // 写入失败时也标记停下，读取方不会一直读下去
            Volatile.Write(ref _phase, Stopped);
        }
    }

    /// <summary>
    /// 读取方 <paramref name="reader"/>：反复读，直到写入方停下
    /// </summary>
    /// <remarks>
    /// 至少读一次。读取抛出时把异常交给等它的写入方再抛出，写入方不会一直等一个不再读的读取方
    /// </remarks>
    public async Task ReadAsync(int reader, Func<Task> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        try
        {
            // 阶段在这次读取开始之前取：取到写入中，这次读取就开始于写入开始之后，而写入方要等它读完才停，所以它整个落在写入期间
            for (var phase = Volatile.Read(ref _phase); phase != Stopped; phase = Volatile.Read(ref _phase))
            {
                // 每轮让出：Head 与列举同步完成，Unix 上的文件读写与读完后的续体都排在本线程的局部队列里，
                // 不让出时一个读取方占住一个线程池线程直到写入方停下，工作项一直在完成，线程池也不补线程
                await Task.Yield();
                await read().ConfigureAwait(false);
                _readOnce[reader].TrySetResult();
                if (phase == Writing)
                {
                    _readWhileWriting[reader].TrySetResult();
                }
            }
        }
        catch (Exception exception)
        {
            _readOnce[reader].TrySetException(exception);
            _readWhileWriting[reader].TrySetException(exception);
            throw;
        }
    }

    /// <summary>
    /// 一组续体异步执行的信号
    /// </summary>
    private static TaskCompletionSource[] Signals(int count)
        => [.. Enumerable.Range(0, count).Select(static _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))];
}
