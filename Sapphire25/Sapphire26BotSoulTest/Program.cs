using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sapphire2026Telegram;
using Microsoft.ML;
using Microsoft.ML.Data;
using Sapphire2026Telegram.Semantics;
using Sapphire2025.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

IHost? host = Host.CreateDefaultBuilder(args)
.ConfigureAppConfiguration((context, config) =>
{
	config.SetBasePath(Directory.GetCurrentDirectory());
	config.AddJsonFile("appsettings.json", optional: true);
	config.AddJsonFile("appsettings.Development.json", optional: true);
	config.AddEnvironmentVariables();
})
.ConfigureServices((context, services) =>
{
	string auxApiBaseAddress = context.Configuration["ApiBaseAddress"] ?? "http://localhost:5031/api/";
	services.AddSingleton(sp => new HttpClient { BaseAddress = new Uri(auxApiBaseAddress)});
	services.AddSingleton<IntStorageService>();
	services.AddSingleton<AuthenticationClient>();
	services.AddSingleton<AeneasClient>();
	services.AddSingleton<ExpertClient>();
	services.AddSingleton<TimeNetClient>();
})
.Build();

using AsyncServiceScope auxScope = host.Services.CreateAsyncScope();
IServiceProvider auxServices = auxScope.ServiceProvider;
IConfiguration auxConfig = auxServices.GetRequiredService<IConfiguration>();

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
ILogger<BotSoul> auxLogger = loggerFactory.CreateLogger<BotSoul>();

AuthenticationClient auxAuthenticationClient = auxServices.GetRequiredService<AuthenticationClient>();
try{
	if (await auxAuthenticationClient.ping())
		auxLogger.LogInformation("Server connection verified");
	else
		auxLogger.LogError("Server connection error!");
	}
	catch (Exception ex)
	{
		auxLogger.LogCritical($"Could not connect to API rest server: {ex.Message}");
	}

BotSoul mvarBotSoul = new BotSoul(auxLogger, auxConfig,auxServices);

bool active = true;
while(active)
{
	string? auxEntrada = Console.ReadLine();
	if ("exit" == auxEntrada)
		active = false;
	else if ("train" == auxEntrada)
		TrainModel();
	else
	{
		if (null != auxEntrada)
		{
			await mvarBotSoul.HandleDummyConsoleMessage(auxEntrada);
			Console.WriteLine(BotSoul.DummyResponse);
		}
	}
}

void TrainModel()
{
	string auxPath = "intent_data.csv";
	MLContext contexto = new MLContext(seed: 1);
	//Cargamos los datos desde el archivo de intenciones
	if (File.Exists(auxPath))
	{
		IDataView? dataset = contexto.Data.LoadFromTextFile<IntentData>(
		auxPath, separatorChar: ',', hasHeader: true);

		//Pipeline de procesamiento y entrenamiento
		EstimatorChain<Microsoft.ML.Transforms.KeyToValueMappingTransformer> pipeline =
		contexto.Transforms.Text.FeaturizeText("Features", "Text")
		.Append(contexto.Transforms.Conversion.MapValueToKey("Label"))
		.Append(contexto.MulticlassClassification.Trainers.SdcaMaximumEntropy(
		labelColumnName: "Label", featureColumnName: "Features"))
		.Append(contexto.Transforms.Conversion.MapKeyToValue("PredictedLabel"));


		//Entrenando el modelo
		TransformerChain<Microsoft.ML.Transforms.KeyToValueMappingTransformer> auxModelo = pipeline.Fit(dataset);

		//Guarda el modelo entrenado en un archivo
		contexto.Model.Save(auxModelo, dataset.Schema, "intent_model.zip");
		Console.WriteLine("Modelo guardado como intent_model.zip");
	}
	else
		Console.WriteLine($"No se encontró {auxPath}.");

}
		   