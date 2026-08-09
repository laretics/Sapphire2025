namespace Sapphire2025Models.Diamond
{
	/// <summary>Cabecera de un plan publicado/compilado (sin payload).</summary>
	public class DiamondPublishedPlanHeaderModel
	{
		public Guid Id { get; set; }

		public Guid? SourcePlanId { get; set; }

		public string Name { get; set; } = string.Empty;

		public Guid TopoId { get; set; }

		public string TopoName { get; set; } = string.Empty;

		public string TopoContentHash { get; set; } = string.Empty;

		public string TopoStructuralHash { get; set; } = string.Empty;

		public DateTime ValidFrom { get; set; }

		public DateTime? ValidTo { get; set; }

		public DateTime CompiledUtc { get; set; }

		public string ContentHash { get; set; } = string.Empty;

		public string Format { get; set; } = string.Empty;

		public int ByteLength { get; set; }

		public int CirculationCount { get; set; }

		public int AsimilationCount { get; set; }

		public string Notes { get; set; } = string.Empty;

		/// <summary>
		/// En producción: los dispositivos externos (trenes, SIU, enclavamientos…)
		/// descargan este paquete. Persistido como IsActive en BD.
		/// </summary>
		public bool IsActive { get; set; }

		/// <summary>Alias semántico de <see cref="IsActive"/> para clientes de dispositivo.</summary>
		public bool InProduction
		{
			get { return IsActive; }
			set { IsActive = value; }
		}

		public DateTime CreatedUtc { get; set; }
	}

	/// <summary>Petición de publicación (compilar + almacenar).</summary>
	public class DiamondPublishPlanRequest
	{
		/// <summary>Plan de autoría en almacén (si se publica desde CRUD).</summary>
		public Guid? SourcePlanId { get; set; }

		/// <summary>Script en vivo (si se publica desde el planificador).</summary>
		public string? SourceScript { get; set; }

		/// <summary>Topología del almacén (obligatoria).</summary>
		public Guid TopoId { get; set; }

		public string? Name { get; set; }

		/// <summary>Inicio de vigencia (fecha). Por defecto hoy UTC o la del plan origen.</summary>
		public DateTime? ValidFrom { get; set; }

		/// <summary>Fin de vigencia (opcional).</summary>
		public DateTime? ValidTo { get; set; }

		public string? Notes { get; set; }

		/// <summary>Si true (defecto), queda en producción al publicar.</summary>
		public bool InProduction { get; set; } = true;
	}

	/// <summary>Actualización CRUD de metadatos de un plan ya compilado (sin recompilar).</summary>
	public class DiamondPublishedPlanUpdateRequest
	{
		public Guid Id { get; set; }

		public string? Name { get; set; }

		public DateTime? ValidFrom { get; set; }

		public DateTime? ValidTo { get; set; }

		/// <summary>Si se envía, actualiza el flag en producción.</summary>
		public bool? InProduction { get; set; }

		public string? Notes { get; set; }
	}

	public class DiamondPublishPlanResult
	{
		public bool Success { get; set; }

		public string Message { get; set; } = string.Empty;

		public DiamondPublishedPlanHeaderModel? Header { get; set; }
	}

	/// <summary>
	/// Paquete para dispositivos externos: topología cacheable + planes en producción
	/// vigentes o próximos a partir de una fecha.
	/// </summary>
	public class DiamondDeviceTopoPackageModel
	{
		public Guid TopoId { get; set; }

		public string TopoName { get; set; } = string.Empty;

		public string TopoContentHash { get; set; } = string.Empty;

		public string TopoStructuralHash { get; set; } = string.Empty;

		public string TopoFormat { get; set; } = string.Empty;

		public int TopoByteLength { get; set; }

		/// <summary>UTC de generación de esta respuesta.</summary>
		public DateTime GeneratedUtc { get; set; }

		/// <summary>Fecha civil de referencia (filtro de vigencia).</summary>
		public DateTime FromDate { get; set; }

		/// <summary>Planes en producción cuya vigencia no ha terminado antes de FromDate.</summary>
		public List<DiamondPublishedPlanHeaderModel> ProductionPlans { get; set; }
			= new List<DiamondPublishedPlanHeaderModel>();
	}
}
