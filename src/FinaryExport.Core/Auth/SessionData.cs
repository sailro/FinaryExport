using System.Net;

namespace FinaryExport.Auth;

// Data persisted between runs for warm-start authentication.
// Contains session ID and all cookies (Clerk + Cloudflare) from the CookieContainer.
public sealed record SessionData(string SessionId, IReadOnlyCollection<Cookie> Cookies);
