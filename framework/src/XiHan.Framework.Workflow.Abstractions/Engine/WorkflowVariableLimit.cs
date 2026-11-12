// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Text.Json;
using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Engine;

/// <summary>
/// 实例变量的大小上限（ADR-0182）
/// </summary>
/// <remarks>
/// 大小是变量对象按存储的写法（缺省转义）序列化成 UTF-8 的字节数，只计数、不留副本
/// </remarks>
internal static class WorkflowVariableLimit
{
    /// <summary>
    /// 序列化成 UTF-8 JSON 的字节数
    /// </summary>
    public static long Measure(JsonNode? value)
    {
        if (value is null)
        {
            return 4;
        }

        var counter = new CountingBufferWriter();
        using (var writer = new Utf8JsonWriter(counter))
        {
            value.WriteTo(writer);
        }

        return counter.Count;
    }

    /// <summary>
    /// 变量超过上限时的错误
    /// </summary>
    /// <remarks>
    /// <paramref name="input"/> 为真时是调用方给的变量（启动、改变量、跳转），返回校验错误；否则是节点写出的，返回节点故障。没超过为空
    /// </remarks>
    public static Error? Check(JsonObject variables, WorkflowOptions options, bool input)
    {
        var size = Measure(variables);
        if (size <= options.MaxVariablesBytes)
        {
            return null;
        }

        var details = MessageArguments.Of(("size", size), ("max", options.MaxVariablesBytes));
        return input
            ? Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.VariablesTooLarge, details)
            : Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.VariablesTooLarge, details: details);
    }

    /// <summary>
    /// 只计数的缓冲写入器
    /// </summary>
    /// <remarks>
    /// 每次交出同一块暂存区，写进去的内容不保留
    /// </remarks>
    private sealed class CountingBufferWriter : IBufferWriter<byte>
    {
        /// <summary>
        /// 暂存区
        /// </summary>
        private byte[] _scratch = new byte[4096];

        /// <summary>
        /// 已写入的字节数
        /// </summary>
        public long Count { get; private set; }

        /// <inheritdoc />
        public void Advance(int count) => Count += count;

        /// <inheritdoc />
        public Memory<byte> GetMemory(int sizeHint = 0) => Scratch(sizeHint);

        /// <inheritdoc />
        public Span<byte> GetSpan(int sizeHint = 0) => Scratch(sizeHint);

        /// <summary>
        /// 不小于 <paramref name="sizeHint"/> 的暂存区
        /// </summary>
        private byte[] Scratch(int sizeHint)
        {
            if (sizeHint > _scratch.Length)
            {
                _scratch = new byte[sizeHint];
            }

            return _scratch;
        }
    }
}
