using Sapphire2026Telegram.Operative;
using System.Text;
using Telegram.Bot;
namespace Sapphire2026Telegram.Semantics
{
	/// <summary>
	/// Una respuesta es un objeto semántico que contiene información para responder al usuario
	/// sobre una pregunta o una orden.
	/// Está diseñado para que alterne entre los diferentes mensajes de forma que proporcione
	/// la ilusión de estar hablando con un ser humano.
	/// </summary>
	public abstract class Response
	{
		protected static Random generador = new Random(); //Generador de números aleatorios para las respuestas.

		internal virtual async Task Send(ITelegramBotClient client, UserContext userContext) { }

	}

	/// <summary>
	/// Respuesta de texto con diferentes respuestas que dar. Se genera en el momento.
	/// </summary>
	public class TextResponse: Response
	{
		private Dictionary<string, string> mcolParameters = new Dictionary<string, string>(); //Conjunto de parámetros para articular una contestación.
		private List<string> mcolPhrases = new List<string>(); //Conjunto de diferentes formas de dar el mensaje.

		public void addText(string rhs)
		{
			mcolPhrases.Add(rhs);
		}
		public void addKey(string key, string value)
		{
			//Añade o modifica una clave.
			if (mcolParameters.ContainsKey(key))
				mcolParameters[key] = value;
			else
				mcolParameters.Add(key, value);
		}
		/// <summary>
		/// Devuelve una de las posibles cadenas a mostrar.
		/// </summary>
		/// <returns>Cadena mostrada</returns>
		protected string internalResponse()
		{
			int max = mcolPhrases.Count;
			if (max < 1)
				return string.Empty;
			int aleatorio = Random.Shared.Next(max);
			string frase = mcolPhrases[aleatorio];
			foreach (KeyValuePair<string,string> pareja in mcolParameters)
				frase = frase.Replace("#" + pareja.Key, pareja.Value);
			return frase;			
		}

		protected virtual byte maxResponses { get => 1; } //Número máximo de respuestas que puede devolver el objeto. Por defecto es 1, pero se puede sobreescribir en las clases hijas.
		internal override async Task Send(ITelegramBotClient client, UserContext userContext)
		{
			//byte indice = (byte)generador.Next(0, maxResponses);
			if (BotSoul.DummyMode)
				BotSoul.DummyResponse = internalResponse();
			else
				await client.SendMessage(userContext.TelegramId, internalResponse());
		}
	}
	public class TableResponse: Response
	{
		private List<object> mcolTable = new();
		private List<string> mcolFields = new(); //Nombre de las propiedades a mostrar
		private List<string>? mcolHeaders = null; //Encabezados

		public void SetFields(params string[] fields)
		{
			mcolFields = fields.ToList();
		}
		public void SetHeaders(params string[] headers)
		{
			mcolHeaders = headers.ToList();
		}
		public void AddRow(object row)
		{
			mcolTable.Add(row);
		}
		public void AddRows(IEnumerable<object> rows)
		{
			mcolTable.AddRange(rows);
		}
		private string BuildTable()
		{
			if (mcolTable.Count < 1 || mcolFields.Count < 1)
				return "No hay datos para mostrar";

			//Preparando encabezados
			List<string> auxHeaders = mcolHeaders ?? mcolFields;
			StringBuilder salida = new StringBuilder();

			salida.AppendLine(string.Join(" | ", auxHeaders));
			salida.AppendLine(new string('-', auxHeaders.Sum(h => h.Length + 3)));

			//Registros
			foreach(object row in mcolTable)
			{
				IEnumerable<string>? values = mcolFields.Select(f =>
				{
					System.Reflection.PropertyInfo? prop = row.GetType().GetProperty(f);
					if (null != prop)
						return prop.GetValue(row)?.ToString() ?? "";
					System.Reflection.FieldInfo? field = row.GetType().GetField(f);
					return field?.GetValue(row)?.ToString() ?? "";
				});
				salida.AppendLine(string.Join(" | ", values));
			}
			return salida.ToString();
		}

		internal override async Task Send(ITelegramBotClient client, UserContext userContext)
		{
			string tabla = BuildTable();
			if (BotSoul.DummyMode)
				BotSoul.DummyResponse = tabla;
			else
				await client.SendMessage(userContext.TelegramId, tabla);
		}
	}
	public class ImageResponse:TextResponse
	{
		public string? ImageUrl { get; set; }
		internal override async Task Send(ITelegramBotClient client, UserContext userContext)
		{
			if (-1 != userContext.TelegramId)
			{				
				if (null == ImageUrl)
					await base.Send(client, userContext);
				else
				{
					if(BotSoul.DummyMode)
					{
						BotSoul.DummyResponse = $"[Imagen {ImageUrl} ({internalResponse()}]";
					}
					else
					{
						string rutaRelativa = Path.Combine("Resources", "Images", ImageUrl);
						string rutaFisica = Path.Combine(Directory.GetCurrentDirectory(), rutaRelativa);
						using (FileStream cadena = File.OpenRead(rutaFisica))
						{
							await client.SendPhoto(
								chatId: userContext.TelegramId,
								photo: cadena,
								caption: internalResponse());
						}
					}					
				}
			}
		}


	}

}
