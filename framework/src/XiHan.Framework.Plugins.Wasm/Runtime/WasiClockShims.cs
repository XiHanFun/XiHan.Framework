// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using Wasmtime;

namespace XiHan.Framework.Plugins.Wasm.Runtime;

/// <summary>
/// 替换 WASI 的 <c>poll_oneoff</c> 与 <c>clock_time_get</c>
/// </summary>
/// <remarks>
/// epoch 中断只在客体执行 Wasm 代码时检查，阻塞在宿主函数里的调用中断不了；WASI 里唯一能无限阻塞的是 <c>poll_oneoff</c> 的时钟订阅（睡眠）。这里的实现最多睡到当前调用的截止时间，调用被取消时立即醒来；没睡到订阅的时刻就把 epoch 截止设为当下，客体回到 Wasm 代码即被中断，与计算超时同一个结果（ADR-0186）。
/// 时钟订阅的绝对时刻按本类的 <c>clock_time_get</c> 解释，所以两者一起替换：实时钟取系统时间，单调钟取 <see cref="Stopwatch"/>。文件描述符订阅（插件只有空的 stdin 与预开放的目录）一律立即就绪
/// </remarks>
internal sealed class WasiClockShims(WasmHostFunctions functions, WasmHostContext host)
{
    /// <summary>
    /// WASI 模块名
    /// </summary>
    public const string Module = "wasi_snapshot_preview1";

    /// <summary>
    /// 订阅结构的字节数
    /// </summary>
    private const int SubscriptionSize = 48;

    /// <summary>
    /// 事件结构的字节数
    /// </summary>
    private const int EventSize = 32;

    /// <summary>
    /// errno：成功
    /// </summary>
    private const int Success = 0;

    /// <summary>
    /// errno：内存地址越界
    /// </summary>
    private const int Fault = 21;

    /// <summary>
    /// errno：参数不合法
    /// </summary>
    private const int Invalid = 28;

    /// <summary>
    /// 实时钟
    /// </summary>
    private const int RealtimeClock = 0;

    /// <summary>
    /// 单调钟
    /// </summary>
    private const int MonotonicClock = 1;

    /// <summary>
    /// <c>poll_oneoff(in, out, nsubscriptions, nevents) -> errno</c>
    /// </summary>
    public int PollOneoff(Caller caller, int input, int output, int count, int eventsPointer)
    {
        var memory = caller.GetMemory("memory");
        if (count <= 0)
        {
            return Invalid;
        }

        if (memory is null || !Fits(memory, input, (long)count * SubscriptionSize) || !Fits(memory, output, (long)count * EventSize) || !Fits(memory, eventsPointer, 4))
        {
            return Fault;
        }

        // 先找立即就绪的：文件描述符订阅、时钟不认识的订阅（以错误事件交回），有就不睡
        var immediate = new List<(long UserData, short Error, byte Type)>();
        var clocks = new List<(long UserData, long WaitNanoseconds)>();
        for (var index = 0; index < count; index++)
        {
            var subscription = input + ((long)index * SubscriptionSize);
            var userData = memory.ReadInt64(subscription);
            var tag = memory.ReadByte(subscription + 8);
            if (tag != 0)
            {
                immediate.Add((userData, tag is 1 or 2 ? (short)Success : (short)Invalid, tag));
                continue;
            }

            var id = memory.ReadInt32(subscription + 16);
            if (id is not (RealtimeClock or MonotonicClock))
            {
                immediate.Add((userData, Invalid, 0));
                continue;
            }

            var timeout = memory.ReadInt64(subscription + 24);
            var absolute = (memory.ReadInt16(subscription + 40) & 1) != 0;
            var wait = absolute ? timeout - Now(id) : timeout;
            clocks.Add((userData, wait < 0 ? 0 : wait));
        }

        if (immediate.Count > 0)
        {
            return WriteEvents(memory, output, eventsPointer, immediate);
        }

        // 只有时钟订阅：睡到最早的那个，但不越过当前调用的截止时间
        var earliest = clocks.Min(static c => c.WaitNanoseconds);
        var cap = Cap();
        var fires = earliest <= cap.Ticks * 100;
        var woken = Sleep(fires ? TimeSpan.FromTicks(earliest / 100) : cap);
        if (!fires || woken)
        {
            // 截止时间到了或调用被取消：不报事件，epoch 截止设为当下，客体一回到 Wasm 代码就被中断；
            // 只靠刻度追上截止时间不可靠，计时器的粒度（Windows 上约 15.6 毫秒）会让刻度落后于墙钟
            caller.Store.SetEpochDeadline(0);
            memory.WriteInt32(eventsPointer, 0);
            return Success;
        }

        return WriteEvents(memory, output, eventsPointer, [.. clocks.Where(c => c.WaitNanoseconds <= earliest).Select(static c => (c.UserData, (short)Success, (byte)0))]);
    }

    /// <summary>
    /// <c>clock_time_get(id, precision, time) -> errno</c>
    /// </summary>
    public static int ClockTimeGet(Caller caller, int id, long precision, int result)
    {
        var memory = caller.GetMemory("memory");
        if (id is not (RealtimeClock or MonotonicClock))
        {
            return Invalid;
        }

        if (memory is null || !Fits(memory, result, 8))
        {
            return Fault;
        }

        memory.WriteInt64(result, Now(id));
        return Success;
    }

    /// <summary>
    /// 时钟的当前读数（纳秒）
    /// </summary>
    private static long Now(int id)
    {
        if (id == RealtimeClock)
        {
            return (DateTimeOffset.UtcNow - DateTimeOffset.UnixEpoch).Ticks * 100;
        }

        // 分两段换算，免得时间戳乘以 10^9 溢出
        var timestamp = Stopwatch.GetTimestamp();
        return (timestamp / Stopwatch.Frequency * 1_000_000_000) + (timestamp % Stopwatch.Frequency * 1_000_000_000 / Stopwatch.Frequency);
    }

    /// <summary>
    /// 这一次最多睡多久
    /// </summary>
    /// <remarks>
    /// 到当前调用的截止时间，没有调用（如实例化时的 <c>_initialize</c>）或截止时间更远时取调用时限
    /// </remarks>
    private TimeSpan Cap()
    {
        var callTimeout = host.Options.CallTimeout;
        var remaining = functions.CurrentDeadline is { } deadline ? deadline - host.Clock.GetUtcNow() : callTimeout;
        if (remaining > callTimeout)
        {
            remaining = callTimeout;
        }

        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    /// <summary>
    /// 在执行线程上睡眠，调用被取消时立即醒来
    /// </summary>
    /// <remarks>
    /// 返回是否因取消而提前醒来
    /// </remarks>
    private bool Sleep(TimeSpan duration)
    {
        var cancellation = functions.CurrentCancellation;
        if (cancellation.IsCancellationRequested)
        {
            return true;
        }

        if (duration <= TimeSpan.Zero)
        {
            return false;
        }

        if (cancellation.CanBeCanceled)
        {
            return cancellation.WaitHandle.WaitOne(duration);
        }

        Thread.Sleep(duration);
        return false;
    }

    /// <summary>
    /// 写事件与事件数
    /// </summary>
    private static int WriteEvents(Memory memory, int output, int eventsPointer, List<(long UserData, short Error, byte Type)> events)
    {
        for (var index = 0; index < events.Count; index++)
        {
            var target = output + ((long)index * EventSize);
            var (userData, error, type) = events[index];
            memory.GetSpan(target, EventSize).Clear();
            memory.WriteInt64(target, userData);
            memory.WriteInt16(target + 8, error);
            memory.WriteByte(target + 10, type);
        }

        memory.WriteInt32(eventsPointer, events.Count);
        return Success;
    }

    /// <summary>
    /// <paramref name="pointer"/> 起的 <paramref name="length"/> 个字节是否都在线性内存里
    /// </summary>
    private static bool Fits(Memory memory, long pointer, long length) => pointer >= 0 && pointer + length <= memory.GetLength();
}
