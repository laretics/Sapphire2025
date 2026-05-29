using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sapphire2026Telegram;
using Microsoft.ML;
using Microsoft.ML.Data;
using Sapphire2026Telegram.Semantics;

IConfiguration auxConfig = new ConfigurationBuilder()
	.SetBasePath(Directory.GetCurrentDirectory())
	.AddJsonFile("appsettings.json", optional: true)
	.AddJsonFile("appsettings.Development.json", optional: true)
	.AddEnvironmentVariables()
	.Build();

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
ILogger<BotSoul> auxLogger = loggerFactory.CreateLogger<BotSoul>();

BotSoul mvarBotSoul = new BotSoul(auxLogger, auxConfig);

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
		   