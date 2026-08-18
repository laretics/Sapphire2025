using System;
using System.Collections.Generic;
using Diamond.Topo;

namespace Diamond.Controls.Services
{
	/// <summary>Entrada de listado de planes del almacén (sin script).</summary>
	public sealed class MeshStorePlanRef
	{
		public Guid Id { get; set; }

		public string Name { get; set; } = string.Empty;

		public Guid TopoId { get; set; }

		public string TopoName { get; set; } = string.Empty;

		public string SourceFileName { get; set; } = string.Empty;

		public DateTime UpdatedUtc { get; set; }

		public bool IsActive { get; set; } = true;
	}

	/// <summary>Plan completo cargado desde el almacén.</summary>
	public sealed class MeshStorePlanDocument
	{
		public Guid Id { get; set; }

		public string Name { get; set; } = string.Empty;

		public Guid TopoId { get; set; }

		public string TopoName { get; set; } = string.Empty;

		public string SourceScript { get; set; } = string.Empty;

		public string SourceFileName { get; set; } = string.Empty;

		/// <summary>Temporales del topo del plan (para pintarlas y aplicarlas en la malla).</summary>
		public IReadOnlyList<TemporarySpeedLimit> TemporaryLimits { get; set; } =
			Array.Empty<TemporarySpeedLimit>();
	}

	/// <summary>Topología del almacén (selector al crear/guardar plan).</summary>
	public sealed class MeshStoreTopoRef
	{
		public Guid Id { get; set; }

		public string Name { get; set; } = string.Empty;

		public string SourceFileName { get; set; } = string.Empty;
	}

	/// <summary>Petición de guardado en el almacén.</summary>
	public sealed class MeshStorePlanSaveArgs
	{
		/// <summary>Null = alta; con valor = actualización.</summary>
		public Guid? Id { get; set; }

		public Guid TopoId { get; set; }

		public string SourceScript { get; set; } = string.Empty;

		public string Name { get; set; } = string.Empty;

		public string SourceFileName { get; set; } = string.Empty;

		public string Notes { get; set; } = string.Empty;
	}

	/// <summary>Resultado de guardado en el almacén.</summary>
	public sealed class MeshStorePlanSaveResult
	{
		public bool Success { get; set; }

		public string Message { get; set; } = string.Empty;

		public Guid? Id { get; set; }

		public string? Name { get; set; }

		public Guid? TopoId { get; set; }

		public string? TopoName { get; set; }
	}

	/// <summary>Petición de publicación compilada (Tourmaline).</summary>
	public sealed class MeshStorePublishArgs
	{
		public Guid? SourcePlanId { get; set; }

		public Guid TopoId { get; set; }

		public string SourceScript { get; set; } = string.Empty;

		public string Name { get; set; } = string.Empty;

		public DateTime ValidFrom { get; set; }

		public DateTime? ValidTo { get; set; }

		public string Notes { get; set; } = string.Empty;
	}

	public sealed class MeshStorePublishResult
	{
		public bool Success { get; set; }

		public string Message { get; set; } = string.Empty;

		public Guid? PublishedId { get; set; }
	}
}
