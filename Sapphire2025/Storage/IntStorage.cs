using Microsoft.JSInterop;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
namespace Sapphire2025.Storage
{
	/// <summary>
	/// Clase de almacenaje en el propio cliente.
	/// Serán datos de sesión que usaré para memorizar la autenticación de usuarios
	/// y los parámetros de consulta.
	/// </summary>
	public class IntStorageService
	{
        private readonly IJSRuntime mvarJsRuntime;
		public event Action OnChange;
		internal const string LOCAL_STORAGE_ID = "localStorage";
		internal const string SESSION_STORAGE_ID = "sessionStorage";

		public IntStorageService(IJSRuntime jsRuntime)
        {
            mvarJsRuntime = jsRuntime;
        }

        public async Task<string?> getToken()
        {
            return await GetStringValue("sessionToken",false);
        }

        internal string internalRequestString(bool session, string command)
        {
            return string.Format("{0}.{1}",session? SESSION_STORAGE_ID: LOCAL_STORAGE_ID, command);
        }

        public async Task SetStringValue(string key, string value, bool session)
        {
            NotifyStateChanged();
            string auxStorageId = internalRequestString(session, "setItem");
			await mvarJsRuntime.InvokeVoidAsync(auxStorageId, key, value);
        }
        public async Task<string?> GetStringValue(string key, bool session)
        {
			string auxStorageId = internalRequestString(session, "getItem");
			return await mvarJsRuntime.InvokeAsync<string>(auxStorageId, key);
        }
        public async Task SetIntValue(string key, int value, bool session)
        {
            await SetStringValue(key, string.Format("{0}", value),session);
        }
        public async Task<int> GetIntValue(string key, bool session)
        {
            string? entrada = await GetStringValue(key,session);
            int salida = int.MinValue;
            int.TryParse(entrada, out salida);
            return salida;
        }
        public async Task SetGuidValue(string key, Guid value, bool session)
        {
            await SetStringValue(key,string.Format("{0}", value),session);
        }
        public async Task<Guid> GetGuidValue(string key, bool session)
        {
            string? entrada = await GetStringValue(key,session);
            Guid salida = Guid.Empty;
            Guid.TryParse(entrada, out salida);
            return salida;
        }

		private void NotifyStateChanged() => OnChange?.Invoke();
	}
}

/*
 * public static class WebAssemblyHostExtension
{
    public static async Task SetDefaultCulture(this WebApplication host)
    {
        using (var scope = host.Services.CreateScope())
        {
            var jsInterop = scope.ServiceProvider.GetRequiredService<IJSRuntime>();
            var result = await jsInterop.InvokeAsync<string>("blazorCulture.get");
            CultureInfo culture;

            if (result != null)
                culture = new CultureInfo(result);
            else
                culture = new CultureInfo("en-US");

            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }
    }
}

 * 
 * 
 * 
 * 
 * 
 * 
 * ¡No te preocupes, no parece loco en absoluto! Es completamente razonable querer gestionar la política de usuarios sin depender de módulos de terceros. En Blazor WebAssembly, puedes utilizar el almacenamiento local (localStorage) o el almacenamiento de sesión (sessionStorage) para guardar datos de sesión. Aquí te explico cómo hacerlo:

Uso de sessionStorage en Blazor WebAssembly
sessionStorage permite almacenar datos que persisten durante la sesión del navegador. Los datos se eliminan cuando la pestaña o ventana del navegador se cierra.

1. Agregar el Servicio de JavaScript Interop
Primero, necesitas agregar un servicio para interactuar con sessionStorage utilizando JavaScript Interop.

SessionStorageService.cs:


2. Registrar el Servicio en Program.cs
Asegúrate de registrar el servicio en Program.cs.

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

builder.Services.AddScoped<SessionStorageService>();

await builder.Build().RunAsync();
3. Utilizar el Servicio en una Página Razor
Ahora puedes utilizar el servicio SessionStorageService en tus páginas Razor para guardar y recuperar datos de sesión.

Login.razor:


4. Recuperar Datos de Sesión
Puedes recuperar los datos de sesión en otras páginas utilizando el servicio SessionStorageService.

Home.razor:

@page "/"
@inject SessionStorageService SessionStorage

<h3>Home</h3>
<p>Bienvenido, @username</p>

@code {
    private string username;

    protected override async Task OnInitializedAsync()
    {
        username = await SessionStorage.GetItemAsync("username");
    }
}
5. Eliminar Datos de Sesión
Puedes eliminar los datos de sesión cuando el usuario cierre sesión.

Logout.razor:

@page "/logout"
@inject SessionStorageService SessionStorage
@inject NavigationManager Navigation

<h3>Logout</h3>
<button @onclick="Logout">Logout</button>

@code {
    private async Task Logout()
    {
        await SessionStorage.RemoveItemAsync("username");
        Navigation.NavigateTo("/login");
    }
}
Resumen
Guardar datos de sesión: Utiliza sessionStorage para almacenar datos durante la sesión del navegador.
Recuperar datos de sesión: Utiliza el servicio SessionStorageService para recuperar datos almacenados.
Eliminar datos de sesión: Elimina los datos de sesión cuando el usuario cierre sesión.
 * 
 * */