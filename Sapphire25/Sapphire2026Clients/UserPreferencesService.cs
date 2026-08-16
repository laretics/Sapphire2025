using System.Globalization;
using System.Text.Json;
using Sapphire2025.Storage;
using Sapphire2025Models.I18n;
using Sapphire2025Models.Preferences;

namespace Sapphire2026Clients
{
	/// <summary>
	/// Preferencias de usuario (Sapphire) con caché local y localización.
	/// Compartido por Zafiro WASM y Tourmaline.
	/// </summary>
	public sealed class UserPreferencesService
	{
		public const string CacheKey = "user.preferences";

		private readonly AuthenticationClient mvarAuth;
		private readonly IntStorageService mvarStorage;
		private UserPreferencesModel mvarModel = new();
		private UiLocale mvarLocale = UiLocale.Spanish;

		public UserPreferencesService(AuthenticationClient auth, IntStorageService storage)
		{
			mvarAuth = auth;
			mvarStorage = storage;
		}

		public event EventHandler? Changed;

		public UiLocale Locale => mvarLocale;

		public UserPreferencesModel Snapshot => mvarModel;

		public string T(string key) => UiCatalog.Get(mvarLocale, key);

		public string T(string key, params object?[] args)
		{
			string fmt = UiCatalog.Get(mvarLocale, key);
			if (args is null || args.Length == 0)
				return fmt;
			try
			{
				return string.Format(CultureInfo.CurrentCulture, fmt, args);
			}
			catch (FormatException)
			{
				return fmt;
			}
		}

		public string? Get(string key) => mvarModel.Get(key);

		public async Task LoadLocalAsync()
		{
			try
			{
				string? raw = await mvarStorage.GetStringValue(CacheKey, false);
				if (!string.IsNullOrWhiteSpace(raw))
				{
					UserPreferencesModel? parsed = JsonSerializer.Deserialize<UserPreferencesModel>(raw);
					if (parsed is not null)
						mvarModel = parsed;
				}
			}
			catch
			{
			}

			ApplyLocale(mvarModel.Get(PreferenceKeys.Locale), notify: false);
		}

		public async Task LoadFromServerAsync()
		{
			try
			{
				UserPreferencesModel? remote = await mvarAuth.GetUserPreferencesAsync();
				if (remote is not null)
				{
					mvarModel = remote;
					await PersistLocalAsync();
				}
			}
			catch
			{
			}

			ApplyLocale(mvarModel.Get(PreferenceKeys.Locale), notify: true);
		}

		public async Task SetAsync(string key, string value)
		{
			mvarModel.Set(key, value);
			mvarModel.UpdatedUtc = DateTime.UtcNow;
			if (string.Equals(key, PreferenceKeys.Locale, StringComparison.OrdinalIgnoreCase))
				ApplyLocale(value, notify: false);
			await PersistLocalAsync();
			try
			{
				UserPreferencesModel? saved = await mvarAuth.SetUserPreferencesAsync(mvarModel.Items);
				if (saved is not null)
					mvarModel = saved;
			}
			catch
			{
			}

			RaiseChanged();
		}

		public async Task SetLocaleAsync(UiLocale locale)
		{
			await SetAsync(PreferenceKeys.Locale, UiLocales.ToCode(locale));
		}

		private async Task PersistLocalAsync()
		{
			try
			{
				string json = JsonSerializer.Serialize(mvarModel);
				await mvarStorage.SetStringValue(CacheKey, json, false);
			}
			catch
			{
			}
		}

		private void ApplyLocale(string? raw, bool notify)
		{
			UiLocale next = UiLocales.Parse(raw);
			mvarLocale = next;
			try
			{
				CultureInfo culture = CultureInfo.GetCultureInfo(UiLocales.CultureName(next));
				CultureInfo.DefaultThreadCurrentCulture = culture;
				CultureInfo.DefaultThreadCurrentUICulture = culture;
			}
			catch
			{
			}

			if (notify)
				RaiseChanged();
		}

		private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
	}
}
