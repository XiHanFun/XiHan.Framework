// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;

namespace XiHan.Framework.Security.WebAuthn;

/// <summary>
/// 解出的客户端数据（<c>CollectedClientData</c>）
/// </summary>
/// <param name="Type">仪式类型：<c>webauthn.create</c> 或 <c>webauthn.get</c></param>
/// <param name="Challenge">挑战的字节</param>
/// <param name="Origin">发起的来源</param>
/// <param name="CrossOrigin">是否在跨源的 iframe 里发起</param>
internal sealed record ClientData(string Type, byte[] Challenge, string Origin, bool CrossOrigin)
{
    /// <summary>
    /// 从 UTF-8 的 JSON 解出
    /// </summary>
    /// <param name="json">客户端数据的原始字节</param>
    /// <param name="clientData">解出的数据</param>
    /// <returns>不是 JSON 对象、缺 <c>type</c> / <c>challenge</c> / <c>origin</c>、字段类型不对时为假</returns>
    public static bool TryParse(byte[] json, out ClientData? clientData)
    {
        clientData = null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !TryString(root, "type", out var type)
                || !TryString(root, "challenge", out var challenge)
                || !TryString(root, "origin", out var origin)
                || !Base64UrlText.TryDecode(challenge, out var challengeBytes))
            {
                return false;
            }

            var crossOrigin = false;
            if (root.TryGetProperty("crossOrigin", out var crossOriginElement))
            {
                // 字段可选，出现时只能是布尔值
                if (crossOriginElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    return false;
                }

                crossOrigin = crossOriginElement.GetBoolean();
            }

            clientData = new ClientData(type, challengeBytes, origin, crossOrigin);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// 读一个字符串字段
    /// </summary>
    private static bool TryString(JsonElement root, string name, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString() ?? string.Empty;
        return true;
    }
}
