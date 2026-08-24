// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace XiHan.Framework.Authentication.OAuth.Handlers;

/// <summary>
/// 企业微信登录处理器
/// </summary>
/// <remarks>
/// 企业微信换的是企业凭证而不是用户令牌，成员身份要再用授权码换一次，
/// 敏感资料还要凭 user_ticket 取第三次，因此三段响应合并后再跑声明映射。
/// </remarks>
public class WorkWeixinAuthenticationHandler : XiHanOAuthHandler<WorkWeixinAuthenticationOptions>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">选项监视器</param>
    /// <param name="logger">日志工厂</param>
    /// <param name="encoder">URL 编码器</param>
    public WorkWeixinAuthenticationHandler(
        IOptionsMonitor<WorkWeixinAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    private bool UsesAccountAuthorization => string.Equals(
        Options.AuthorizationEndpoint,
        OAuthProviderEndpoints.WorkWeixin.AccountAuthorization,
        StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 回调时把挪进回调地址的状态串还原到 state 参数上
    /// </summary>
    /// <returns>处理结果</returns>
    protected override Task<HandleRequestResult> HandleRemoteAuthenticateAsync()
    {
        WeixinShortState.Restore(Request);
        return base.HandleRemoteAuthenticateAsync();
    }

    /// <summary>
    /// 构造企业微信授权地址
    /// </summary>
    /// <param name="properties">认证属性</param>
    /// <param name="redirectUri">回调地址</param>
    /// <returns>授权地址</returns>
    protected override string BuildChallengeUrl(AuthenticationProperties properties, string redirectUri)
    {
        var state = Options.StateDataFormat.Protect(properties);
        Dictionary<string, string?> parameters;
        string? fragment = null;

        if (UsesAccountAuthorization)
        {
            (redirectUri, state) = WeixinShortState.Apply(redirectUri, state);
            var scopes = properties.GetParameter<ICollection<string>>(OAuthChallengeProperties.ScopeKey);

            parameters = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["appid"] = Options.ClientId,
                ["redirect_uri"] = redirectUri,
                ["response_type"] = "code",
                ["scope"] = scopes is null ? FormatScope() : FormatScope(scopes),
                ["agentid"] = Options.AgentId,
                ["state"] = state
            };
            fragment = OAuthProviderEndpoints.WeixinRedirectFragment;
        }
        else
        {
            parameters = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["login_type"] = "CorpApp",
                ["appid"] = Options.ClientId,
                ["agentid"] = Options.AgentId,
                ["redirect_uri"] = redirectUri,
                ["state"] = state
            };
        }

        foreach (var parameter in Options.AdditionalAuthorizationParameters)
        {
            parameters[parameter.Key] = parameter.Value;
        }

        return QueryHelpers.AddQueryString(Options.AuthorizationEndpoint, parameters) + fragment;
    }

    /// <summary>
    /// 换取企业凭证
    /// </summary>
    /// <param name="context">授权码交换上下文</param>
    /// <returns>令牌响应</returns>
    protected override async Task<OAuthTokenResponse> ExchangeCodeAsync(OAuthCodeExchangeContext context)
    {
        var tokenUrl = QueryHelpers.AddQueryString(Options.TokenEndpoint, new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["corpid"] = Options.ClientId,
            ["corpsecret"] = Options.ClientSecret
        });

        var payload = await GetJsonAsync(tokenUrl, "换取企业凭证");

        var errCode = ReadInt32(payload.RootElement, "errcode");
        if (errCode is not (null or 0))
        {
            var errMessage = ReadString(payload.RootElement, "errmsg") ?? "未知错误";
            payload.Dispose();
            Logger.LogError("{Scheme} 换取企业凭证失败，errcode={ErrCode}，errmsg={ErrMessage}。", Scheme.Name, errCode, errMessage);
            return OAuthTokenResponse.Failed(new AuthenticationFailureException($"换取企业凭证失败：errcode={errCode}，errmsg={errMessage}。"));
        }

        return OAuthTokenResponse.Success(payload);
    }

    /// <summary>
    /// 换取成员身份、补齐资料并生成认证票据
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
        var accessToken = tokens.AccessToken;
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);

        var identifierUrl = QueryHelpers.AddQueryString(Options.UserIdentificationEndpoint, new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["access_token"] = accessToken,
            ["code"] = Request.Query["code"].ToString()
        });

        string? memberId;
        string? openId;
        string? userTicket;

        using (var member = await GetJsonAsync(identifierUrl, "换取成员身份"))
        {
            EnsureErrCodeSuccess(member.RootElement, "换取成员身份");
            memberId = ReadString(member.RootElement, "userid");
            openId = ReadString(member.RootElement, "openid");
            userTicket = ReadString(member.RootElement, "user_ticket");
            CollectScalars(member.RootElement, fields);
        }

        // userid 在企业内唯一；非企业成员（如外部联系人）只有 openid
        var identifier = memberId ?? openId ?? throw MissingField("换取成员身份", "userid 与 openid");

        if (userTicket is not null)
        {
            await FillDetailAsync(accessToken, userTicket, fields);
        }

        if (Options.LoadMemberProfile && memberId is not null)
        {
            await FillMemberProfileAsync(accessToken, memberId, fields);
        }

        AddNameIdentifier(identity, identifier);

        using var payload = ToJsonDocument(fields);
        return await CreateTicketCoreAsync(identity, properties, tokens, payload.RootElement);
    }

    /// <summary>
    /// 格式化权限范围
    /// </summary>
    /// <param name="scopes">权限范围</param>
    /// <returns>以逗号分隔的权限范围</returns>
    protected override string FormatScope(IEnumerable<string> scopes)
    {
        return string.Join(',', scopes);
    }

    /// <summary>
    /// 不参与声明映射的字段：接口状态码，以及换取敏感信息用的一次性凭据
    /// </summary>
    private static readonly HashSet<string> NonProfileFields = new(StringComparer.Ordinal)
    {
        "errcode",
        "errmsg",
        "user_ticket"
    };

    private static void CollectScalars(JsonElement element, Dictionary<string, string> fields, bool preferExisting = false)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null or JsonValueKind.Undefined)
            {
                continue;
            }

            if (NonProfileFields.Contains(property.Name))
            {
                continue;
            }

            var value = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()
                : property.Value.ToString();

            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            if (preferExisting)
            {
                fields.TryAdd(property.Name, value);
            }
            else
            {
                fields[property.Name] = value;
            }
        }
    }

    private static JsonDocument ToJsonDocument(Dictionary<string, string> fields)
    {
        return JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(fields));
    }

    private async Task FillDetailAsync(string? accessToken, string userTicket, Dictionary<string, string> fields)
    {
        var detailUrl = QueryHelpers.AddQueryString(
            Options.UserInformationEndpoint,
            new Dictionary<string, string?>(StringComparer.Ordinal) { ["access_token"] = accessToken });

        using var detail = await PostJsonAsync(detailUrl, new { user_ticket = userTicket }, "拉取成员敏感信息");
        EnsureErrCodeSuccess(detail.RootElement, "拉取成员敏感信息");
        CollectScalars(detail.RootElement, fields);
    }

    private async Task FillMemberProfileAsync(string? accessToken, string memberId, Dictionary<string, string> fields)
    {
        var memberUrl = QueryHelpers.AddQueryString(Options.MemberEndpoint, new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["access_token"] = accessToken,
            ["userid"] = memberId
        });

        using var member = await GetJsonAsync(memberUrl, "读取通讯录成员资料");

        // 应用无通讯录权限或成员不在可见范围时返回非 0，此处跳过而不中断登录
        var errCode = ReadInt32(member.RootElement, "errcode");
        if (errCode is not (null or 0))
        {
            Logger.LogWarning(
                "{Scheme} 读取通讯录成员资料失败，errcode={ErrCode}，errmsg={ErrMessage}，姓名保持为空。",
                Scheme.Name,
                errCode,
                ReadString(member.RootElement, "errmsg") ?? "未知错误");
            return;
        }

        // 通讯录接口在无字段权限时会返回 gender="0" 这类占位值，先到先得避免覆盖掉已授权拿到的真实值
        CollectScalars(member.RootElement, fields, preferExisting: true);
    }
}
