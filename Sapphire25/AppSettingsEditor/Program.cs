using AppSettingsEditor.Models.Tourmaline;
using Spectre.Console;
using System.Text.Json;

class Program
{
    private static readonly string ConfigPath = "appsettings.json";
    private static AppConfig? config;

    static async Task Main(string[] args)
    {
        AnsiConsole.Write(new FigletText("Config Editor").Centered().Color(Color.Cyan1));
        AnsiConsole.WriteLine();

        await LoadConfig();

        while (true)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("¿Qué deseas hacer?")
                    .PageSize(10)
                    .AddChoices(
                        "Ver configuración actual",
                        "Editar SystemConfiguration",
                        "Gestionar Devices",
                        "Gestionar Cameras",
                        "Guardar cambios",
                        "[red]Salir[/]"
                    ));

            switch (choice)
            {
                case "Ver configuración actual":
                    ShowCurrentConfig();
                    break;
                case "Editar SystemConfiguration":
                    EditSystemConfiguration();
                    break;
                case "Gestionar Devices":
                    await ManageDevices();
                    break;
                case "Gestionar Cameras":
                    await ManageCameras();
                    break;
                case "Guardar cambios":
                    await SaveConfig();
                    break;
                case "[red]Salir[/]":
                    AnsiConsole.MarkupLine("[green]¡Hasta luego![/]");
                    return;
            }

            AnsiConsole.WriteLine("\nPresiona cualquier tecla para continuar...");
            Console.ReadKey(true);
        }
    }

    static async Task LoadConfig()
    {
        if (File.Exists(ConfigPath))
        {
            var json = await File.ReadAllTextAsync(ConfigPath);
            var options = new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                PropertyNameCaseInsensitive = true
            };
            config = JsonSerializer.Deserialize<AppConfig>(json, options);
            AnsiConsole.MarkupLine("[green]Configuración cargada correctamente.[/]");
        }
        else
        {
            config = new AppConfig();
            AnsiConsole.MarkupLine("[yellow]No se encontró el archivo. Se creó uno nuevo.[/]");
        }
    }

    static async Task SaveConfig()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(config, options);
        await File.WriteAllTextAsync(ConfigPath, json);
        AnsiConsole.MarkupLine("[green]¡Archivo guardado correctamente![/]");
    }

    // Métodos de edición (puedes expandirlos)
    static void ShowCurrentConfig()
    {
        AnsiConsole.Write(new Rule("Configuración Actual") { Style = Style.Parse("blue") });
        // Aquí puedes mostrar más detalles si quieres
        AnsiConsole.MarkupLine($"Series: [cyan]{config?.SystemConfiguration.Series}[/]");
        AnsiConsole.MarkupLine($"Devices: [cyan]{config?.Devices.Count}[/]");
        AnsiConsole.MarkupLine($"Cameras: [cyan]{config?.Cameras.Count}[/]");
    }

    static void EditSystemConfiguration()
    {
        SystemConfiguration sys = config!.SystemConfiguration;

        sys.Series = AnsiConsole.Ask<string>("Series:", sys.Series);
        sys.Name = AnsiConsole.Ask<string>("Name:", sys.Name);
        sys.ToniCruz = AnsiConsole.Ask<string>("ToniCruz:", sys.ToniCruz);
        sys.MVBRetries = AnsiConsole.Ask<int>("MVB Retries:", sys.MVBRetries);

        sys.SapphireUrl = AnsiConsole.Ask<string>("SapphireUrl:", sys.SapphireUrl);
        sys.MVBUrl = AnsiConsole.Ask<string>("MVBUrl:", sys.MVBUrl);
        sys.TExperienceUrl = AnsiConsole.Ask<string>("TExperienceUrl:", sys.TExperienceUrl);
        sys.SfmInfoUrl = AnsiConsole.Ask<string>("SfmInfoUrl:", sys.SfmInfoUrl);
        sys.SfmInfoToken = AnsiConsole.Ask<string>("SfmInfoToken:", sys.SfmInfoToken);

        AnsiConsole.MarkupLine("[green]SystemConfiguration actualizado[/]");
    }

    static async Task ManageDevices() 
    {
        List<Device> devices  = config!.Devices;
        while (true)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Gestionar Devices")
                    .AddChoices("Ver Devices", "Añadir Device", "Editar Device", "Eliminar Device", "Volver"));

            if (choice == "Volver") break;

            if (choice == "Ver Devices")
            {
                var table = new Table().Border(TableBorder.Rounded);
                table.AddColumns("PublicId", "Type", "Coach", "Address");
                foreach (var d in devices)
                    table.AddRow(d.PublicId, d.Type, d.Coach, d.Address);
                AnsiConsole.Write(table);
            }
            else if (choice == "Añadir Device")
            {
                var d = new Device();
                d.PublicId = AnsiConsole.Ask<string>("PublicId:");
                d.Type = AnsiConsole.Ask<string>("Type (HMI/TFT/Led):");
                d.Coach = AnsiConsole.Ask<string>("Coach:");
                d.Address = AnsiConsole.Ask<string>("Address:");
                devices.Add(d);
                AnsiConsole.MarkupLine("[green]Device añadido[/]");
            }
            // Editar y Eliminar se pueden expandir más si necesitas
        }
    }
    static async Task ManageCameras() 
    {
        List<Camera> cameras = config!.Cameras;
        while (true)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Gestionar Cameras")
                    .AddChoices("Ver Cameras", "Añadir Camera", "Volver"));

            if (choice == "Volver") break;

            if (choice == "Ver Cameras")
            {
                var table = new Table().Border(TableBorder.Rounded);
                table.AddColumns("Id", "Name", "Address", "Essential");
                foreach (var c in cameras)
                    table.AddRow(c.Id, c.Name, c.Address, c.Essential.ToString());
                AnsiConsole.Write(table);
            }
            else if (choice == "Añadir Camera")
            {
                var c = new Camera();
                c.Id = AnsiConsole.Ask<string>("Id:");
                c.Name = AnsiConsole.Ask<string>("Name:");
                c.Address = AnsiConsole.Ask<string>("Address:");
                c.Essential = AnsiConsole.Confirm("Essential?", false);
                cameras.Add(c);
                AnsiConsole.MarkupLine("[green]Camera añadida[/]");
            }
        }
    }
}