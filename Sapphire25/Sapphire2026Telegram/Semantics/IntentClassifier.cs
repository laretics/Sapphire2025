using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2026Telegram.Semantics
{
	internal class IntentClassifier
	{
		private static IntentClassifier? mvarInstance;
		private readonly PredictionEngine<IntentData, IntentPrediction> mvarEngine;

		private IntentClassifier()
		{
			MLContext auxContext = new MLContext();
			ITransformer? auxModel = auxContext.Model.Load("intent_model.zip", out _);
			mvarEngine = auxContext.Model.CreatePredictionEngine<IntentData, IntentPrediction>(auxModel);
		}
		public static IntentClassifier Instance => mvarInstance??= new IntentClassifier();

		public string Predict(string text)
		{
			IntentPrediction? prediction = mvarEngine.Predict(new IntentData{ Text = text });
			return prediction.PredictedLabel;
		}
	}
}
