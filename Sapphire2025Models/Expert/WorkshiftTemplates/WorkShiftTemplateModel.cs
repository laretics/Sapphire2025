using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Sapphire2025Models.Expert.WorkshiftTemplates
{
	//Cualquier asignación tiene estos datos.
	[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
	[JsonDerivedType(typeof(RestTemplateModel), "rst")]
	[JsonDerivedType(typeof(AttTemplateModel), "att")]
	[JsonDerivedType(typeof(WorkTemplateModel), "wkt")]
	public abstract class WorkShiftTemplateModel
	{
		public string? Name { get; set; }
		public List<string>? Tokens { get; set; } //Lista de tokens con los que se puede invocar a este turno.
		public string? comment { get; set; }
		public string? Color { get; set; }
		public string? BgColor { get; set; }
		public string? StripeColor { get; set; }
		public int CoorX { get; set; } //Lugar de representación en la tabla del gráfico
		public int CoorY { get; set; } //Coordenada y de representación en la tabla del gráfico
		public byte DayOfWeekEnabled { get; set; } //Flag de los días de la semana en que este turno está disponible

		//Indica si este turno concretamente está disponible en este día de la semana
		public bool IsEnabled(DayOfWeek dayOfWeek, bool isFestive)
		{
			return Utils.IsDayCompatible(DayOfWeekEnabled, dayOfWeek, isFestive);
		}
		/// <summary>
		/// Indica si este turno corresponde a esta referencia.
		/// </summary>
		/// <param name="keyString">Cadena del turno que se busca</param>
		/// <returns>True si el turno coincide</returns>
		public bool Match(string keyString)
		{
			if (null == Tokens) return false;

			foreach (string token in Tokens)
				if (token.Equals(keyString)) return true;

			return false;
		}
	}
}