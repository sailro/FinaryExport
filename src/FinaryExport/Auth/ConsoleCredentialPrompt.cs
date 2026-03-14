namespace FinaryExport.Auth;

// Interactive console prompts for cold-start authentication.
// Password input is masked with asterisks.
public sealed class ConsoleCredentialPrompt : ICredentialPrompt
{
	public (string Email, string Password, string TotpCode) PromptCredentials()
	{
		Console.WriteLine();
		Console.WriteLine("Authentication required (no stored session).");
		Console.WriteLine();

		Console.Write("Email: ");
		var email = Console.ReadLine()?.Trim() ?? "";

		Console.Write("Password: ");
		var password = ReadMasked();

		Console.Write("TOTP Code: ");
		var totpCode = Console.ReadLine()?.Trim() ?? "";

		Console.WriteLine();
		return (email, password, totpCode);
	}

	// Reads input character-by-character, displaying '*' for each keystroke.
	// Supports backspace to correct mistakes.
	private static string ReadMasked()
	{
		var buffer = new System.Text.StringBuilder();
		while (true)
		{
			var key = Console.ReadKey(intercept: true);

			if (key.Key == ConsoleKey.Enter)
			{
				Console.WriteLine();
				break;
			}

			if (key.Key == ConsoleKey.Backspace)
			{
				if (buffer.Length > 0)
				{
					buffer.Remove(buffer.Length - 1, 1);
					Console.Write("\b \b");
				}
				continue;
			}

			if (key.KeyChar == '\0')
				continue;

			buffer.Append(key.KeyChar);
			Console.Write('*');
		}

		return buffer.ToString();
	}
}
