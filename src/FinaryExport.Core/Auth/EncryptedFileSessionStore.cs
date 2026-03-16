using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using FinaryExport.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinaryExport.Auth;

public sealed class EncryptedFileSessionStore(
	IOptions<FinaryOptions> options,
	ILogger<EncryptedFileSessionStore> logger)
	: ISessionStore
{
	private readonly string _filePath = options.Value.SessionStorePath
										?? Path.Combine(
											Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
											".finaryexport", "session.dat");

	public async Task SaveSessionAsync(SessionData data, CancellationToken ct)
	{
		try
		{
			var serializable = new SerializableSession
			{
				SessionId = data.SessionId,
				Cookies = [.. data.Cookies.Select(c => new SerializableCookie
				{
					Name = c.Name,
					Value = c.Value,
					Domain = c.Domain,
					Path = c.Path,
					Expires = c.Expires
				})]
			};

			var json = JsonSerializer.SerializeToUtf8Bytes(serializable);
			var encrypted = ProtectedData.Protect(json, null, DataProtectionScope.CurrentUser);

			Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
			await File.WriteAllBytesAsync(_filePath, encrypted, ct);
			logger.LogDebug("Session saved to {Path}", _filePath);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Failed to save session to {Path}", _filePath);
		}
	}

	public async Task<SessionData?> LoadSessionAsync(CancellationToken ct)
	{
		if (!File.Exists(_filePath))
			return null;

		try
		{
			var encrypted = await File.ReadAllBytesAsync(_filePath, ct);
			var json = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
			var session = JsonSerializer.Deserialize<SerializableSession>(json);

			if (session?.SessionId is null || session.Cookies is null)
				return null;

			var cookies = session.Cookies.Select(sc => new Cookie(sc.Name ?? "", sc.Value ?? "", sc.Path, sc.Domain)
			{
				Expires = sc.Expires
			}).ToList();

			return new SessionData(session.SessionId, cookies);
		}
		catch (CryptographicException ex)
		{
			logger.LogWarning(ex, "Session file corrupted or created by another user");
			return null;
		}
		catch (JsonException ex)
		{
			logger.LogWarning(ex, "Session file has invalid format");
			return null;
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Failed to load session from {Path}", _filePath);
			return null;
		}
	}

	public Task ClearSessionAsync(CancellationToken ct)
	{
		try
		{
			if (File.Exists(_filePath))
			{
				File.Delete(_filePath);
				logger.LogDebug("Session cleared from {Path}", _filePath);
			}
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Failed to clear session from {Path}", _filePath);
		}
		return Task.CompletedTask;
	}

	private sealed class SerializableSession
	{
		public string? SessionId { get; init; }
		public List<SerializableCookie>? Cookies { get; init; }
	}

	private sealed class SerializableCookie
	{
		public string? Name { get; init; }
		public string? Value { get; init; }
		public string? Domain { get; init; }
		public string? Path { get; init; }
		public DateTime Expires { get; init; }
	}
}
