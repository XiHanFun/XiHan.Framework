// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Google.Protobuf;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Primitives;
using Error = XiHan.Framework.Primitives.Error;

namespace XiHan.Framework.Plugins.Protocol.Mapping;

/// <summary>
/// 消息信封与协议 <c>Envelope</c> 之间的映射（RFC-0009）
/// </summary>
/// <remarks>
/// <c>time</c> 只有一条文法（<c>spec/plugin/invocation.md</c> §4，ADR-0293）：与 RFC-0009「信封」第 3 条相同，读入经 <see cref="TimestampText"/>，写出一律 UTC、七位小数、<c>+00:00</c>
/// </remarks>
internal static class PluginEnvelopeMapper
{
    /// <summary>
    /// 投递 → 协议信封
    /// </summary>
    /// <remarks>
    /// <c>time</c> 换算成 UTC 后按 <c>O</c> 格式写出（七位小数、<c>+00:00</c>），合 <see cref="TimestampText"/> 的文法；没有时间写空串
    /// </remarks>
    public static Envelope ToProto(string topic, MessageEnvelope envelope)
    {
        var proto = new Envelope
        {
            Topic = topic,
            Id = envelope.Id,
            Source = envelope.Source,
            Type = envelope.Type,
            SpecVersion = MessageEnvelope.SpecVersion,
            Subject = envelope.Subject ?? string.Empty,
            Time = envelope.Time?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
            DataContentType = envelope.DataContentType,
            Data = ByteString.CopyFrom(envelope.Data.Span),
        };
        foreach (var (name, value) in envelope.Extensions)
        {
            proto.Extensions[name] = value;
        }

        return proto;
    }

    /// <summary>
    /// 协议信封 → 消息信封
    /// </summary>
    /// <remarks>
    /// 属性不合法返回 <c>MESSAGING_VALIDATION_002</c>：<c>time</c> 不是空串又不合 <see cref="TimestampText"/> 的文法时消息键是 <c>messaging.envelope.invalid_time</c>（参数 <c>time</c> 是原文），读出的是 UTC 时刻；其余属性不合法时是 <c>messaging.envelope.invalid</c>
    /// </remarks>
    public static Result<MessageEnvelope> FromProto(Envelope proto)
    {
        // 空串是没有时间；不是空串就只认一条文法，不换别的格式再试，也不当作没有：以前的通用解析把没有偏移的按 UTC、空格分隔与只有日期的都读得出
        DateTimeOffset? time = null;
        if (proto.Time.Length > 0)
        {
            if (!TimestampText.TryParse(proto.Time, out var parsed))
            {
                return Result.Failure<MessageEnvelope>(Error.Validation(MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalidTime, MessageArguments.Of(("time", proto.Time))));
            }

            time = parsed;
        }

        try
        {
            var envelope = new MessageEnvelope
            {
                Id = proto.Id,
                Source = proto.Source,
                Type = proto.Type,
                Subject = string.IsNullOrEmpty(proto.Subject) ? null : proto.Subject,
                Time = time,
                Data = proto.Data.Memory,
                Extensions = proto.Extensions.ToDictionary(static e => e.Key, static e => e.Value, StringComparer.Ordinal),
            };
            if (!string.IsNullOrEmpty(proto.DataContentType))
            {
                envelope = envelope with { DataContentType = proto.DataContentType };
            }

            return Result.Success(envelope);
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<MessageEnvelope>(Error.Validation(MessagingErrors.InvalidEnvelope, MessagingMessages.EnvelopeInvalid, MessageArguments.Of(("reason", exception.Message))));
        }
    }
}
