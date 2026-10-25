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
/// 消息信封与协议 <c>Envelope</c> 之间的映射（RFC-0009）。
/// </summary>
internal static class PluginEnvelopeMapper
{
    /// <summary>
    /// 投递 → 协议信封。
    /// </summary>
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
    /// 协议信封 → 消息信封；属性不合法返回 <c>MESSAGING_VALIDATION_002</c>。
    /// </summary>
    public static Result<MessageEnvelope> FromProto(Envelope proto)
    {
        try
        {
            DateTimeOffset? time = null;
            if (!string.IsNullOrEmpty(proto.Time))
            {
                time = DateTimeOffset.TryParse(proto.Time, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
                    ? parsed
                    : throw new ArgumentException($"The event time '{proto.Time}' is not an RFC 3339 timestamp.");
            }

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
