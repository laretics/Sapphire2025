namespace Sapphire2025Models.Preferences
{
	/// <summary>Claves conocidas. El almacén acepta cualquier otra clave.</summary>
	public static class PreferenceKeys
	{
		public const string Locale = "locale";
		public const string Theme = "theme";
		public const string Contrast = "contrast";
		public const string FontScale = "fontScale";
		public const string NightMode = "nightMode";
	}

	public class UserPreferenceItem
	{
		public string Key { get; set; } = string.Empty;
		public string Value { get; set; } = string.Empty;
	}

	public class UserPreferencesModel
	{
		public UserPreferencesModel()
		{
			Items = new List<UserPreferenceItem>();
		}

		public List<UserPreferenceItem> Items { get; set; }

		public DateTime UpdatedUtc { get; set; }

		public string? Get(string key)
		{
			if (string.IsNullOrWhiteSpace(key) || Items is null)
				return null;
			UserPreferenceItem? hit = Items.FirstOrDefault(
				i => string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase));
			return hit?.Value;
		}

		public void Set(string key, string? value)
		{
			if (string.IsNullOrWhiteSpace(key))
				return;
			UserPreferenceItem? hit = Items.FirstOrDefault(
				i => string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase));
			if (hit is null)
			{
				Items.Add(new UserPreferenceItem { Key = key.Trim(), Value = value ?? string.Empty });
				return;
			}

			hit.Value = value ?? string.Empty;
		}
	}

	public class UserPreferencesQueryRequest : BasicRequestModel
	{
		/// <summary>Vacío = el usuario de la sesión. Otro Guid exige permiso de administrador.</summary>
		public Guid TargetUserId { get; set; }
	}

	public class UserPreferencesSaveRequest : BasicRequestModel
	{
		public UserPreferencesSaveRequest()
		{
			Items = new List<UserPreferenceItem>();
		}

		/// <summary>Vacío = el usuario de la sesión. Otro Guid exige permiso de administrador.</summary>
		public Guid TargetUserId { get; set; }

		public List<UserPreferenceItem> Items { get; set; }
	}
}
