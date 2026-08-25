// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Authentication.OAuth.Handlers;

/// <summary>
/// 内置提供商处理器基类
/// </summary>
/// <remarks>
/// 默认实现覆盖「用 Bearer 令牌 GET 用户信息接口、拿返回的 JSON 跑声明映射」这一种形态，
/// 走这条路的提供商可以直接用本类，不必再写处理器。形态不同的提供商覆写对应方法。
/// </remarks>
/// <typeparam name="TOptions">提供商选项类型</typeparam>
public class XiHanOAuthHandler<TOptions> : OAuthHandler<TOptions>
    where TOptions : XiHanOAuthProviderOptions, new()
{
    private static readonly JsonSerializerOptions BodySerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">选项监视器</param>
    /// <param name="logger">日志工厂</param>
    /// <param name="encoder">URL 编码器</param>
    public XiHanOAuthHandler(IOptionsMonitor<TOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <summary>
    /// 拉取用户信息并生成认证票据
    /// </summary>
    /// <param name="identity">声明标识</param>
    /// <param name="properties">认证属性</param>
    /// <param name="tokens">令牌响应</param>
    /// <returns>认证票据</returns>
    protected override async Task<AuthenticationTicket> CreateTicketAsync(
        ClaimsIdentity identity,
        AuthenticationProperties properties,
        OAuthTokenResponse tokens)
    {
        using var payload = await GetBearerJsonAsync(Options.UserInformationEndpoint, tokens.AccessToken, "拉取用户信息");
        return await CreateTicketCoreAsync(identity, properties, tokens, payload.RootElement);
    }

    /// <summary>
    /// 声明映射跑完之后、票据生成之前的补充处理
    /// </summary>
    /// <remarks>需要额外调一次接口补声明（如 GitHub 的私密邮箱）时覆写。</remarks>
    /// <param name="identity">声明标识</param>
    /// <param name="tokens">令牌响应</param>
    /// <param name="payload">用户信息 JSON</param>
    protected virtual Task AfterClaimActionsAsync(ClaimsIdentity identity, OAuthTokenResponse tokens, JsonElement payload)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 跑声明映射并生成认证票据
    /// </summary>
    /// <param name="identity">声明标识</param>
    /// <param name="properties">认证属性</param>
    /// <param name="tokens">令牌响应</param>
    /// <param name="payload">用户信息 JSON</param>
    /// <returns>认证票据</returns>
    protected async Task<AuthenticationTicket> CreateTicketCoreAsync(
        ClaimsIdentity identity,
        AuthenticationProperties properties,
        OAuthTokenResponse tokens,
        JsonElement payload)
    {
        var principal = new ClaimsPrincipal(identity);
        var context = new OAuthCreatingTicketContext(principal, properties, Context, Scheme, Options, Backchannel, tokens, payload);
        context.RunClaimActions();

        await AfterClaimActionsAsync(identity, tokens, payload);
        await Events.CreatingTicket(context);

        return new AuthenticationTicket(context.Principal!, context.Properties, Scheme.Name);
    }

    /// <summary>
    /// 添加登录标识声明
    /// </summary>
    /// <param name="identity">声明标识</param>
    /// <param name="identifier">登录标识</param>
    protected void AddNameIdentifier(ClaimsIdentity identity, string identifier)
    {
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, identifier, ClaimValueTypes.String, Options.ClaimsIssuer));
    }

    /// <summary>
    /// 用 Bearer 令牌 GET 一个返回 JSON 的接口
    /// </summary>
    /// <param name="endpoint">接口地址</param>
    /// <param name="accessToken">访问令牌</param>
    /// <param name="operation">操作描述，用于异常与日志</param>
    /// <returns>响应 JSON 文档</returns>
    protected Task<JsonDocument> GetBearerJsonAsync(string endpoint, string? accessToken, string operation)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return SendJsonAsync(request, operation);
    }

    /// <summary>
    /// GET 一个返回 JSON 的接口
    /// </summary>
    /// <param name="endpoint">接口地址</param>
    /// <param name="operation">操作描述，用于异常与日志</param>
    /// <returns>响应 JSON 文档</returns>
    protected Task<JsonDocument> GetJsonAsync(string endpoint, string operation)
    {
        return SendJsonAsync(new HttpRequestMessage(HttpMethod.Get, endpoint), operation);
    }

    /// <summary>
    /// 以 JSON 体 POST 一个返回 JSON 的接口
    /// </summary>
    /// <param name="endpoint">接口地址</param>
    /// <param name="body">请求体</param>
    /// <param name="operation">操作描述，用于异常与日志</param>
    /// <returns>响应 JSON 文档</returns>
    protected Task<JsonDocument> PostJsonAsync(string endpoint, object body, string operation)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, BodySerializerOptions), Encoding.UTF8, "application/json")
        };
        return SendJsonAsync(request, operation);
    }

    /// <summary>
    /// 以表单体 POST 一个返回 JSON 的接口
    /// </summary>
    /// <param name="endpoint">接口地址</param>
    /// <param name="form">表单字段</param>
    /// <param name="operation">操作描述，用于异常与日志</param>
    /// <returns>响应 JSON 文档</returns>
    protected Task<JsonDocument> PostFormAsync(string endpoint, IEnumerable<KeyValuePair<string, string>> form, string operation)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new FormUrlEncodedContent(form)
        };
        return SendJsonAsync(request, operation);
    }

    /// <summary>
    /// 发起请求并解析 JSON 响应，HTTP 失败或响应不是 JSON 时抛出
    /// </summary>
    /// <param name="request">请求，调用后由本方法释放</param>
    /// <param name="operation">操作描述，用于异常与日志</param>
    /// <returns>响应 JSON 文档</returns>
    protected async Task<JsonDocument> SendJsonAsync(HttpRequestMessage request, string operation)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using (request)
        {
            using var response = await Backchannel.SendAsync(request, Context.RequestAborted);
            var content = await response.Content.ReadAsStringAsync(Context.RequestAborted);

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogError("{Scheme} {Operation}失败，HTTP 状态 {Status}，响应 {Body}。", Scheme.Name, operation, response.StatusCode, RedactForLog(content));
                throw new AuthenticationFailureException($"{operation}失败，远端返回 {(int)response.StatusCode}。");
            }

            try
            {
                return JsonDocument.Parse(content);
            }
            catch (JsonException exception)
            {
                Logger.LogError(exception, "{Scheme} {Operation}返回了非 JSON 内容：{Body}。", Scheme.Name, operation, RedactForLog(content));
                throw new AuthenticationFailureException($"{operation}失败，响应不是合法的 JSON。", exception);
            }
        }
    }

    /// <summary>
    /// 日志里记录响应体的长度上限
    /// </summary>
    private const int MaxLoggedBodyLength = 512;

    /// <summary>
    /// 记日志前需要抹掉取值的字段名
    /// </summary>
    /// <remarks>令牌接口的响应体本身就是凭据载体，出错日志常被汇聚到访问控制远弱于凭据本身的日志平台。</remarks>
    private static readonly string[] SensitiveJsonKeys =
    [
        "access_token",
        "refresh_token",
        "accessToken",
        "refreshToken",
        "user_ticket",
        "client_secret",
        "corpsecret",
        "secret"
    ];

    /// <summary>
    /// 抹掉响应体里的凭据取值并截断，供日志使用
    /// </summary>
    /// <param name="content">响应体原文</param>
    /// <returns>可安全写入日志的文本</returns>
    protected static string RedactForLog(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        var redacted = content;
        foreach (var key in SensitiveJsonKeys)
        {
            redacted = Regex.Replace(
                redacted,
                "(\"" + Regex.Escape(key) + "\"\\s*:\\s*\")[^\"]*(\")",
                "${1}***${2}",
                RegexOptions.IgnoreCase,
                TimeSpan.FromSeconds(1));
        }

        return redacted.Length > MaxLoggedBodyLength
            ? string.Concat(redacted.AsSpan(0, MaxLoggedBodyLength), "…（已截断）")
            : redacted;
    }

    /// <summary>
    /// 校验微信系接口返回的 errcode，非 0 时抛出
    /// </summary>
    /// <param name="root">响应 JSON 根节点</param>
    /// <param name="operation">操作描述，用于异常与日志</param>
    protected void EnsureErrCodeSuccess(JsonElement root, string operation)
    {
        var errCode = ReadInt32(root, "errcode");
        if (errCode is null or 0)
        {
            return;
        }

        var errMessage = ReadString(root, "errmsg") ?? "未知错误";
        Logger.LogError("{Scheme} {Operation}失败，errcode={ErrCode}，errmsg={ErrMessage}。", Scheme.Name, operation, errCode, errMessage);
        throw new AuthenticationFailureException($"{operation}失败：errcode={errCode}，errmsg={errMessage}。");
    }

    /// <summary>
    /// 抛出提供商响应缺少必要字段的异常
    /// </summary>
    /// <param name="operation">操作描述</param>
    /// <param name="fields">缺少的字段</param>
    /// <returns>异常实例，由调用方 throw</returns>
    protected AuthenticationFailureException MissingField(string operation, string fields)
    {
        Logger.LogError("{Scheme} {Operation}失败，响应缺少 {Fields}。", Scheme.Name, operation, fields);
        return new AuthenticationFailureException($"{operation}失败：响应缺少 {fields}。");
    }

    /// <summary>
    /// 读取 JSON 对象中的字符串属性
    /// </summary>
    /// <param name="element">JSON 对象</param>
    /// <param name="propertyName">属性名</param>
    /// <returns>属性值，缺失或为空返回 null</returns>
    protected static string? ReadString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
            _ => null
        };

        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>
    /// 读取 JSON 对象中的整数属性
    /// </summary>
    /// <param name="element">JSON 对象</param>
    /// <param name="propertyName">属性名</param>
    /// <returns>属性值，缺失或类型不符返回 null</returns>
    protected static int? ReadInt32(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;
    }

    /// <summary>
    /// 读取 JSON 对象中的子对象
    /// </summary>
    /// <param name="element">JSON 对象</param>
    /// <param name="propertyName">属性名</param>
    /// <returns>子对象，缺失或类型不符返回默认值</returns>
    protected static JsonElement ReadObject(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.Object
            ? value
            : default;
    }
}
