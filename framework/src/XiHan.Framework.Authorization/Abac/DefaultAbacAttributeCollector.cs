#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultAbacAttributeCollector
// Guid:5f407a23-2f87-4e1c-ab95-96f9d2711e90
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/10 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace XiHan.Framework.Authorization.Abac;

/// <summary>
/// 默认 ABAC 属性收集器
/// </summary>
public class DefaultAbacAttributeCollector : IAbacAttributeCollector
{
    private static readonly string[] UserIdClaimTypes =
    [
        ClaimTypes.NameIdentifier,
        "sub",
        "userid",
        "user_id"
    ];

    private static readonly string[] TenantIdClaimTypes =
    [
        "tenantid",
        "tenant_id",
        "tenant"
    ];

    /// <inheritdoc />
    public Task<AbacAttributeSet> CollectAsync(
        ClaimsPrincipal principal,
        object? resource,
        string permissionCode,
        string policyCode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = new AbacAttributeSet();
        CollectSubjectAttributes(principal, result.SubjectAttributes);
        CollectResourceAttributes(resource, result.ResourceAttributes);
        CollectEnvironmentAttributes(resource, permissionCode, policyCode, result.EnvironmentAttributes);

        return Task.FromResult(result);
    }

    private static void CollectSubjectAttributes(ClaimsPrincipal principal, IDictionary<string, object?> attributes)
    {
        var userId = ResolveClaimValue(principal, UserIdClaimTypes);
        var tenantId = ResolveClaimValue(principal, TenantIdClaimTypes);
        var roles = principal.Claims
            .Where(claim =>
                string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
                || string.Equals(claim.Type, "role", StringComparison.OrdinalIgnoreCase))
            .Select(static claim => claim.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        TrySetAttribute(attributes, "user_id", userId);
        TrySetAttribute(attributes, "tenant_id", tenantId);
        TrySetAttribute(attributes, "roles", roles);
        TrySetAttribute(attributes, "is_authenticated", principal.Identity?.IsAuthenticated == true);

        foreach (var claim in principal.Claims)
        {
            if (string.IsNullOrWhiteSpace(claim.Type))
            {
                continue;
            }

            var key = $"claim.{NormalizeKey(claim.Type)}";
            if (!attributes.ContainsKey(key))
            {
                attributes[key] = claim.Value;
            }
        }
    }

    private static void CollectResourceAttributes(object? resource, IDictionary<string, object?> attributes)
    {
        if (resource is null)
        {
            return;
        }

        TrySetAttribute(attributes, "resource_type", resource.GetType().Name);

        if (TryResolveHttpContext(resource, out var httpContext))
        {
            foreach (var routeValue in httpContext.Request.RouteValues)
            {
                var routeKey = $"route.{NormalizeKey(routeValue.Key)}";
                TrySetAttribute(attributes, routeKey, routeValue.Value?.ToString());
            }

            foreach (var queryItem in httpContext.Request.Query)
            {
                var queryKey = $"query.{NormalizeKey(queryItem.Key)}";
                TrySetAttribute(attributes, queryKey, queryItem.Value.ToString());
            }

            TrySetAttribute(attributes, "http.method", httpContext.Request.Method);
            TrySetAttribute(attributes, "http.path", httpContext.Request.Path.Value);
            TrySetAttribute(attributes, "http.client_ip", httpContext.Connection.RemoteIpAddress?.ToString());
        }

        if (resource is string || resource.GetType().IsPrimitive)
        {
            TrySetAttribute(attributes, "resource_value", resource);
            return;
        }

        foreach (var property in resource.GetType()
                     .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                     .Where(static property => property.CanRead && property.GetIndexParameters().Length == 0))
        {
            object? value;
            try
            {
                value = property.GetValue(resource);
            }
            catch
            {
                continue;
            }

            TrySetAttribute(attributes, NormalizeKey(property.Name), value);
        }
    }

    private static void CollectEnvironmentAttributes(
        object? resource,
        string permissionCode,
        string policyCode,
        IDictionary<string, object?> attributes)
    {
        var now = DateTimeOffset.UtcNow;
        TrySetAttribute(attributes, "permission_code", permissionCode);
        TrySetAttribute(attributes, "policy_code", policyCode);
        TrySetAttribute(attributes, "utc_now", now);
        TrySetAttribute(attributes, "utc_hour", now.Hour);
        TrySetAttribute(attributes, "day_of_week", now.DayOfWeek.ToString());

        if (!TryResolveHttpContext(resource, out var httpContext))
        {
            return;
        }

        TrySetAttribute(attributes, "client_ip", httpContext.Connection.RemoteIpAddress?.ToString());
        TrySetAttribute(attributes, "request_path", httpContext.Request.Path.Value);
        TrySetAttribute(attributes, "request_method", httpContext.Request.Method);
        TrySetAttribute(attributes, "user_agent", httpContext.Request.Headers.UserAgent.ToString());
    }

    private static bool TryResolveHttpContext(object? resource, out HttpContext httpContext)
    {
        httpContext = default!;
        switch (resource)
        {
            case HttpContext directHttpContext:
                httpContext = directHttpContext;
                return true;
            case null:
                return false;
        }

        var property = resource.GetType().GetProperty("HttpContext", BindingFlags.Public | BindingFlags.Instance);
        if (property?.GetValue(resource) is HttpContext resolvedHttpContext)
        {
            httpContext = resolvedHttpContext;
            return true;
        }

        return false;
    }

    private static string ResolveClaimValue(ClaimsPrincipal principal, IEnumerable<string> claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.Claims
                .FirstOrDefault(claim => string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase))
                ?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static void TrySetAttribute(IDictionary<string, object?> attributes, string key, object? value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var normalizedKey = NormalizeKey(key);
        if (!attributes.ContainsKey(normalizedKey))
        {
            attributes[normalizedKey] = value;
        }
    }

    private static string NormalizeKey(string key)
    {
        return key.Trim().Replace(":", "_", StringComparison.Ordinal).ToLowerInvariant();
    }
}
