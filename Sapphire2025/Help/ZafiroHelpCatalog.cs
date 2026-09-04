using Sapphire2025Models;

namespace Sapphire2025.Help
{
	/// <summary>
	/// Catálogo estático de ayuda de Zafiro, filtrable por roles de usuario.
	/// No incluye menús de administración (Root); sí el resto de secciones operativas.
	/// </summary>
	/// <remarks>
	/// Formato del <see cref="HelpTopic.Body"/>:
	/// <list type="bullet">
	/// <item>Párrafos separados por una línea en blanco.</item>
	/// <item>Listas: líneas que empiezan por "• ".</item>
	/// <item>Negrita: **texto**.</item>
	/// <item>
	/// Imágenes (archivos en wwwroot): [[img:img/help/ejemplo.png]]
	/// o con pie: [[img:img/help/ejemplo.png|Descripción visible bajo la imagen]].
	/// Coloque las capturas preferiblemente en wwwroot/img/help/.
	/// </item>
	/// </list>
	/// </remarks>
	public static class ZafiroHelpCatalog
	{
		public sealed class HelpTopic
		{
			public required string Id { get; init; }
			public required string Title { get; init; }
			public required string Category { get; init; }
			public required string Summary { get; init; }
			/// <summary>
			/// Texto de la ayuda. Soporta **negrita**, listas "• " e imágenes
			/// [[img:ruta/relativa.png|pie opcional]] (solo rutas locales bajo wwwroot).
			/// </summary>
			public required string Body { get; init; }
			public string? Route { get; init; }
			public string? Icon { get; init; }
			/// <summary>Roles que ven este tema. Vacío = todos los autenticados (salvo exclusión admin).</summary>
			public Common.UserRole[] Roles { get; init; } = Array.Empty<Common.UserRole>();
			public string[] Keywords { get; init; } = Array.Empty<string>();
		}

		public static IReadOnlyList<HelpTopic> All { get; } = Build();

		public static IEnumerable<HelpTopic> ForRoles(IEnumerable<Common.UserRole>? roles)
		{
			HashSet<Common.UserRole> set = roles?.ToHashSet() ?? new HashSet<Common.UserRole>();
			bool isRoot = set.Contains(Common.UserRole.Root);

			foreach (HelpTopic topic in All)
			{
				if (topic.Roles.Length == 0)
				{
					// Temas generales: cualquier sesión autenticada.
					if (set.Count > 0)
						yield return topic;
					continue;
				}

				// Root ve toda la ayuda operativa (para dar soporte), sin temas de admin.
				if (isRoot)
				{
					yield return topic;
					continue;
				}

				if (topic.Roles.Any(set.Contains))
					yield return topic;
			}
		}

		public static HelpTopic? Find(string? id) =>
			string.IsNullOrWhiteSpace(id)
				? null
				: All.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

		private static IReadOnlyList<HelpTopic> Build()
		{
			Common.UserRole[] almostAll =
			{
				Common.UserRole.Anonymous,
				Common.UserRole.Inspector,
				Common.UserRole.Expert,
				Common.UserRole.Oficial,
				Common.UserRole.Mechanic,
				Common.UserRole.Engineer,
				Common.UserRole.Station
			};

			Common.UserRole[] tallerRoles =
			{
				Common.UserRole.Anonymous,
				Common.UserRole.Inspector,
				Common.UserRole.Expert,
				Common.UserRole.Oficial,
				Common.UserRole.Mechanic,
				Common.UserRole.Engineer,
				Common.UserRole.Station
			};

			Common.UserRole[] queryRoles = tallerRoles;

			Common.UserRole[] personalRoles =
			{
				Common.UserRole.Inspector,
				Common.UserRole.Expert,
				Common.UserRole.Station,
				Common.UserRole.Engineer
			};

			Common.UserRole[] scheduleRoles =
			{
				Common.UserRole.Anonymous,
				Common.UserRole.Inspector,
				Common.UserRole.Expert,
				Common.UserRole.Station,
				Common.UserRole.Engineer
			};

			Common.UserRole[] engineerRoles = { Common.UserRole.Engineer, Common.UserRole.Oficial };
			Common.UserRole[] diamondRoles = { Common.UserRole.Engineer };
			Common.UserRole[] tourmalineRoles = { Common.UserRole.Mechanic, Common.UserRole.Oficial };
			Common.UserRole[] inspectorOnly = { Common.UserRole.Inspector };
			Common.UserRole[] capsRoles = { Common.UserRole.Expert };

			return new List<HelpTopic>
			{
				new HelpTopic
				{
					Id = "nav-basica",
					Title = "Cómo moverse por Zafiro",
					Category = "Primeros pasos",
					Icon = "Home",
					Summary = "Menú lateral, roles y pantalla de inicio.",
					Keywords = new[] { "menú", "inicio", "navegación", "roles", "sidebar" },
					Body =
@"Zafiro se organiza con un menú lateral izquierdo. Solo verá las entradas correspondientes a sus roles (Inspector, Estación, Mecánico, etc.).

• **Inicio**: pantalla principal y avisos del sistema.
• **Yo**: su ficha personal, actividad y emparejado con Telegram.
• El resto de secciones (Inspector, Taller, Movimiento…) agrupan las herramientas de cada colectivo.

En pantallas pequeñas use el botón ☰ de la barra superior para abrir el menú. Puede contraer el menú en escritorio con la flecha del lateral.

[[img:img/sfmImg.png|Logotipo SFM (ejemplo de imagen en la ayuda). Sustitúyalo por capturas en img/help/.]]

Si no encuentra una función, es probable que su usuario no tenga el rol necesario: póngase en contacto con un administrador."
				},
				new HelpTopic
				{
					Id = "mi-perfil",
					Title = "Mi perfil (Yo)",
					Category = "Primeros pasos",
					Icon = "Myself",
					Route = "admin/myuserdossier",
					Roles = almostAll,
					Summary = "Datos personales, actividad y Telegram.",
					Keywords = new[] { "perfil", "yo", "telegram", "password", "contraseña", "actividad" },
					Body =
@"En **Yo** puede consultar su CF, nombre, correo y teléfonos, y el estado de la cuenta.

Pestañas habituales:
• **Info**: resumen de actividad reciente en el sistema.
• **Telegram**: emparejar o desvincular su cuenta de Telegram para recibir avisos (notas técnicas, broadcast, etc.).

Si debe cambiar la contraseña y el sistema se lo pide al entrar, complete el formulario de establecimiento de contraseña antes de continuar."
				},
				new HelpTopic
				{
					Id = "taller-flujo",
					Title = "Taller · Estado del material móvil",
					Category = "Material móvil",
					Icon = "RoleMechanic",
					Route = "trainFlow",
					Roles = tallerRoles,
					Summary = "Lista de trenes, estados y acceso al dossier de cada unidad.",
					Keywords = new[] { "taller", "tren", "estado", "flujo", "trainflow", "correctivo", "preventivo" },
					Body =
@"La tarjeta **Taller** abre el panel de material móvil: ve el estado actual de cada tren (disponible, correctivo, preventivo, pendiente de diagnóstico, etc.).

Qué puede hacer desde ahí:
1. Localizar un tren por número o estado.
2. Abrir el **dossier** de la unidad para ver historial, notas y órdenes.
3. Según su rol, ejecutar acciones (abrir parte, iniciar/fin correctivo o preventivo, lavados, cambio de vía…).

Los colores y etiquetas de estado le indican de un vistazo en qué fase del ciclo se encuentra cada tren. Las acciones no permitidas para su rol no aparecen o no se ejecutan."
				},
				new HelpTopic
				{
					Id = "dossier-tren",
					Title = "Dossier del tren",
					Category = "Material móvil",
					Icon = "Att",
					Route = "trainFlow",
					Roles = tallerRoles,
					Summary = "Detalle de una unidad: notas, cambios de estado y órdenes.",
					Keywords = new[] { "dossier", "nota", "parte", "avería", "odómetro", "vía", "andén" },
					Body =
@"Al abrir un tren entra en su dossier. Ahí se concentra la información operativa:

• **Estado y última operación**: quién y cuándo movió el tren.
• **Notas e incidencias**: partes de avería, notas informativas y técnicas.
• **Historial de cambios de estado**.
• **Órdenes de trabajo** (p. ej. lavados) cuando existan.
• Según permisos: **odómetro**, **vía/andén**, lavado, cambios de estado.

**Tipos de nota habituales**
• Parte de avería: describe una incidencia; puede llevar el tren a «pendiente de diagnóstico».
• Nota informativa: contexto general.
• Nota técnica: observación técnica (puede notificar por Telegram a colectivos definidos).

Use siempre un texto claro (síntoma, lugar, consecuencias) para que el resto del equipo entienda la situación sin llamar."
				},
				new HelpTopic
				{
					Id = "consulta-trenes",
					Title = "Consulta de material móvil",
					Category = "Material móvil",
					Icon = "Eye",
					Route = "trainQuery",
					Roles = queryRoles,
					Summary = "Buscar trenes por número, estado, lugar o usuario.",
					Keywords = new[] { "consulta", "buscar", "filtro", "material", "trainquery", "imprimir" },
					Body =
@"**Material móvil** permite filtrar la flota sin entrar tren a tren:

• Número (o parte del nombre).
• Estado actual.
• Lugar / estación de la vía.
• Último usuario que intervino.

Tras aplicar filtros puede **imprimir** la vista. Es la forma rápida de preparar un listado de situación para un turno o una reunión."
				},
				new HelpTopic
				{
					Id = "consulta-incidencias",
					Title = "Incidencias y notas",
					Category = "Material móvil",
					Icon = "AddReport",
					Route = "aeneas/incidenceQuery",
					Roles = queryRoles,
					Summary = "Consulta avanzada de notas y cambios de estado con exportación.",
					Keywords = new[] { "incidencia", "nota", "consulta", "excel", "csv", "etiquetas", "sistema", "síntoma" },
					Body =
@"La consulta de **Incidencias y notas** combina dos orígenes:
• Notas (partes, informativas, técnicas, taller).
• Cambios de estado de los trenes.

**Filtros útiles**
• Rango de fechas (atajos de 7/30/90 días).
• Uno o varios trenes y usuarios.
• Tipo de nota y etiquetas (válida, síntoma/resolución, sistema del tren afectado).
• Palabras clave en el texto de las notas.

**Resultados**
• Vista cronológica o solo notas / solo estados.
• Exportar a **CSV/Excel** e **imprimir** un informe.

Cada búsqueda queda registrada en el log de actividad del sistema (quién consultó y con qué criterios), para auditoría."
				},
				new HelpTopic
				{
					Id = "mi-grafico",
					Title = "Mi gráfico · Cuadrante de turnos",
					Category = "Personal y turnos",
					Icon = "EventDateTime",
					Route = "drivers/schedules",
					Roles = new[] { Common.UserRole.Anonymous },
					Summary = "Ver turnos y trenes asignados en el cuadrante.",
					Keywords = new[] { "cuadrante", "turno", "gráfico", "maquinista", "schedules" },
					Body =
@"**Mi gráfico** muestra su planificación de turnos y, cuando aplica, información de trenes asociados.

Consejos:
• Navegue por días o periodos según la vista disponible.
• Compruebe siempre la fecha del servicio antes de firmar o solicitar cambios.
• Si el número de tren aparece como enlace, figura en el **plan de explotación** de esa fecha (Diamond, cualquier día del año) y abre la **hoja de circulación**. Si no hay enlace, ese tren no está en el plan.
• En la hoja, **Obs.** muestra los trenes de cruce. Debajo se indica el maquinista grafiado de cada uno. Con permiso de simulación (Expert / Root) puede abrir su cuadrante.
• Algunas consultas de turnos ajenos o del día quedan registradas en el log de actividad.

Si el cuadrante no carga datos, revise la sesión o contacte con el inspector / jefe de maquinistas."
				},
				new HelpTopic
				{
					Id = "listado-maquinistas",
					Title = "Personal de movimiento",
					Category = "Personal y turnos",
					Icon = "RoleTrainDriver",
					Route = "submenu/inspector",
					Roles = personalRoles,
					Summary = "Listados y herramientas de personal de tracción.",
					Keywords = new[] { "maquinistas", "agentes", "personal", "listado", "CTC" },
					Body =
@"Desde los menús de **Inspector**, **Estación** o **J. Maquinista** accede a herramientas de personal:

• Listados de agentes / maquinistas.
• Vistas de cuadrante o gráficos diarios según el perfil.
• Impresión de listados cuando la pantalla lo ofrece.

Use los filtros de fecha y vista antes de imprimir. Los datos reflejan la información cargada en Expert / planificación; si falta un agente, revise primero la importación o la asignación del día."
				},
				new HelpTopic
				{
					Id = "parte-diario",
					Title = "Parte / hoja de trabajo del día",
					Category = "Personal y turnos",
					Icon = "Printer",
					Roles = scheduleRoles,
					Summary = "Hojas de trabajo y gráficos del día de servicio.",
					Keywords = new[] { "parte", "diario", "hoja", "trabajo", "impresión", "día" },
					Body =
@"Varias tarjetas del menú abren **hojas o gráficos del día** (trabajo del servicio, listados, impresión).

Flujo típico:
1. Elija la fecha del servicio.
2. Revise que la vista (agentes, trenes, comentarios) sea la correcta.
3. Imprima o exporte si lo necesita el puesto.

Estas pantallas están pensadas para el relevo de turno: prepare el documento al inicio o al cierre del servicio."
				},
				new HelpTopic
				{
					Id = "vias-plataforma",
					Title = "Informe de vías",
					Category = "Operación",
					Icon = "Location",
					Route = "inspector/platformreport",
					Roles = inspectorOnly,
					Summary = "Estado del material por vías y estaciones.",
					Keywords = new[] { "vías", "andén", "plataforma", "estación", "parking" },
					Body =
@"El **informe de vías** muestra la ocupación / ubicación del material por estación y vía.

Sirve para:
• Saber qué tren está en cada vía.
• Preparar movimientos de entrada/salida a taller o circulación.
• Imprimir un pantallazo de situación para el puesto de mando.

Si un tren aparece en vía incorrecta, actualice la vía desde el dossier del tren (cuando su rol lo permita) o coordine con quien gestiona el estacionamiento."
				},
				new HelpTopic
				{
					Id = "disponibilidad",
					Title = "Disponibilidad · Línea temporal",
					Category = "Análisis",
					Icon = "RoleDetective",
					Route = "timeSnap",
					Roles = engineerRoles,
					Summary = "Histórico de disponibilidad del material a lo largo del tiempo.",
					Keywords = new[] { "disponibilidad", "timesnap", "timeline", "histórico", "excel" },
					Body =
@"**Disponibilidad** representa en el tiempo cuándo cada tren ha estado disponible, en taller, en preventivo, etc.

Úselo para:
• Analizar tiempos fuera de servicio.
• Preparar informes de fiabilidad o de contrato.
• Exportar datos cuando la pantalla ofrezca exportación.

Elija el intervalo temporal adecuado: intervalos muy amplios pueden tardar más en cargar."
				},
				new HelpTopic
				{
					Id = "contrato-erion",
					Title = "Contrato SFM · seguimiento",
					Category = "Análisis",
					Icon = "RoleDetective",
					Route = "timeSnap/avail",
					Roles = new[] { Common.UserRole.Engineer },
					Summary = "Indicadores de seguimiento según pliego.",
					Keywords = new[] { "contrato", "erion", "pliego", "indicadores", "disponibilidad" },
					Body =
@"Esta vista concentra indicadores de **seguimiento contractual** derivados de la disponibilidad y del servicio.

Interprete las series con el criterio del pliego vigente y cruce con la consulta de incidencias si necesita justificar un periodo concreto de indisponibilidad."
				},
				new HelpTopic
				{
					Id = "tourmaline",
					Title = "Tourmaline · modo servicio",
					Category = "Taller",
					Icon = "Map",
					Route = "tourmalineServiceMode",
					Roles = tourmalineRoles,
					Summary = "Herramienta de servicio Tourmaline para taller.",
					Keywords = new[] { "tourmaline", "gps", "servicio", "taller" },
					Body =
@"**Tourmaline** en modo servicio es la herramienta de apoyo en taller / explotación asociada al sistema Tourmaline.

Ábrala desde el menú de Mecánico u Oficial. Siga los procedimientos internos de SFM para la puesta en servicio y la interpretación de la información en campo."
				},
				new HelpTopic
				{
					Id = "jefe-maquinistas",
					Title = "Menú Jefe de Maquinistas",
					Category = "Personal y turnos",
					Icon = "RoleDiagnoser",
					Route = "submenu/caps",
					Roles = capsRoles,
					Summary = "Herramientas del colectivo de tracción y supervisión técnica.",
					Keywords = new[] { "caps", "jefe", "maquinistas", "diagnóstico", "expert" },
					Body =
@"El menú **J. Maquinista** reúne:
• Hojas / gráficos de trabajo del día.
• Vistas de personal y cuadrantes.
• Acceso al taller y consultas de material e incidencias.

Es el punto de entrada para combinar la visión de personal con la del material cuando hay una incidencia en línea o un cambio de plan."
				},
				new HelpTopic
				{
					Id = "diamond-explotacion",
					Title = "Diamond · planes y topologías",
					Category = "Explotación",
					Icon = "Map",
					Route = "submenu/diamond",
					Roles = diamondRoles,
					Summary = "Almacén de topologías, mallas y planes de explotación.",
					Keywords = new[] { "diamond", "timenet", "malla", "topología", "plan", "explotación", "festivo", "festivos", "calendario" },
					Body =
@"El menú **Diamond** agrupa la planificación de horarios:

• **Explotación (Legacy)**: gestión clásica de turnos / workshifts.
• **Almacén Diamond**: topologías, planes versionados, limitaciones temporales, calendario de festivos y catálogo de lugares (`places.xml`) de Tourmaline.
• **Planes de Explotación**: mallas horarias del planificador Diamond / TimeNet.

Flujo habitual:
1. Mantener la topología correcta en el almacén.
2. Marcar los días festivos del año en la solapa **Festivos** (calendario; se guarda en la tabla `Festives`).
3. Editar o importar el plan de explotación.
4. Publicar / validar según el procedimiento de su área.

Los cambios de topología o borrados relevantes pueden quedar auditados en el registro de eventos."
				},
				new HelpTopic
				{
					Id = "roles-resumen",
					Title = "Qué ve cada rol (resumen)",
					Category = "Primeros pasos",
					Icon = "Eye",
					Summary = "Mapa rápido de menús por colectivo.",
					Keywords = new[] { "roles", "permisos", "quién", "acceso", "menú" },
					Body =
@"Guía orientativa (puede haber solapes si un usuario tiene varios roles):

• **Movimiento (maquinista)**: taller, mi gráfico, consulta de trenes e incidencias.
• **Estación**: taller, personal, material, incidencias, partes del día.
• **Inspector**: personal, vías, partes, taller, material.
• **Mecánico**: taller, Tourmaline, consultas de material e incidencias.
• **Oficial**: disponibilidad, taller, Tourmaline, consultas.
• **J. Maquinista (Expert)**: personal + material + partes del día.
• **Ingeniero**: análisis de disponibilidad, contrato, material, Diamond.
• **Administrador**: configuración del sistema (no cubierta en esta ayuda).

Si le falta un menú que debería tener, solicite la asignación del rol correspondiente."
				}
			};
		}
	}
}
