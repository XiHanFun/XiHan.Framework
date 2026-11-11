// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Formats.Cbor;

namespace XiHan.Framework.Security.WebAuthn;

/// <summary>
/// 解出的证明对象
/// </summary>
/// <param name="Format">证明格式（<c>fmt</c>）</param>
/// <param name="StatementEmpty">证明声明（<c>attStmt</c>）是否为空映射</param>
/// <param name="AuthenticatorData">认证器数据（<c>authData</c>）</param>
internal sealed record AttestationObject(string Format, bool StatementEmpty, byte[] AuthenticatorData)
{
    /// <summary>
    /// 从 CBOR 解出
    /// </summary>
    /// <param name="encoded">证明对象的字节</param>
    /// <param name="result">解出的对象</param>
    /// <returns>不是 CBOR 映射、缺 <c>fmt</c> / <c>attStmt</c> / <c>authData</c> 或有多余字节时为假</returns>
    public static bool TryParse(byte[] encoded, out AttestationObject? result)
    {
        result = null;
        string? format = null;
        bool? statementEmpty = null;
        byte[]? authenticatorData = null;
        try
        {
            var reader = new CborReader(encoded, CborConformanceMode.Strict);
            reader.ReadStartMap();
            while (reader.PeekState() != CborReaderState.EndMap)
            {
                // 键都是文本；不认识的键跳过它的值
                switch (reader.ReadTextString())
                {
                    case "fmt":
                        format = reader.ReadTextString();
                        break;
                    case "attStmt":
                        // 只关心是否为空：证明声明不校验，none 格式要求为空映射
                        if (reader.PeekState() != CborReaderState.StartMap)
                        {
                            return false;
                        }

                        var count = reader.ReadStartMap();
                        var empty = count == 0 || reader.PeekState() == CborReaderState.EndMap;
                        while (reader.PeekState() != CborReaderState.EndMap)
                        {
                            reader.SkipValue();
                        }

                        reader.ReadEndMap();
                        statementEmpty = empty;
                        break;
                    case "authData":
                        authenticatorData = reader.ReadByteString();
                        break;
                    default:
                        reader.SkipValue();
                        break;
                }
            }

            reader.ReadEndMap();
            if (reader.BytesRemaining != 0)
            {
                return false;
            }
        }
        catch (Exception exception) when (exception is CborContentException or InvalidOperationException)
        {
            return false;
        }

        if (string.IsNullOrEmpty(format) || statementEmpty is not { } isEmpty || authenticatorData is null)
        {
            return false;
        }

        result = new AttestationObject(format, isEmpty, authenticatorData);
        return true;
    }
}
