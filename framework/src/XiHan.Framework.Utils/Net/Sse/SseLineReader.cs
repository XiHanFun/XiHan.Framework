// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Buffers;
using System.Text;

namespace XiHan.Framework.Utils.Net.Sse;

/// <summary>
/// Reads UTF-8 lines without buffering more than the configured line limit.
/// </summary>
internal sealed class SseLineReader(Stream stream)
{
    private readonly byte[] _readBuffer = new byte[4096];
    private readonly ArrayBufferWriter<byte> _lineBuffer = new();
    private int _readPosition;
    private int _readLength;
    private bool _isFirstLine = true;

    public async ValueTask<SseLine?> ReadLineAsync(int maxLineBytes, CancellationToken cancellationToken)
    {
        while (true)
        {
            if (_readPosition == _readLength)
            {
                _readLength = await stream.ReadAsync(_readBuffer, cancellationToken).ConfigureAwait(false);
                _readPosition = 0;
                if (_readLength == 0)
                {
                    return _lineBuffer.WrittenCount == 0 ? null : BuildLine();
                }
            }

            var newlinePosition = Array.IndexOf(_readBuffer, (byte)'\n', _readPosition, _readLength - _readPosition);
            var segmentEnd = newlinePosition >= 0 ? newlinePosition : _readLength;
            var segmentLength = segmentEnd - _readPosition;
            if (_lineBuffer.WrittenCount + segmentLength > maxLineBytes)
            {
                throw new InvalidDataException($"SSE 行超过最大长度 {maxLineBytes} 字节。");
            }

            if (segmentLength > 0)
            {
                _readBuffer.AsSpan(_readPosition, segmentLength).CopyTo(_lineBuffer.GetSpan(segmentLength));
                _lineBuffer.Advance(segmentLength);
            }

            _readPosition = newlinePosition >= 0 ? newlinePosition + 1 : _readLength;
            if (newlinePosition >= 0)
            {
                return BuildLine();
            }
        }
    }

    private SseLine BuildLine()
    {
        var bytes = _lineBuffer.WrittenSpan;
        var byteCount = bytes.Length;
        if (bytes.Length > 0 && bytes[^1] == (byte)'\r')
        {
            bytes = bytes[..^1];
        }

        var text = Encoding.UTF8.GetString(bytes);
        if (_isFirstLine)
        {
            if (text.StartsWith('\uFEFF'))
            {
                text = text[1..];
            }

            _isFirstLine = false;
        }

        _lineBuffer.Clear();
        return new SseLine(text, byteCount);
    }
}

internal readonly record struct SseLine(string Text, int ByteCount);
