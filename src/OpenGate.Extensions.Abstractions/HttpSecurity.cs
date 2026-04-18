using System.Net;
using System.Net.Sockets;

namespace OpenGate.Extensions.Abstractions;

/// <summary>
/// Helpers for guarding administrator-supplied URLs against trivial
/// server-side request forgery (SSRF) by rejecting hosts that resolve to
/// loopback, link-local, multicast, or private network ranges.
/// </summary>
public static class HttpSecurity
{
    /// <summary>
    /// Validates that <paramref name="url"/> is an absolute http/https URL
    /// whose host does not resolve to a private/loopback/link-local address
    /// unless the deployment explicitly opts in via <paramref name="allowPrivateHosts"/>.
    /// </summary>
    public static bool TryValidateOutboundUrl(string url, bool allowPrivateHosts, out Uri? uri, out string? error)
    {
        uri = null;
        error = null;

        if (string.IsNullOrWhiteSpace(url))
        {
            error = "URL must not be empty.";
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            error = "URL must be an absolute URI.";
            return false;
        }

        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
        {
            error = "URL must use http or https.";
            return false;
        }

        if (allowPrivateHosts)
        {
            uri = parsed;
            return true;
        }

        try
        {
            IPAddress[] addresses;
            if (IPAddress.TryParse(parsed.Host, out var literal))
            {
                addresses = [literal];
            }
            else
            {
                addresses = Dns.GetHostAddresses(parsed.Host);
            }

            foreach (var address in addresses)
            {
                if (IsPrivate(address))
                {
                    error = "URL host resolves to a private or loopback address.";
                    return false;
                }
            }
        }
        catch (SocketException ex)
        {
            error = $"DNS resolution failed: {ex.Message}";
            return false;
        }

        uri = parsed;
        return true;
    }

    /// <summary>
    /// Returns true when the given address is considered private, loopback,
    /// link-local, multicast or otherwise non-routable on the public internet.
    /// </summary>
    public static bool IsPrivate(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return true;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            if (bytes[0] == 10) return true;
            if (bytes[0] == 127) return true;
            if (bytes[0] == 169 && bytes[1] == 254) return true;
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
            if (bytes[0] == 192 && bytes[1] == 168) return true;
            if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) return true;
            if (bytes[0] >= 224) return true;
            if (bytes[0] == 0) return true;
        }
        else if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast) return true;
            if (address.Equals(IPAddress.IPv6Loopback)) return true;

            var bytes = address.GetAddressBytes();
            if (bytes[0] == 0xfc || bytes[0] == 0xfd) return true;
        }

        return false;
    }
}
