using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace Sapphire2025.Storage
{
	/// <summary>
	/// Pila de navegación de la SPA: permite atrás / adelante propios
	/// sin depender del menú ni confundirse con el pin de la barra.
	/// </summary>
	public sealed class NavigationStackService : IDisposable
	{
		private const int MaxDepth = 40;
		private readonly NavigationManager mvarNavigator;
		private readonly List<string> mcolBack = new List<string>();
		private readonly List<string> mcolForward = new List<string>();
		private string mvarCurrent = string.Empty;
		private bool mvarReady;

		public NavigationStackService(NavigationManager navigator)
		{
			mvarNavigator = navigator;
			mvarCurrent = Normalize(mvarNavigator.Uri);
			mvarNavigator.LocationChanged += OnLocationChanged;
			mvarReady = true;
		}

		public event Action? OnChange;

		public bool CanGoBack => mcolBack.Count > 0;

		public bool CanGoForward => mcolForward.Count > 0;

		public string Current => mvarCurrent;

		public void GoBack()
		{
			if (!CanGoBack)
				return;
			mvarNavigator.NavigateTo(ToHref(mcolBack[^1]));
		}

		public void GoForward()
		{
			if (!CanGoForward)
				return;
			mvarNavigator.NavigateTo(ToHref(mcolForward[^1]));
		}

		private static string ToHref(string relative)
		{
			return string.IsNullOrEmpty(relative) ? "/" : relative;
		}

		public void Dispose()
		{
			if (!mvarReady)
				return;
			mvarNavigator.LocationChanged -= OnLocationChanged;
			mvarReady = false;
		}

		private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
		{
			string next = Normalize(e.Location);
			if (string.Equals(next, mvarCurrent, StringComparison.OrdinalIgnoreCase))
				return;

			if (IsAuthPath(next) || IsAuthPath(mvarCurrent))
			{
				mvarCurrent = next;
				Notify();
				return;
			}

			if (mcolBack.Count > 0 && string.Equals(next, mcolBack[^1], StringComparison.OrdinalIgnoreCase))
			{
				mcolForward.Add(mvarCurrent);
				Trim(mcolForward);
				mcolBack.RemoveAt(mcolBack.Count - 1);
			}
			else if (mcolForward.Count > 0 && string.Equals(next, mcolForward[^1], StringComparison.OrdinalIgnoreCase))
			{
				mcolBack.Add(mvarCurrent);
				Trim(mcolBack);
				mcolForward.RemoveAt(mcolForward.Count - 1);
			}
			else
			{
				if (!string.IsNullOrEmpty(mvarCurrent) && !IsAuthPath(mvarCurrent))
				{
					mcolBack.Add(mvarCurrent);
					Trim(mcolBack);
				}
				mcolForward.Clear();
			}

			mvarCurrent = next;
			Notify();
		}

		private void Notify()
		{
			OnChange?.Invoke();
		}

		private static void Trim(List<string> stack)
		{
			while (stack.Count > MaxDepth)
				stack.RemoveAt(0);
		}

		private string Normalize(string uri)
		{
			string relative = mvarNavigator.ToBaseRelativePath(uri ?? string.Empty);
			if (string.IsNullOrWhiteSpace(relative) || relative == "#")
				return string.Empty;

			int hash = relative.IndexOf('#');
			if (hash >= 0)
				relative = relative.Substring(0, hash);

			return relative.Trim().Trim('/');
		}

		private static bool IsAuthPath(string relative)
		{
			if (string.IsNullOrEmpty(relative))
				return false;
			return relative.StartsWith("auth/", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(relative, "auth", StringComparison.OrdinalIgnoreCase);
		}
	}
}
