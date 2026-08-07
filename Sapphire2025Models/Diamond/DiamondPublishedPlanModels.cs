namespace Sapphire2025Models.Diamond
{
	/// <summary>Cabecera de un plan publicado (sin payload).</summary>
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

		public bool IsActive { get; set; }

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

		/// <summary>Inicio de vigencia (fecha). Por defecto hoy UTC.</summary>
		public DateTime? ValidFrom { get; set; }

		/// <summary>Fin de vigencia (opcional).</summary>
		public DateTime? ValidTo { get; set; }

		public string? Notes { get; set; }
	}

	public class DiamondPublishPlanResult
	{
		public bool Success { get; set; }

		public string Message { get; set; } = string.Empty;

		public DiamondPublishedPlanHeaderModel? Header { get; set; }
	}
}
