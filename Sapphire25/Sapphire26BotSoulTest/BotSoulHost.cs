using Sapphire2026Telegram;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Sapphire26BotSoulTest
{
	internal static class BotSoulHost
	{
		internal static BotSoul? mvarInstance{ get; private set; }
		public static void Initialize (ILogger<BotSoul> logger, IConfiguration config, BotSoul soul, Guid dummyUserId)
		{
			if (null == mvarInstance)
				mvarInstance = new BotSoul(logger, config,dummyUserId);	
		}
	}
}


/*
 * 
 Código de GitHub para ML.
 * 
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.TorchSharp;

var mlContext = new MLContext(seed: 1);

// Carga datos
var data = mlContext.Data.LoadFromTextFile<IntentData>("datos_intents.csv", separatorChar: ',', hasHeader: true);

// Pipeline
var pipeline = mlContext.Transforms.Text.NormalizeText("Text")
    .Append(mlContext.Transforms.Text.TokenizeIntoWords("Tokens", "Text"))
    .Append(mlContext.Transforms.Text.RemoveDefaultStopWords("Tokens"))
    .Append(mlContext.Transforms.Conversion.MapValueToKey("Label"))
    .Append(mlContext.MulticlassClassification.Trainers.TextClassification(
        labelColumnName: "Label",
        sentenceColumnName: "Text",
        architecture: TextClassificationArchitecture.NAS_BERT,
        maxEpochs: 10));

// Entrena
var model = pipeline.Fit(data);

// Guarda el modelo
mlContext.Model.Save(model, data.Schema, "intent_model.zip");
 */ 