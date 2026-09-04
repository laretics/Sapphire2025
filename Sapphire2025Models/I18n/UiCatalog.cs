namespace Sapphire2025Models.I18n
{
	/// <summary>
	/// Catálogo de textos de interfaz (ca / es / en).
	/// Clave estable; si falta traducción se usa castellano y, si no, la propia clave.
	/// </summary>
	public static class UiCatalog
	{
		private static readonly Dictionary<string, string[]> sdict = Build();

		public static string Get(UiLocale locale, string key)
		{
			if (string.IsNullOrWhiteSpace(key))
				return string.Empty;
			if (!sdict.TryGetValue(key, out string[]? row) || row is null || row.Length == 0)
				return key;
			int i = (int)locale;
			if (i >= 0 && i < row.Length && !string.IsNullOrEmpty(row[i]))
				return row[i];
			if (row.Length > (int)UiLocale.Spanish && !string.IsNullOrEmpty(row[(int)UiLocale.Spanish]))
				return row[(int)UiLocale.Spanish];
			return row[0];
		}

		public static bool Has(string key) => sdict.ContainsKey(key);

		private static Dictionary<string, string[]> Build()
		{
			// Orden: català, castellano, english
			Dictionary<string, string[]> d = new(StringComparer.OrdinalIgnoreCase);
			void A(string key, string ca, string es, string en) => d[key] = new[] { ca, es, en };

			A("nav.home", "Inici", "Inicio", "Home");
			A("nav.me", "Jo", "Yo", "Me");
			A("nav.help", "Ajuda", "Ayuda", "Help");
			A("nav.admin", "Admin", "Admin", "Admin");
			A("nav.expert", "Cap de maquinistes", "J.Maquinista", "Chief driver");
			A("nav.station", "Estació", "Estación", "Station");
			A("nav.inspector", "Inspector", "Inspector", "Inspector");
			A("nav.mechanic", "Mecànic", "Mecánico", "Mechanic");
			A("nav.oficial", "Oficial", "Oficial", "Foreman");
			A("nav.engineer", "Enginyer", "Ingeniero", "Engineer");
			A("nav.diamond", "Diamond", "Diamond", "Diamond");
			A("nav.movement", "Moviment", "Movimiento", "Operations");
			A("nav.home.tip", "Pantalla d'inici", "Pantalla de inicio", "Home screen");
			A("nav.me.tip", "La meva pàgina d'informació.", "Mi página de información.", "My information page.");
			A("nav.help.tip", "Centre d'ajuda segons el perfil.", "Centro de ayuda según su perfil.", "Help centre for your role.");
			A("nav.admin.tip", "Administració del sistema.", "Administración del sistema.", "System administration.");
			A("nav.expert.tip", "Opcions per a caps de maquinistes", "Opciones para Jefes de Maquinistas", "Options for chief drivers");
			A("nav.station.tip", "Personal d'estacions", "Personal de estaciones", "Station staff");
			A("nav.inspector.tip", "Opcions per a inspectors", "Opciones para Inspectores", "Options for inspectors");
			A("nav.mechanic.tip", "Opcions per al personal de tallers", "Opciones para Personal de los Talleres", "Workshop staff options");
			A("nav.oficial.tip", "Opcions per a oficials de taller de SFM", "Opciones para Oficiales de taller de SFM", "SFM workshop foremen");
			A("nav.engineer.tip", "Opcions per a anàlisi d'informació", "Opciones para Análisis de Información", "Information analysis");
			A("nav.diamond.tip", "Generació de malles horàries", "Generación de mallas horarias", "Timetable planning");
			A("nav.movement.tip", "Maquinistes i personal de trens", "Maquinistas y personal de trenes", "Drivers and train staff");
			A("nav.pin", "Fixa o contrau el menú", "Fijar o contraer el menú", "Pin or collapse the menu");
			A("nav.open", "Obre el menú", "Abrir menú", "Open menu");
			A("nav.exitzen", "Surt de pantalla completa (F11)", "Salir de pantalla completa (F11)", "Exit full screen (F11)");

			A("auth.login.title", "Inici de sessió", "Inicio de sesión", "Sign in");
			A("auth.login.subtitle", "Accediu a Zafiro amb les vostres credencials", "Acceda a Zafiro con sus credenciales", "Sign in to Zafiro with your credentials");
			A("auth.login.id", "Identificació", "Identificación", "Identification");
			A("auth.login.id.hint", "Podeu iniciar sessió amb el nom, el correu o el carnet ferroviari (CF).", "Puede iniciar la sesión tecleando su nombre, su buzón de correo electrónico o su carnet ferroviario (CF).", "You can sign in with your name, email or railway ID (CF).");
			A("auth.login.id.ph", "Nom, correu o CF", "Nombre, correo o CF", "Name, email or CF");
			A("auth.login.password", "Contrasenya", "Contraseña", "Password");
			A("auth.login.submit", "Inicia sessió", "Iniciar sesión", "Sign in");
			A("auth.login.back", "Torna a l'inici", "Volver al inicio", "Back to home");
			A("auth.login.error", "No s'ha pogut iniciar sessió. Introduïu credencials vàlides i torneu-ho a provar.", "No ha podido iniciar sesión. Introduzca unas credenciales válidas y vuelva a intentarlo.", "Sign-in failed. Enter valid credentials and try again.");
			A("auth.login.error.title", "Error", "Error", "Error");
			A("auth.ok", "Accepta", "Aceptar", "OK");
			A("auth.logout", "Tanca sessió", "Cerrar Sesión", "Sign out");
			A("auth.login.link", "Obre sessió", "Abrir Sesión", "Sign in");
			A("auth.unknown", "Usuari desconegut", "Usuario desconocido", "Unknown user");
			A("auth.user", "Usuari", "Usuario", "User");

			A("pref.title", "Preferències", "Preferencias", "Preferences");
			A("pref.language", "Idioma", "Idioma", "Language");
			A("pref.language.hint", "S'aplica a Zafiro i a Tourmaline (cabina) amb el mateix compte.", "Se aplica a Zafiro y a Tourmaline (cabina) con la misma cuenta.", "Applies to Zafiro and Tourmaline (cab) with the same account.");
			A("pref.saved", "Preferències desades.", "Preferencias guardadas.", "Preferences saved.");
			A("pref.save", "Desa", "Guardar", "Save");
			A("pref.saving", "Desant…", "Guardando…", "Saving…");
			A("pref.open", "Les preferències són obertes: se'n poden afegir més (colors, contrast…) sense canviar el model de dades.", "Las preferencias son abiertas: se pueden añadir más (colores, contraste…) sin cambiar el modelo de datos.", "Preferences are open-ended: more can be added (colours, contrast…) without changing the data model.");

			A("me.info", "Info", "Info", "Info");
			A("me.telegram", "Telegram", "Telegram", "Telegram");
			A("me.cf", "CF", "CF", "ID");
			A("me.email", "Correu", "Email", "Email");
			A("me.phone", "Telèfon", "Teléfono", "Phone");
			A("me.ext", "Extensió", "Extensión", "Ext.");
			A("me.status", "Estat", "Estado", "Status");
			A("me.active", "Actiu", "Activo", "Active");
			A("me.disabled", "Desactivat", "Desactivado", "Disabled");
			A("me.notfound", "Usuari no trobat", "Usuario no encontrado", "User not found");
			A("me.notfound.hint", "Comproveu la base de dades", "Compruebe la base de datos", "Check the database");

			A("common.search", "Cerca", "Buscar", "Search");
			A("common.cancel", "Cancel·la", "Cancelar", "Cancel");
			A("common.close", "Tanca", "Cerrar", "Close");
			A("common.save", "Desa", "Guardar", "Save");
			A("common.clear", "Neteja", "Limpiar", "Clear");
			A("common.loading", "Carregant…", "Cargando…", "Loading…");
			A("common.pleasewait", "Espereu uns instants.", "Por favor, espere un momento.", "Please wait a moment.");
			A("common.print", "Imprimeix", "Imprimir", "Print");
			A("common.export", "Exporta", "Exportar", "Export");
			A("common.refresh", "Actualitza", "Actualizar", "Refresh");
			A("common.import", "Importa", "Importar", "Import");
			A("common.send", "Envia", "Enviar", "Send");
			A("common.check", "Comprova", "Comprobar", "Check");
			A("common.new", "Nou", "Nuevo", "New");
			A("common.open", "Obre", "Abrir", "Open");
			A("common.yes", "Sí", "Sí", "Yes");
			A("common.no", "No", "No", "No");
			A("common.any", "Qualsevol", "Cualquiera", "Any");
			A("common.all", "Tots", "Todos", "All");
			A("common.none", "Cap", "Ninguno", "None");
			A("common.from", "Des de", "Desde", "From");
			A("common.to", "Fins a", "Hasta", "To");
			A("common.today", "Avui", "Hoy", "Today");
			A("common.date", "Data", "Fecha", "Date");
			A("common.time", "Hora", "Hora", "Time");
			A("common.day", "Dia", "Día", "Day");
			A("common.days", "Dies", "Días", "Days");
			A("common.days7", "7 dies", "7 días", "7 days");
			A("common.days30", "30 dies", "30 días", "30 days");
			A("common.days90", "90 dies", "90 días", "90 days");
			A("common.filters", "Filtres", "Filtros", "Filters");
			A("common.results", "Resultats", "Resultados", "Results");
			A("common.details", "Detall", "Detalle", "Detail");
			A("common.notes", "Notes", "Notas", "Notes");
			A("common.status", "Estat", "Estado", "Status");
			A("common.user", "Usuari", "Usuario", "User");
			A("common.train", "Tren", "Tren", "Train");
			A("common.type", "Tipus", "Tipo", "Type");
			A("common.tags", "Etiquetes", "Etiquetas", "Tags");
			A("common.notfound", "No s'ha trobat res en aquesta adreça.", "No hay nada en esta dirección.", "There's nothing at this address.");
			A("common.understood", "Entès", "Entendido", "Got it");
			A("common.moreinfo", "Més info", "Más info", "More info");
			A("common.action", "Acció", "Acción", "Action");
			A("common.update", "Actualitza", "Actualizar", "Update");
			A("common.viewdetails", "Veure detalls", "Ver detalles", "View details");
			A("common.searching", "Cercant…", "Buscando...", "Searching…");
			A("common.querying", "Consultant el servidor…", "Consultando el servidor...", "Querying the server…");
			A("common.noneselected", "Sense selecció = tots.", "Sin selección = todos.", "No selection = all.");
			A("common.shown", "{0} mostrats", "{0} mostrados", "{0} shown");
			A("common.truncated", "Truncat (augmentau el màxim)", "Truncado (aumenta el máximo)", "Truncated (increase the maximum)");
			A("common.filter.results", "Filtra als resultats…", "Filtrar en resultados...", "Filter results…");
			A("common.noresults", "No hi ha resultats per als filtres seleccionats.", "No hay resultados para los filtros seleccionados.", "No results for the selected filters.");
			A("common.loading.users", "Carregant taula d'usuaris…", "Cargando tabla de usuarios...", "Loading user table…");
			A("common.loading.data", "Carregant dades…", "Cargando datos...", "Loading data…");
			A("common.loading.results", "Carregant resultats…", "Cargando resultados...", "Loading results…");
			A("common.csv", "CSV / Excel", "CSV / Excel", "CSV / Excel");
			A("common.csv.tip", "Exporta CSV (Excel)", "Exportar CSV (Excel)", "Export CSV (Excel)");
			A("common.print.tip", "Imprimeix l'informe", "Imprimir informe", "Print report");
			A("common.maxpertype", "Màx. per tipus", "Máx. por tipo", "Max. per type");
			A("common.maxrecords", "Màx. registres", "Máx. registros", "Max. records");
			A("common.number", "Número", "Número", "Number");
			A("common.place", "Lloc", "Lugar", "Location");
			A("common.realtime", "Temps real", "Tiempo real", "Real time");
			A("common.ok", "D'acord", "OK", "OK");
			A("common.badge.new", "Nou", "Nuevo", "New");
			A("common.name", "Nom", "Nombre", "Name");
			A("common.you", "Tu", "Tú", "You");
			A("common.origin", "Origen", "Origen", "Origin");
			A("common.event", "Esdeveniment", "Evento", "Event");
			A("common.datetime", "Data / hora", "Fecha / hora", "Date / time");
			A("common.expand", "Expandeix", "Expandir", "Expand");
			A("common.collapse", "Compacta", "Compactar", "Compact");
			A("common.sound", "So", "Sonido", "Sound");
			A("common.mute", "Silenci", "Silencio", "Mute");
			A("common.workshop", "Taller", "Taller", "Workshop");
			A("common.available", "Disponibles", "Disponibles", "Available");

			A("role.admin", "Administrador", "Administrador", "Administrator");

			A("sched.title", "El meu quadrante", "Mi cuadrante", "My roster");
			A("inc.title", "Incidències i notes", "Incidencias y notas", "Incidents and notes");
			A("log.title", "Registre d'esdeveniments", "Registro de eventos", "Event log");
			A("page.users", "Usuaris", "Usuarios", "Users");
			A("page.help", "Ajuda · Zafiro", "Ayuda · Zafiro", "Help · Zafiro");
			A("page.help.center", "Centre d'ajuda", "Centro de ayuda", "Help centre");
			A("page.comm", "Comunicació", "Comunicación", "Communication");
			A("page.trains", "Material mòbil", "Material Móvil", "Rolling stock");
			A("page.workshifts", "Gestió de torns", "Gestión de Turnos", "Shift management");
			A("page.agents", "Llista d'agents", "Lista de Agentes", "Agents list");
			A("page.platforms", "Vies d'estacionament", "Vías de estacionamiento", "Stabling tracks");
			A("page.timenet", "Plans d'explotació · Diamond", "Planes de Explotación · Diamond", "Exploitation plans · Diamond");
			A("page.topostorage", "Magatzem Diamond · Topologies i plans", "Almacén Diamond · Topologías y planes", "Diamond store · Topologies and plans");
			A("page.monthgraph", "Gràfic mensual de Tracció", "Gráfico mensual de Tracción", "Monthly traction chart");
			A("page.dailygraph", "Gràfic diari de Tracció", "Gráfico diario de Tracción", "Daily traction chart");
			A("page.instantsnap", "Situació actual", "Situación actual", "Current situation");
			A("page.inc.query", "Incidències i notes · Consulta", "Incidencias y notas · Consulta", "Incidents and notes · Query");
			A("page.inc.print", "Informe d'incidències · Zafiro", "Informe de incidencias · Zafiro", "Incident report · Zafiro");
			A("page.log.print", "Informe d'esdeveniments · Zafiro", "Informe de eventos · Zafiro", "Event report · Zafiro");
			A("page.log.events", "Esdeveniments · Zafiro Log", "Eventos · Zafiro Log", "Events · Zafiro Log");
			A("page.createuser", "Usuari nou", "Nuevo usuario", "New user");
			A("page.dailysheet", "Full del dia · {0}", "Hoja del día · {0}", "Day sheet · {0}");
			A("page.password", "Contrasenya", "Contraseña", "Password");
			A("page.notfound", "No trobat", "No encontrado", "Not found");

			A("home.important", "Important:", "Importante:", "Important:");
			A("home.version.current", "La versió que esteu utilitzant és la {0}.", "La versión que está utilizando usted es la {0}.", "You are using version {0}.");
			A("home.version.new", "El programa s'ha d'actualitzar a la versió {0}.", "El programa deberá ser actualizado a la versión {0}.", "The application must be updated to version {0}.");
			A("home.version.new.fallback", "nova", "nueva", "new");
			A("home.changes", "Canvis que us afecten:", "Cambios que le afectan:", "Changes that affect you:");
			A("home.moreinfo.tip", "Més informació sobre aquest canvi", "Más información sobre este cambio", "More information about this change");
			A("home.reload.hint", "Podeu prémer les tecles Ctrl + F5, esborrar les dades de navegació, buidar l'historial d'aquesta pàgina o eliminar les galetes.", "Para ello puede pulsar las teclas Ctrl + F5, borrar los datos de navegación, vaciar el historial de esta página o eliminar las cookies de navegación.", "Press Ctrl + F5, clear browsing data, empty this page’s history or delete cookies.");
			A("home.reload.warn", "Seguireu veient aquest avís mentre la vostra versió no coincideixi amb l'actual. Mentrestant es podrien produir errors. Les vostres dades al sistema romanen emmagatzemades de forma segura.", "Seguirá viendo este aviso mientras su versión no coincida con la actual. Mientras tanto se podrían producir errores. Sus datos en el sistema permanecen almacenados de forma segura.", "You will keep seeing this notice until your version matches the current one. Errors may occur in the meantime. Your data remains stored safely.");
			A("home.notes", "Notes de la versió {0}", "Notas de la versión {0}", "Release notes {0}");
			A("home.signin", "Inicia sessió", "Iniciar sesión", "Sign in");
			A("home.signin.go", "Ves a l'inici de sessió", "Ir al inicio de sesión", "Go to sign in");
			A("home.panel.a", "Tauler A", "Panel A", "Panel A");
			A("home.panel.b", "Tauler B", "Panel B", "Panel B");
			A("home.panel.c", "Tauler C", "Panel C", "Panel C");
			A("home.metric", "Mètrica {0}", "Métrica {0}", "Metric {0}");
			A("home.load", "Càrrega", "Carga", "Load");

			A("menu.open", "Obre", "Abrir", "Open");
			A("menu.inspector.lead", "Tot el necessari per a les tasques que un inspector d'operacions requereix de la gestió del personal de moviment i tracció.", "Todo lo necesario para las tareas que un inspector de operaciones requiere de la gestión del personal de movimiento y tracción.", "Everything an operations inspector needs to manage movement and traction staff.");
			A("menu.station.lead", "Gestió de personal i material mòbil", "Gestión de personal y material móvil", "Staff and rolling stock management");
			A("menu.caps.lead", "Conjunt d'eines per a responsables del col·lectiu de tracció i supervisió tècnica del material mòbil.", "Conjunto de herramientas para responsables del colectivo de tracción y supervisión técnica del material móvil.", "Tools for traction managers and technical supervision of rolling stock.");
			A("menu.mechanic.lead", "Eines per al personal de taller de SFM i de les empreses col·laboradores", "Herramientas para el personal de taller de SFM y de las empresas colaboradoras", "Tools for SFM workshop staff and partner companies");
			A("menu.oficial.lead", "Eines per al personal de taller de SFM", "Herramientas para el personal de taller de SFM", "Tools for SFM workshop staff");
			A("menu.engineer.lead", "Accés a eines de mineria de dades i avaluació del servei", "Acceso a herramientas de minería de datos y evaluación del servicio", "Access to data mining and service evaluation tools");
			A("menu.diamond.lead", "Gestió d'horaris i explotació", "Gestión de horarios y explotación", "Timetable and operations management");
			A("menu.anonymous.lead", "Material mòbil", "Material Móvil", "Rolling stock");
			A("menu.admin.lead", "Configuració del sistema Zafiro per a administradors", "Configuración del sistema Zafiro para administradores", "Zafiro system settings for administrators");

			A("card.platforms", "Vies", "Vías", "Tracks");
			A("card.platforms.desc", "Estat del material", "Estado del material", "Rolling stock status");
			A("card.tourmaline", "Tourmaline", "Tourmaline", "Tourmaline");
			A("card.tourmaline.desc", "Mode de servei de Tourmaline.", "Modo de servicio de Tourmaline.", "Tourmaline service mode.");
			A("card.avail", "Disponibilitat", "Disponibilidad", "Availability");
			A("card.avail.desc", "Línia temporal de disponibilitat", "Línea temporal de disponibilidad", "Availability timeline");
			A("card.contract", "Contracte SFM Erion", "Contrato SFM Erion", "SFM–Erion contract");
			A("card.contract.desc", "Seguiment segons plec", "Seguimiento según pliego", "Follow-up per specification");
			A("card.legacy", "Explotació (Legacy)", "Explotación (Legacy)", "Operations (Legacy)");
			A("card.legacy.desc", "Horaris, plans d'explotació.", "Horarios, Planes de Explotación.", "Timetables and exploitation plans.");
			A("card.store", "Magatzem Diamond", "Almacén Diamond", "Diamond store");
			A("card.store.desc", "Topologies, plans, limitacions temporals, festius i catàleg de llocs", "Topologías, planes, limitaciones temporales, festivos y catálogo de lugares", "Topologies, plans, temporary limits, festive days and places catalog");
			A("topo.places", "Catàleg de llocs", "Catálogo de lugares", "Places catalog");
			A("card.plans", "Plans d'explotació", "Planes de Explotación", "Exploitation plans");
			A("card.plans.desc", "Malles horàries Diamond (planificador)", "Mallas horarias Diamond (planificador)", "Diamond timetable meshes (planner)");
			A("card.mysheet", "El meu gràfic", "Mi gráfico", "My chart");
			A("card.mysheet.desc", "Torns i trens", "Turnos y trenes", "Shifts and trains");
			A("card.users", "Usuaris del sistema", "Usuarios del Sistema", "System users");
			A("card.users.desc", "Gestió d'usuaris i permisos", "Gestión de usuarios y permisos", "User and permission management");
			A("card.events", "Esdeveniments", "Eventos", "Events");
			A("card.events.desc", "Revisió d'activitat", "Revisión de actividad", "Activity review");
			A("card.comm", "Comunicació", "Comunicación", "Communication");
			A("card.comm.desc", "Avisos als usuaris del sistema.", "Avisos a los usuarios del sistema.", "Notices to system users.");
			A("card.agents", "Agents de Tracció", "Agentes de Tracción", "Traction agents");
			A("card.agents.desc", "Informe amb la llista dels Agents de Tracció per a ús diari del CTC", "Informe con la lista de los Agentes de Tracción para uso diario del CTC", "Daily CTC list of traction agents");
			A("card.workshop", "Taller", "Taller", "Workshop");
			A("card.workshop.desc", "Estat del material mòbil i tallers.", "Estado del material móvil y talleres.", "Rolling stock and workshop status.");
			A("card.rolling", "Material mòbil", "Material Móvil", "Rolling stock");
			A("card.rolling.desc", "Informació sobre el material mòbil", "Información sobre el Material Móvil", "Rolling stock information");
			A("card.inc", "Incidències i notes", "Incidencias y Notas", "Incidents and notes");
			A("card.inc.desc", "Consultes complexes sobre el registre", "Consultas complejas sobre el registro", "Advanced queries on the log");
			A("card.dailysheet", "Full de maquinistes", "Hoja de Maquinistas", "Drivers sheet");
			A("card.dailysheet.desc", "Seguiment de torns.", "Seguimiento de turnos.", "Shift follow-up.");
			A("card.driversgraph.desc", "Gràfic de maquinistes.", "Gráfico de Maquinistas.", "Drivers chart.");

			A("auth.setpwd.title", "Assignació de contrasenya per a {0}", "Asignación de contraseña para {0}", "Set password for {0}");
			A("auth.setpwd.hint", "La contrasenya d'aquest usuari és buida. Assigneu-ne una de nova.", "La contraseña para este usuario está vacía. Por favor, asigne una nueva contraseña.", "This user’s password is empty. Please set a new one.");
			A("auth.setpwd.confirm", "Confirma la contrasenya", "Confirmar Contraseña", "Confirm password");
			A("auth.setpwd.submit", "Inicia sessió", "Iniciar Sesión", "Sign in");
			A("auth.setpwd.ok.title", "Validació", "Validación", "Validation");
			A("auth.setpwd.ok.body", "Contrasenya canviada amb èxit. Inicieu sessió.", "Contraseña cambiada con éxito. Inicie sesión.", "Password changed successfully. Please sign in.");
			A("auth.setpwd.err.title", "Error de registre", "Error de registro", "Registration error");
			A("auth.setpwd.err.internal", "No ha estat possible establir la contrasenya per un error intern. Contactau amb l'administrador.", "No ha sido posible establecer la contraseña debido a un error interno. Póngase en contacto con el administrador.", "The password could not be set because of an internal error. Contact the administrator.");
			A("auth.setpwd.err.mismatch", "La contrasenya no coincideix amb la confirmació. Comproveu-la i torneu-ho a provar.", "La contraseña no coincide con la confirmación. Compruebe la contraseña tecleada e inténtelo de nuevo.", "The password does not match the confirmation. Check it and try again.");
			A("auth.setpwd.retry", "Reintenta", "Reintentar", "Retry");

			A("inc.subtitle.users", "Consulta combinada sobre notes i canvis d'estat (trens, usuaris, dates i etiquetes).", "Consulta combinada sobre notas y cambios de estado (trenes, usuarios, fechas y etiquetas).", "Combined query of notes and status changes (trains, users, dates and tags).");
			A("inc.subtitle", "Consulta combinada sobre notes i canvis d'estat (trens, dates i etiquetes).", "Consulta combinada sobre notas y cambios de estado (trenes, fechas y etiquetas).", "Combined query of notes and status changes (trains, dates and tags).");
			A("inc.source", "Origen de dades", "Origen de datos", "Data source");
			A("inc.statuschanges", "Canvis d'estat", "Cambios de estado", "Status changes");
			A("inc.keywords", "Paraules clau (notes)", "Palabras clave (notas)", "Keywords (notes)");
			A("inc.keywords.ph", "Separades per espai o coma (totes han d'aparèixer al text)", "Separadas por espacio o coma (todas deben aparecer en el texto)", "Separated by space or comma (all must appear in the text)");
			A("inc.keywords.hint", "Només s'aplica a notes. Exemple: porta fre", "Solo se aplica a notas. Ejemplo: puerta freno", "Applies to notes only. Example: door brake");
			A("inc.train.filter", "Filtra per número…", "Filtrar por número...", "Filter by number…");
			A("inc.notrains", "No hi ha trens que coincideixin.", "No hay trenes que coincidan.", "No matching trains.");
			A("inc.user.filter", "CF o nom…", "CF o nombre...", "ID or name…");
			A("inc.nousers", "No hi ha usuaris que coincideixin.", "No hay usuarios que coincidan.", "No matching users.");
			A("inc.notetype", "Tipus de nota", "Tipo de nota", "Note type");
			A("inc.nlptags", "Etiquetes NLP", "Etiquetas NLP", "NLP tags");
			A("inc.valid", "Vàlida", "Válida", "Valid");
			A("inc.invalid", "No vàlida", "No válida", "Invalid");
			A("inc.symptom", "Símptoma", "Síntoma", "Symptom");
			A("inc.symptom.fault", "Símptoma / avaria", "Síntoma / avería", "Symptom / fault");
			A("inc.resolution", "Resolució", "Resolución", "Resolution");
			A("inc.system", "Sistema afectat", "Sistema afectado", "Affected system");
			A("inc.tags.hint", "Les etiquetes i tipus només filtren notes.", "Las etiquetas y tipos solo filtran notas.", "Tags and types only filter notes.");
			A("inc.totals", " · {0} notes / {1} estats (totals)", " · {0} notas / {1} estados (totales)", " · {0} notes / {1} statuses (totals)");
			A("inc.counts", " · {0} notes · {1} estats", " · {0} notas · {1} estados", " · {0} notes · {1} statuses");
			A("inc.timeline", "Cronologia", "Cronología", "Timeline");
			A("inc.hint", "Definiu els filtres i premeu Cerca. Per defecte es consulten els darrers 7 dies (notes i canvis d'estat).", "Define los filtros y pulsa Buscar. Por defecto se consultan los últimos 7 días (notas y cambios de estado).", "Set the filters and press Search. By default the last 7 days are queried (notes and status changes).");
			A("inc.closed", "Tancada {0}", "Cerrada {0}", "Closed {0}");

			A("log.subtitle", "Consulta avançada d'activitat del sistema (SessionEvents)", "Consulta avanzada de actividad del sistema (SessionEvents)", "Advanced system activity query (SessionEvents)");
			A("log.host", "Origen / IP conté", "Origen / IP contiene", "Origin / IP contains");
			A("log.host.ph", "p.ex. 192.168 o telegram", "p.ej. 192.168 o telegram", "e.g. 192.168 or telegram");
			A("log.user.filter", "Filtra la llista per CF o nom…", "Filtrar lista por CF o nombre...", "Filter list by ID or name…");
			A("log.noneselected", "Sense selecció = tots els usuaris.", "Sin selección = todos los usuarios.", "No selection = all users.");
			A("log.selected", "{0} seleccionats", "{0} seleccionados", "{0} selected");
			A("log.eventtypes", "Tipus d'esdeveniment", "Tipos de evento", "Event types");
			A("log.nofilter", "Sense filtre de tipus (tots).", "Sin filtro de tipo (todos).", "No type filter (all).");
			A("log.ntypes", "{0} tipus", "{0} tipos", "{0} types");
			A("log.shownof", " de {0} coincidències", " de {0} coincidencias", " of {0} matches");
			A("log.none", "No hi ha esdeveniments per als filtres seleccionats.", "No hay eventos para los filtros seleccionados.", "No events for the selected filters.");

			A("users.new", "Nou", "Nuevo", "New");

			A("help.lead", "Guies de les seccions a les quals teniu accés amb el vostre perfil.", "Guías de las secciones a las que tiene acceso con su perfil.", "Guides for the sections available with your profile.");
			A("help.roles", "Rols:", "Roles:", "Roles:");
			A("help.search.ph", "Cerca a l'ajuda…", "Buscar en la ayuda...", "Search help…");
			A("help.login", "Inicieu sessió per veure l'ajuda adaptada al vostre perfil.", "Inicie sesión para ver la ayuda adaptada a su perfil.", "Sign in to see help tailored to your profile.");
			A("help.empty", "No hi ha temes d'ajuda per al vostre perfil actual.", "No hay temas de ayuda para su perfil actual.", "There are no help topics for your current profile.");
			A("help.index", "Índex", "Índice", "Index");
			A("help.start", "Per on començo?", "¿Por dónde empiezo?", "Where do I start?");
			A("help.start.body", "Trieu un tema de l'índex o useu el cercador. Només es mostren les funcions disponibles per als vostres rols (no s'inclou l'administració del sistema).", "Elija un tema del índice o use el buscador. Solo se muestran las funciones disponibles para sus roles (no se incluye la administración del sistema).", "Pick a topic from the index or use the search. Only features available for your roles are shown (system administration is not included).");
			A("help.open", "Obre la secció", "Abrir sección", "Open section");

			A("comm.messages", "Gestió de missatges", "Gestión de Mensajes", "Message management");
			A("comm.pre", "Missatge abans d'iniciar sessió", "Mensaje antes de iniciar sesión", "Message before sign-in");
			A("comm.pre.hint", "Aquest missatge es mostra a tots els usuaris abans d'iniciar sessió.", "Este mensaje se muestra a todos los usuarios antes de iniciar sesión.", "This message is shown to all users before they sign in.");
			A("comm.post", "Missatge després d'iniciar sessió", "Mensaje después de iniciar sesión", "Message after sign-in");
			A("comm.post.hint", "Aquest missatge es mostra a tots els usuaris després d'iniciar sessió.", "Este mensaje se muestra a todos los usuarios después de iniciar sesión.", "This message is shown to all users after they sign in.");
			A("comm.save", "Desa els missatges", "Guardar mensajes", "Save messages");
			A("comm.saved", "Missatges desats correctament.", "Mensajes guardados correctamente.", "Messages saved.");
			A("comm.telegram", "Missatge immediat per Telegram", "Mensaje inmediato por Telegram", "Immediate Telegram message");
			A("comm.telegram.hint", "Aquest missatge s'enviarà immediatament per Telegram i no s'emmagatzemarà a la base de dades.", "Este mensaje se enviará inmediatamente por Telegram y no se almacenará en la base de datos.", "This message will be sent immediately via Telegram and will not be stored in the database.");
			A("comm.telegram.send", "Envia per Telegram", "Enviar por Telegram", "Send via Telegram");

			A("trainq.number.ph", "Nom o part…", "Nombre o parte...", "Name or part…");
			A("trainflow.loading", "Carregant cicles de taller…", "Cargando ciclos de taller...", "Loading workshop cycles…");
			A("trainflow.refresh", "Actualitza", "Actualizar", "Refresh");
			A("trainflow.refresh.short", "Act.", "Act.", "Ref.");
			A("trainflow.export.short", "Exp.", "Exp.", "Exp.");
			A("trainflow.compact", "Vista compacta", "Vista compacta", "Compact view");
			A("trainflow.extended", "Vista estesa", "Vista extendida", "Extended view");
			A("trainflow.audio.off", "Desactiva les notificacions d'àudio", "Desactivar notificaciones de audio", "Disable audio notifications");
			A("trainflow.audio.on", "Activa les notificacions d'àudio", "Activar notificaciones de audio", "Enable audio notifications");
			A("trainflow.recent", "canvi(s) recent(s)", "cambio(s) reciente(s)", "recent change(s)");
			A("trainflow.recent.short", "canvis", "cambios", "changes");
			A("trainflow.avail.short", "Disp.", "Disp.", "Avail.");
			A("trainflow.workshop.short", "Tall.", "Tall.", "W/S");
			A("trainflow.refresh.tip", "Actualitza les dades", "Actualizar datos", "Refresh data");
			A("trainflow.export.tip", "Exporta les dades", "Exportar datos", "Export data");

			A("dossier.loading", "Carregant", "Cargando", "Loading");
			A("dossier.loading.data", "Carregant dades…", "Cargando datos...", "Loading data…");
			A("dossier.general", "General", "General", "General");
			A("dossier.chat", "Xat", "Chat", "Chat");
			A("dossier.history", "Historial", "Historial", "History");
			A("dossier.works", "Actuacions", "Actuaciones", "Work orders");
			A("dossier.cleaning", "Neteja", "Limpieza", "Cleaning");

			A("timesnap.loading", "Carregant dades…", "Cargando datos...", "Loading data…");
			A("timesnap.empty", "No hi ha dades per al filtre seleccionat.", "No hay datos para el filtro seleccionado.", "No data for the selected filter.");
			A("timesnap.fleet", "Disponibilitat de flota", "Disponibilidad de flota", "Fleet availability");
			A("timesnap.preventive", "Immobilització per preventius", "Inmovilización por Preventivos", "Immobilisation for preventives");
			A("timesnap.ntrains", " ({0} trens)", " ({0} trenes)", " ({0} trains)");
			A("timesnap.train", "Tren {0}", "Tren {0}", "Train {0}");

			A("platforms.title", "Estat de vies", "Estado de vías", "Track status");
			A("platforms.subtitle", "Ocupació de material a les estacions de la xarxa", "Ocupación de material en estaciones de la red", "Stock occupation at network stations");

			A("daily.circulations", "Circulacions", "Circulaciones", "Circulations");
			A("daily.depots", "Dipòsits", "Depósitos", "Depots");
			A("daily.assignations", "Assignacions", "Asignaciones", "Assignments");
			A("daily.vacancies", "Vacants", "Vacantes", "Vacancies");

			A("month.import", "Importa", "Importar", "Import");
			A("month.importing", "Important el gràfic des d'Excel", "Importando gráfico desde Excel", "Importing chart from Excel");

			A("topo.title", "Magatzem Diamond", "Almacén Diamond", "Diamond store");
			A("topo.subtitle", "Topologies geogràfiques i plans d'explotació (scripts) versionats a Zafiro.", "Topologías geográficas y planes de explotación (scripts) versionados en Zafiro.", "Geographic topologies and exploitation plans (scripts) versioned in Zafiro.");
			A("topo.topologies", "Topologies", "Topologías", "Topologies");
			A("topo.upload", "Puja topo", "Subir topo", "Upload topo");
			A("timenet.include", "Include / topologia", "Include / topología", "Include / topology");
			A("timenet.example", "Ex.:", "Ej.:", "E.g.:");
			A("timenet.loading", "Carregant topologies del magatzem Zafiro…", "Cargando topologías del almacén Zafiro…", "Loading topologies from the Zafiro store…");

			A("work.title", "Gestió de torns del personal", "Gestión de Turnos del Personal", "Staff shift management");
			A("work.clear.assign", "Neteja assignacions", "Limpiar Asignaciones", "Clear assignments");
			A("work.clear.agents", "Neteja la llista d'agents", "Limpiar Lista de Agentes", "Clear agents list");
			A("work.import.excel", "Importa Excel", "Importar Excel", "Import Excel");
			A("work.import.xml", "Importa XML", "Importar XML", "Import XML");
			A("work.import.xml.tip", "Puja un pla d'explotació o una llista d'agents en XML", "Subir plan de explotación o lista de agentes en XML", "Upload an exploitation plan or agents list in XML");
			A("work.loading.plans", "Carregant plans d'explotació", "Cargando planes de explotación", "Loading exploitation plans");

			A("usernew.title", "Usuari nou", "Nuevo Usuario", "New user");
			A("usernew.cf", "Carnet", "Carnet", "ID card");
			A("usernew.cf.hint", "El número de carnet ferroviari és un codi d'identificació únic per als treballadors de l'empresa i per als externs, que s'han d'identificar amb una numeració alternativa i compatible amb la dels interns.", "El número de carnet ferroviario es un código de identificación único para los trabajadores de la empresa y para los externos, que deberán ser identificados con una numeración alternativa y compatible con la de los internos.", "The railway ID number is a unique code for company staff and for external workers, who must be identified with an alternative numbering compatible with internal IDs.");
			A("usernew.name.hint", "El nom d'usuari que introduïu es cercarà a la base de dades actual per evitar donar d'alta dues vegades el mateix usuari. Assegureu-vos que la llista d'usuaris en conflicte aparegui buida o que els noms que contingui pertanyin a altres persones.", "El nombre de usuario que está introduciendo será buscado en la base de datos actual para evitar dar de alta dos veces al mismo usuario en el sistema. Por favor, asegúrese de que la lista de usuarios en conflicto aparezca vacía o bien los nombres que contiene pertenezcan a otras personas.", "The user name you enter will be looked up in the current database to avoid creating the same user twice. Make sure the conflict list is empty or that the names belong to other people.");
			A("usernew.noconflict", "No s'han trobat usuaris en conflicte.", "No se han encontrado usuarios en conflicto.", "No conflicting users found.");
			A("usernew.conflict", "S'han trobat els usuaris en conflicte següents:", "Se han encontrado los siguientes usuarios en conflicto:", "The following conflicting users were found:");

			A("useredit.title", "Usuari {0} ({1})", "Usuario {0} ({1})", "User {0} ({1})");
			A("useredit.personal", "Personal", "Personal", "Personal");
			A("useredit.cf.hint", "Tots els usuaris d'aquest programa tenen un carnet ferroviari si són de SFM o una identificació externa si pertanyen a una subcontracta.", "Todos los usuarios de este programa disponen de un carnet ferroviario si son de SFM o una identificación externa si pertenecen a una subcontrata.", "Every user has a railway ID if they belong to SFM, or an external ID if they work for a contractor.");
			A("useredit.active", "Usuari en actiu", "Usuario en activo", "User is active");
			A("useredit.fullname", "Nom i llinatges", "Nombre y apellidos", "Full name");
			A("useredit.email", "Adreça de correu", "Dirección de correo", "Email address");
			A("useredit.ext", "Extensió d'empresa", "Extensión de empresa", "Company extension");
			A("useredit.loading", "Carregant usuari…", "Cargando usuario...", "Loading user…");
			A("useredit.locale", "Idioma inicial", "Idioma inicial", "Initial language");
			A("useredit.locale.hint", "El veurà a Zafiro i a Tourmaline en obrir sessió, fins que el canviï a les seves preferències.", "Lo verá en Zafiro y en Tourmaline al iniciar sesión, hasta que lo cambie en sus preferencias.", "They will see this in Zafiro and Tourmaline when they sign in, until they change it in their preferences.");
			A("useredit.locale.saved", "Idioma assignat.", "Idioma asignado.", "Language assigned.");
			A("useredit.locale.error", "No s'ha pogut desar l'idioma.", "No se ha podido guardar el idioma.", "The language could not be saved.");
			A("useredit.locale.system", "Encara no n'ha triat: es farà servir el castellà.", "Aún no ha elegido: se usará el castellano.", "Not chosen yet: Spanish will be used.");

			A("sched.viewas", "Vista com", "Vista como", "View as");
			A("sched.cf.ph", "ex. 196", "ej. 196", "e.g. 196");
			A("sched.cf.tip", "CF del maquinista a simular (només desenvolupament)", "CF del maquinista a simular (solo desarrollo)", "CF of the driver to simulate (development only)");
			A("sched.view", "Veure", "Ver", "View");
			A("sched.phone.tip", "Telèfon del maquinista", "Teléfono del maquinista", "Driver’s phone");
			A("sched.sim", "Simulació", "Simulación", "Simulation");
			A("sched.myuser", "El meu usuari", "Mi usuario", "My user");
			A("sched.myuser.tip", "Usa l'usuari de la sessió actual", "Usar el usuario de la sesión actual", "Use the current session user");
			A("sched.needcf", "Indica un CF de maquinista (p. ex. 196) per carregar el seu gràfic, o prem El meu usuari.", "Indica un CF de maquinista (p. ej. 196) para cargar su gráfico, o pulsa Mi usuario.", "Enter a driver’s CF (e.g. 196) to load their chart, or tap My user.");
			A("sched.tab.chart", "Gràfic", "Gráfico", "Chart");
			A("sched.tab.shifts", "Torns", "Turnos", "Shifts");
			A("sched.tab.query", "Consulta", "Consulta", "Query");
			A("sched.mychart", "El meu gràfic", "Mi gráfico", "My chart");
			A("sched.export.ics", "Exporta .ics", "Exportar .ics", "Export .ics");
			A("sched.export.ics.tip", "Descarrega el període visible com a calendari (.ics) per a Google Calendar, Outlook, etc.", "Descargar el periodo visible como calendario (.ics) para Google Calendar, Outlook, etc.", "Download the visible period as a calendar (.ics) for Google Calendar, Outlook, etc.");
			A("sched.noshifts", "No hi ha torns.", "No hay turnos.", "No shifts.");
			A("sched.noassign", "Sense assignació", "Sin asignación", "Unassigned");
			A("sched.swap", "Canvi", "Cambio", "Swap");
			A("sched.td.tip", "Torn en descans", "Turno en descanso", "Rest shift");
			A("sched.col.day", "Dia", "Día", "Day");
			A("sched.col.shift", "Torn", "Turno", "Shift");
			A("sched.col.hours", "Horari", "Horario", "Hours");
			A("sched.col.notes", "Notes", "Notas", "Notes");
			A("sched.pickday", "Selecciona un dia a la llista o amb el selector de data.", "Selecciona un día en la lista o con el selector de fecha.", "Select a day from the list or with the date picker.");
			A("sched.duty", "Jornada", "Jornada", "Duty");
			A("sched.duration", "Durada", "Duración", "Duration");
			A("sched.trains", "Trens", "Trenes", "Trains");
			A("sched.nohours", "Sense horari de treball (descans / llicència / vacances).", "Sin horario de trabajo (descanso / licencia / vacaciones).", "No working hours (rest / leave / holiday).");
			A("sched.noshiftday", "Sense torn grafiat aquest dia.", "Sin turno grafiado este día.", "No rostered shift on this day.");
			A("sched.jmnote", "Nota JM", "Nota JM", "Chief note");
			A("sched.export.trains", "Exporta trens .ics", "Exportar trenes .ics", "Export trains .ics");
			A("sched.export.day.tip", "Descarrega trens i dipòsits d'aquest torn com a calendari (.ics)", "Descargar trenes y depósitos de este turno como calendario (.ics)", "Download this shift’s trains and depots as a calendar (.ics)");
			A("sched.askswap", "Demana canvi de torn…", "Pedir cambio de turno…", "Request a shift swap…");
			A("sched.live", "Ressalt en viu · {0}", "Resalte en vivo · {0}", "Live highlight · {0}");
			A("sched.circs", "Circulacions del torn", "Circulaciones del turno", "Shift circulations");
			A("sched.notrains", "No hi ha trens ni dipòsits associats a aquest torn.", "No hay trenes ni depósitos asociados a este turno.", "No trains or depots associated with this shift.");
			A("sched.plan.lookup", "Cercant trens al pla d'explotació vigent", "Buscando trenes en el plan de explotación vigente", "Looking up trains in the current exploitation plan");
			A("sched.sheet.open", "Obre el full de circulació", "Abrir hoja de circulación", "Open circulation sheet");
			A("sched.plan.checking", "Comprovant el pla d'explotació…", "Comprobando el plan de explotación…", "Checking the exploitation plan…");
			A("sched.plan.missing", "Aquest tren no figura al pla d'explotació d'aquesta data", "Este tren no figura en el plan de explotación de esa fecha", "This train is not in the exploitation plan for that date");
			A("sched.live.now", "En curs", "En curso", "In progress");
			A("sched.done", "Fet", "Hecho", "Done");
			A("sched.optional", "Opcional", "Opcional", "Optional");
			A("sched.depots", "Dipòsits / disponibilitat", "Depósitos / disponibilidad", "Depots / availability");
			A("sched.away", "Fora", "Fuera", "Away");
			A("sched.depot", "Dipòsit", "Depósito", "Depot");
			A("sched.queries", "Consultes", "Consultas", "Queries");
			A("sched.queries.hint", "Qui fa un torn", "Quién hace un turno", "Who works a shift");
			A("sched.shift.ph", "ex. 3, 1f, DD…", "ej. 3, 1f, DD…", "e.g. 3, 1f, DD…");
			A("sched.query.result", "Resultat per a {0} el {1}", "Resultado para {0} el {1}", "Result for {0} on {1}");
			A("sched.query.empty", "Ningú assignat a aquest torn aquest dia (o torn no grafiat).", "Nadie asignado a ese turno ese día (o turno no grafiado).", "Nobody assigned to that shift that day (or shift not rostered).");
			A("sched.askswap.short", "Demana canvi", "Pedir cambio", "Request swap");
			A("sched.soon", "Properament:", "Próximamente:", "Coming soon:");
			A("sched.soon.body", "el canvi de torn s'enviarà a l'altre maquinista i al JM. De moment podeu esbossar la sol·licitud amb el diàleg.", "el cambio de turno se enviará al otro maquinista y al JM. De momento puedes esbozar la solicitud con el diálogo.", "the shift swap will be sent to the other driver and the chief. For now you can draft the request in the dialog.");
			A("sched.swap.title", "Demana canvi de torn", "Pedir cambio de turno", "Request a shift swap");
			A("sched.swap.draft", "Esbós de la petició. Encara no s'envia al servidor.", "Esbozo de la petición. Aún no se envía al servidor.", "Draft request. It is not sent to the server yet.");
			A("sched.swap.yours", "El teu torn", "Tu turno", "Your shift");
			A("sched.swap.target", "Maquinista destí", "Maquinista destino", "Target driver");
			A("sched.swap.offer", "Torn que ofereix / demana", "Turno que ofrece / pide", "Shift offered / requested");
			A("sched.swap.message", "Missatge", "Mensaje", "Message");
			A("sched.swap.message.ph", "Motiu del canvi, preferència de contacte…", "Motivo del cambio, preferencia de contacto…", "Reason for the swap, preferred contact…");
			A("sched.nav.aria", "Seccions del quadrante", "Secciones del cuadrante", "Roster sections");
			A("sched.swap.submit", "Envia la sol·licitud (esbós)", "Enviar solicitud (esbozo)", "Send request (draft)");
			A("sched.crossing", "Encreuaments · maquinista", "Cruzamientos · maquinista", "Crossings · driver");

			A("tm.login.user", "Usuari", "Usuario", "User");
			A("tm.login.password", "Contrasenya", "Contraseña", "Password");
			A("tm.login.user.ph", "Introduïu l'usuari", "Ingrese su usuario", "Enter your username");
			A("tm.login.password.ph", "Introduïu la contrasenya", "Ingrese su contraseña", "Enter your password");
			A("tm.login.brand", "Informació al viatger", "Información al Viajero", "Passenger information");
			A("tm.dest", "Destí", "Destino", "Destination");
			A("tm.itinerary", "Itinerari", "Itinerario", "Itinerary");
			A("tm.cameras", "Càmeres", "Cámaras", "Cameras");
			A("tm.end", "Finalitza", "Finalizar", "End");
			A("tm.loading", "Preparant l'experiència de viatge…", "Estamos preparando tu experiencia de viaje...", "Getting your journey ready…");
			A("tm.pleasewait", "Espereu uns segons.", "Por favor, espera unos segundos.", "Please wait a few seconds.");
			A("tm.starting", "S'està iniciant el sistema", "Iniciando sistema", "Starting system");
			A("tm.search.ph", "Número, hora o destí…", "Número, hora o destino…", "Number, time or destination…");
			A("tm.search.upcoming", "Només sortides posteriors a ara", "Sólo salidas posteriores a ahora", "Only departures after now");
			A("tm.search.all", "Tots els trens del dia", "Todos los trenes del día", "All trains today");

			A("rel.docs.title",
				"Documentació oficial: llibre itinerari i consigna sèrie B",
				"Documentación oficial: libro itinerario y consigna serie B",
				"Official documents: itinerary book and Series B notice");
			A("rel.docs.body",
				"Al magatzem Diamond hi ha una pestanya de documentació per redactar i imprimir el llibre itinerari (versió normal: límits fixos; completa: també els temporals, amb el Max en groc i el motiu en vermell) i la consigna sèrie B.\n\nLa consigna va per eix, amb portada i índex, número yy/xxx per generació (es reinicia cada any i deroga l'anterior). S'ometen els eixos sense anunci. Via I a la dreta; les estacions no es repeteixen i, si una limitació en cobreix diverses, queden a dins. Les columnes V es pinten en gris clar (51 km/h o més) o gris molt fosc (50 o menys). Sota el comentari hi ha la data d'alta (DD-MM-YY).\n\nCada document oficial porta logotip (color a la portada; gris i més petit a les capçaleres), segell SEL i QR. Qualsevol usuari pot comprovar el segell i, si hi ha còpia, obrir el mateix document. Les consultes des del quadrante del maquinista no emeten document oficial: es pot triar normal o completa (per defecte completa).",
				"En el almacén Diamond hay una pestaña de documentación para redactar e imprimir el libro itinerario (versión normal: límites fijos; completa: también los temporales, con el Max en amarillo y el motivo en rojo) y la consigna serie B.\n\nLa consigna va por eje, con portada e índice, número yy/xxx por generación (se reinicia cada año y deroga la anterior). Se omiten los ejes sin anuncio. Vía I a la derecha; las estaciones no se repiten y, si una limitación cubre varias, quedan dentro. Las columnas V se pintan en gris claro (51 km/h o más) o gris muy oscuro (50 o menos). Bajo el comentario está la fecha de alta (DD-MM-YY).\n\nCada documento oficial lleva logotipo (color en la portada; gris y más pequeño en las cabeceras), sello SEL y QR. Cualquier usuario puede comprobar el sello y, si hay copia, abrir el mismo documento. Las consultas desde el cuadrante del maquinista no emiten documento oficial: se puede elegir normal o completa (por defecto completa).",
				"The Diamond store has a documentation tab to draft and print the itinerary book (normal: fixed limits; complete: temporary limits as well, with Max in yellow and the reason in red) and the Series B notice.\n\nThe notice is per axis, with a cover and index, and a yy/xxx number per generation (it resets each year and repeals the previous one). Axes with nothing to announce are omitted. Track I is on the right; station names are not repeated, and if a restriction spans several stations they sit inside it. The V columns are light grey (51 km/h or more) or very dark grey (50 or less). The creation date (DD-MM-YY) appears under the comment.\n\nEach official document has the company logo (colour on the cover; grey and smaller in headers), a SEL seal and a QR code. Any user can check a seal and, if a copy exists, open the same document. Look-ups from the driver’s roster are not official issues: you can choose normal or complete (complete by default).");

			A("rel.nav.title", "Navegació, barra lateral i mode zen", "Navegación, barra lateral y modo zen", "Navigation, sidebar and zen mode");
			A("rel.nav.body",
				"La capçalera de Zafiro incorpora botons d'enrere i endavant propis de l'aplicació (no els del navegador), per recórrer les pantalles que heu visitat en aquesta sessió.\n\nLa barra lateral es redueix amb la xinxeta (clavat = fixa amb textos; desclavat = només icones i tooltip). El triangle de la cantonada ja no contrau el menú.\n\nF11 entra en mode zen: amaga la barra lateral, la capçalera de navegació i l'ajuda, i demana pantalla completa al navegador. Torneu a prémer F11 o Esc per sortir.",
				"La cabecera de Zafiro incorpora botones de atrás y adelante propios de la aplicación (no los del navegador), para recorrer las pantallas que ha visitado en esta sesión.\n\nLa barra lateral se reduce con el icono de chincheta (pinchada = fija con textos; despinchada = solo iconos y tooltip). El triángulo de la esquina ya no colapsa el menú.\n\nF11 entra en modo zen: oculta la barra lateral, la cabecera de navegación y la ayuda, y pide pantalla completa al navegador. Vuelva a pulsar F11 o Esc para salir.",
				"The Zafiro header has its own back and forward buttons (not the browser’s), so you can move through the screens you have visited in this session.\n\nThe sidebar collapses with the pin icon (pinned = fixed with labels; unpinned = icons and tooltips only). The corner triangle no longer collapses the menu.\n\nF11 enters zen mode: it hides the sidebar, the navigation header and help, and asks the browser for full screen. Press F11 or Esc again to leave.");

			A("rel.media.title", "Notes multimèdia al tren", "Notas multimedia en el tren", "Multimedia notes on the train");
			A("rel.media.body",
				"A l'expedient del tren podeu adjuntar fotos, vídeo o un PDF a una nota (botó Multimèdia / càmera al mòbil).\n\nL'arxiu es desa al servidor i apareix al xat d'incidències. Si falta o no es pot llegir, es mostra la icona de no disponible. Hi ha un límit de mida i d'arxius per usuari i dia.\n\nEls avisos de Telegram d'aquest tren inclouen l'adjunt quan existeix.",
				"En el expediente del tren puede adjuntar fotos, vídeo o un PDF a una nota (botón Multimedia / cámara en el móvil).\n\nEl archivo se guarda en el servidor y aparece en el chat de incidencias. Si falta o no se puede leer, se muestra el icono de no disponible. Hay un límite de tamaño y de archivos por usuario y día.\n\nLos avisos de Telegram de ese tren incluyen el adjunto cuando existe.",
				"In the train file you can attach photos, video or a PDF to a note (Multimedia / camera button on a phone).\n\nThe file is stored on the server and appears in the incident chat. If it is missing or cannot be read, an unavailable icon is shown. There is a size limit and a per-user daily file limit.\n\nTelegram notices for that train include the attachment when it exists.");

			A("rel.diamond.title", "Planificador de malla Diamond", "Planificador de malla Diamond", "Diamond mesh planner");
			A("rel.diamond.body",
				"S'ha redissenyat l'espai de treball del planificador: les barres usen icones, la malla i l'script es poden minimitzar o ampliar per separat, i la malla admet vista a pantalla completa.\n\nPodeu acoblar l'script a la dreta o a baix i treballar només amb la malla quan ho necessiteu.",
				"Se ha rediseñado el espacio de trabajo del planificador: las barras usan iconos, la malla y el script se pueden minimizar o ampliar por separado, y la malla admite vista a pantalla completa.\n\nPuede acoplar el script a la derecha o abajo y trabajar solo con la malla cuando lo necesite.",
				"The planner workspace has been redesigned: the bars use icons, the mesh and the script can be minimised or expanded separately, and the mesh supports a full-screen view.\n\nYou can dock the script to the right or the bottom and work with the mesh alone when you need to.");

			A("rel.i18n.title", "Suport multi idioma", "Soporte multi idioma", "Multi-language support");
			A("rel.i18n.body",
				"Zafiro, Tourmaline i el bot de Telegram usen l'idioma del vostre perfil: català balear (oficial), castellà o anglès.\n\nPodeu canviar-lo a Jo → Preferències. L'administrador pot assignar l'idioma inicial d'un usuari perquè, en obrir sessió, ho vegi en la seva llengua.\n\nEl text de les notes dels trens es deixa en l'idioma en què es va redactar.",
				"Zafiro, Tourmaline y el bot de Telegram usan el idioma de su perfil: catalán balear (oficial), castellano o inglés.\n\nPuede cambiarlo en Yo → Preferencias. El administrador puede asignar el idioma inicial de un usuario para que, al iniciar sesión, lo vea en su lengua.\n\nEl texto de las notas de los trenes se deja en el idioma en que se redactó.",
				"Zafiro, Tourmaline and the Telegram bot use the language of your profile: Balearic Catalan (official), Spanish or English.\n\nYou can change it in Me → Preferences. An administrator can assign a user’s initial language so that, when they next sign in, they see the interface in their own language.\n\nTrain notes stay in the language they were written in.");

			A("rel.sched.title", "Full de treball del maquinista", "Hoja de trabajo del maquinista", "Driver’s work sheet");
			A("rel.sched.body",
				"Al quadrante podeu obrir el full de circulació d'un tren si figura al pla d'explotació Diamond d'aquell dia, no només avui.\n\nLa columna d'observacions mostra els encreuaments. Si teniu permís de simulació, el número del tren que encreua enllaça amb el quadrante del maquinista que el condueix aquell dia.\n\nEl telèfon del maquinista vist en simulació es pot marcar des del mòbil. També es pot exportar el període o el torn a un calendari (.ics).",
				"En el cuadrante puede abrir la hoja de circulación de un tren si figura en el plan de explotación Diamond de ese día, no solo hoy.\n\nLa columna de observaciones muestra los cruzamientos. Si tiene permiso de simulación, el número del tren que cruza enlaza con el cuadrante del maquinista que lo conduce ese día.\n\nEl teléfono del maquinista visto en simulación se puede marcar desde el móvil. También se puede exportar el periodo o el turno a un calendario (.ics).",
				"On the roster you can open a train’s circulation sheet if it appears in Diamond’s exploitation plan for that day, not only today.\n\nThe observations column shows crossings. If you have simulation permission, the crossing train number links to the roster of the driver working that train that day.\n\nThe phone of the driver you are viewing in simulation can be dialled from a mobile. You can also export the period or the shift to a calendar (.ics).");

			A("tg.empty", "Error intern: missatge buit", "Error interno: Mensaje vacío", "Internal error: empty message");
			A("tg.unpair", "Has desconnectat el teu usuari d'aquest bot.", "Has desconectado tu usuario de este bot.", "You have disconnected your user from this bot.");
			A("tg.unknown.name", "Desconegut", "Desconocido", "Unknown");
			A("tg.user.unknown", "un usuari desconegut", "un usuario desconocido", "an unknown user");
			A("tg.user.someone", "Un usuari", "Un usuario", "A user");
			A("tg.table.empty", "No hi ha dades per mostrar", "No hay datos para mostrar", "No data to show");

			A("tg.hello.1", "Hola #username. Què t'agradaria fer?", "Hola #username. ¿Qué te gustaría hacer?", "Hi #username. What would you like to do?");
			A("tg.hello.2", "Benvingut #username. Digues-me què vols de mi.", "Bienvenido #username. Dime qué quieres de mí.", "Welcome #username. Tell me what you need.");
			A("tg.hello.3", "Què tal #username? Explica'm què puc fer per tu.", "¿Qué tal #username? Cuéntame qué puedo hacer por ti.", "How are you #username? Tell me how I can help.");
			A("tg.err.1", "No he entès el que vols dir. Vols obrir un part d'avaria o d'incidència?", "No he entendido lo que quieres decir. ¿Quieres abrir un parte de avería o de incidencia?", "I didn't understand that. Do you want to open a fault or incident report?");
			A("tg.err.2", "Si us plau, escriu o parla més clar. T'agradaria conèixer l'estat dels trens disponibles?", "Por favor escribe o habla más claro. ¿Te gustaría conocer el estado de los trenes disponibles?", "Please write or speak more clearly. Would you like to see the available trains?");
			A("tg.err.3", "Estic aprenent versió a versió. De moment no sóc capaç d'entendre el que m'acabes de dir. Puc obrir parts d'incidències, mostrar informes d'un tren o mostrar històrics d'ús.", "Estoy aprendiendo versión a versión. De momento no soy capaz de entender lo que acabas de decirme. Puedo abrir partes de incidencias, mostrar informes de un tren o mostrar históricos de uso.", "I'm still learning. I can't understand that yet. I can open incident reports, show a train report or show usage history.");
			A("tg.err.4", "Perdó? Què em volies dir?", "¿Perdón? ¿Qué querías decirme?", "Sorry? What did you mean?");
			A("tg.err.5", "Pots repetir-ho amb altres paraules?", "¿Puedes repetir con otras palabras?", "Can you say that another way?");
			A("tg.ask.other.1", "No t'he entès. Pots preguntar una altra cosa?", "No te he entendido. ¿Puedes preguntar otra cosa?", "I didn't get that. Can you ask something else?");
			A("tg.ask.other.2", "No estic preparat per a aquesta pregunta. Prova'n una altra.", "No estoy preparado para manejar esta pregunta. Prueba con otra.", "I'm not ready for that question. Try another one.");
			A("tg.report.placeholder", "Ara mostraria un informe d'estat dels trens de la base de dades.", "Ahora estaría mostrando un informe de estado de los trenes en la base de datos.", "This would show a status report of the trains in the database.");

			A("tg.pair.first.1", "Hola. Sóc el bot de Zafiro. Encara no t'has identificat. Abans d'accedir al servei des del teu compte de Telegram has de generar una clau prement el botó de la pàgina \"{0}\" del panell esquerre de l'aplicació i enviar-me-la.", "Hola. Soy el bot de Zafiro. No te has identificado todavía. Antes de acceder al servicio desde tu cuenta de Telegram necesito que generes una clave pulsando el botón de la página \"{0}\" del panel izquierdo de la aplicación y me la envíes.", "Hello. I am the Zafiro bot. You are not identified yet. Before using the service from Telegram, generate a key with the button on the \"{0}\" page in the left panel of the app and send it to me.");
			A("tg.pair.first.2", "Hola. Sóc el bot de Zafiro. Encara no et tenc a la base de dades. Per comunicar-nos has de generar una clau prement el botó de la pàgina \"{0}\" del panell esquerre de l'aplicació i enviar-me-la.", "Hola. Soy el bot de Zafiro. Parece que todavía no te tengo en la base de datos. Para que podamos comunicarnos tienes que generar una clave pulsando el botón de la página \"{0}\" del panel izquierdo de la aplicación y me la envíes.", "Hello. I am the Zafiro bot. I don't have you in the database yet. To talk, generate a key with the button on the \"{0}\" page in the left panel of the app and send it to me.");
			A("tg.pair.bad.1", "El codi que m'has enviat sembla incorrecte. Abans d'accedir al servei des del teu compte de Telegram has de generar una clau prement el botó de la pàgina \"{0}\" del panell esquerre de l'aplicació i enviar-me-la.", "El código que me has enviado parece incorrecto. Antes de acceder al servicio desde tu cuenta de Telegram necesito que generes una clave pulsando el botón de la página \"{0}\" del panel izquierdo de la aplicación y me la envíes.", "The code you sent looks wrong. Before using the service from Telegram, generate a key with the button on the \"{0}\" page in the left panel of the app and send it to me.");
			A("tg.pair.bad.2", "El codi que acabes de teclejar no és vàlid. Encara no et tenc a la base de dades. Per comunicar-nos has de generar una clau prement el botó de la pàgina \"{0}\" del panell esquerre de l'aplicació i enviar-me-la.", "El código que acabas de teclear no es válido. Todavía no te tengo en la base de datos. Para que podamos comunicarnos tienes que generar una clave pulsando el botón de la página \"{0}\" del panel izquierdo de la aplicación y me la envíes.", "The code you just typed is not valid. I don't have you in the database yet. To talk, generate a key with the button on the \"{0}\" page in the left panel of the app and send it to me.");

			A("tg.ask.train.1", "De quin material mòbil parlam?", "¿De qué material móvil estamos hablando?", "Which rolling stock are we talking about?");
			A("tg.ask.train.2", "Necessit saber quin tren o vehicle està implicat", "Necesito saber qué tren o vehículo está implicado", "I need to know which train or vehicle is involved");
			A("tg.ask.train.3", "Em falten dades; a quina unitat tren o cotxe et refereixes?", "Me hacen falta datos; ¿A qué unidad tren o coche te estás refiriendo?", "I need more data: which train unit or car do you mean?");
			A("tg.ask.train.4", "Quin tren és?", "¿Qué tren es?", "Which train is it?");
			A("tg.ask.train.5", "Quin material mòbil s'ha vist afectat?", "¿Qué material móvil se ha visto afectado?", "Which rolling stock is affected?");
			A("tg.ask.train.6", "Em falta saber quin tren és.", "Me falta saber qué tren es.", "I still need to know which train it is.");
			A("tg.ask.sym.1", "Què li passa #utf ?", "¿Qué le ocurre #utf ?", "What is wrong with #utf?");
			A("tg.ask.sym.2", "Quins símptomes té #ut ?", "¿Qué síntomas tiene #ut ?", "What symptoms does #ut have?");
			A("tg.ask.sym.3", "Si us plau, descriu la incidència de #ut.", "Por favor, describe la incidencia de #ut .", "Please describe the incident on #ut.");
			A("tg.confirm.inc.1", "S'està obrint un part d'incidència #ut amb aquesta descripció: \"#sym\". És correcte?", "Se está abriendo un parte de incidencia #ut con la siguiente descripción: \"#sym\". ¿Es correcto?", "An incident report is being opened #ut with this description: \"#sym\". Is that correct?");
			A("tg.confirm.inc.2", "#utf està a punt d'obrir un part d'incidència per #sym. Procedesc?", "#utf está a punto de abrir un parte de incidencia por #sym. ¿Procedo?", "#utf is about to open an incident report for #sym. Shall I proceed?");
			A("tg.confirm.inc.3", "Si ho acceptau #utf, s'obrirà un part d'avaria amb aquesta descripció: \"#sym\" D'acord?", "Si acepta #utf, se abrirá un parte de avería con esta descripción: \"#sym\" ¿De acuerdo?", "If you accept, #utf will open a fault report with this description: \"#sym\". OK?");
			A("tg.confirm.inc.4", "#utf acumularà un part d'avaria amb aquesta descripció: \"#sym\" És correcte?", "#utf va a acumular un parte de avería con esta descripción: \"#sym\" ¿Es correcto?", "#utf will add a fault report with this description: \"#sym\". Is that correct?");
			A("tg.confirm.note.1", "Esteu creant una nota a Zafiro per a #ut amb aquesta descripció: \"#sym\". És correcte?", "Está creando una nota en Zafiro para #ut con la siguiente descripción: \"#sym\". ¿Es correcto?", "You are creating a Zafiro note for #ut with this description: \"#sym\". Is that correct?");
			A("tg.confirm.note.2", "#utf està a punt d'obrir una nota a Zafiro per #sym. Procedesc?", "#utf está a punto de abrir una nota en Zafiro por #sym. ¿Procedo?", "#utf is about to open a Zafiro note for #sym. Shall I proceed?");
			A("tg.confirm.note.3", "Si ho acceptau #utf, s'obrirà una nota nova amb aquesta descripció: \"#sym\" D'acord?", "Si acepta #utf, se abrirá una nueva nota con esta descripción: \"#sym\" ¿De acuerdo?", "If you accept, #utf will open a new note with this description: \"#sym\". OK?");
			A("tg.confirm.note.4", "#utf acumularà una nota amb aquesta descripció: \"#sym\" És correcte?", "#utf va a acumular una nota con esta descripción: \"#sym\" ¿Es correcto?", "#utf will add a note with this description: \"#sym\". Is that correct?");
			A("tg.done.inc.1", "Obert un part d'incidència #utf amb aquesta descripció: \"#sym\".", "Abierto un parte de incidencia #utf con la siguiente descripción: \"#sym\".", "Opened an incident report #utf with this description: \"#sym\".");
			A("tg.done.inc.2", "#ut té obert un part d'incidència per #sym.", "#ut tiene abierto un parte de incidencia por #sym.", "#ut now has an incident report for #sym.");
			A("tg.done.inc.3", "#ut acumula un part d'incidència nou amb aquesta descripció: \"#sym\".", "#ut acumula un nuevo parte de incidencia con esta descripción: \"#sym\".", "#ut has a new incident report with this description: \"#sym\".");
			A("tg.err.internal", "Error intern a TrainIncidenceConcept", "Error interno en TrainIncidenceConcept", "Internal error in TrainIncidenceConcept");

			A("tg.train.none.f.acc", "a cap unitat", "a ninguna unidad", "to no unit");
			A("tg.train.none.f", "cap unitat", "ninguna unidad", "no unit");
			A("tg.train.none.m.acc", "a cap tren", "a ningún tren", "to no train");
			A("tg.train.none.m", "cap tren", "ningún tren", "no train");
			A("tg.train.one.f.acc", "a la unitat {0}", "a la unidad {0}", "to unit {0}");
			A("tg.train.one.f", "la unitat {0}", "la unidad {0}", "unit {0}");
			A("tg.train.one.m.acc", "al tren {0}", "al tren {0}", "to train {0}");
			A("tg.train.one.m", "el tren {0}", "el tren {0}", "train {0}");
			A("tg.train.many.f.acc", "a les unitats {0}", "a las unidades {0}", "to units {0}");
			A("tg.train.many.f", "les unitats {0}", "las unidades {0}", "units {0}");
			A("tg.train.many.m.acc", "als trens {0}", "a los trenes {0}", "to trains {0}");
			A("tg.train.many.m", "els trens {0}", "los trenes {0}", "trains {0}");
			A("tg.train.and", "i ", "y ", "and ");

			A("tg.media.photo", "una foto", "una foto", "a photo");
			A("tg.media.video", "un vídeo", "un vídeo", "a video");
			A("tg.media.pdf", "un PDF", "un PDF", "a PDF");
			A("tg.media.file", "un arxiu", "un archivo", "a file");
			A("tg.media.short.photo", "(foto)", "(foto)", "(photo)");
			A("tg.media.short.video", "(vídeo)", "(vídeo)", "(video)");
			A("tg.media.short.pdf", "(PDF)", "(PDF)", "(PDF)");
			A("tg.media.short.file", "(arxiu)", "(archivo)", "(file)");

			A("tg.notify.endmaint", "La UT {0} acaba de reincorporar-se a la circulació després que {1} acabés els treballs planificats.", "La UT {0} acaba de reincorporarse a la circulación tras terminar {1} los trabajos planificados.", "Unit {0} has just returned to service after {1} finished the planned work.");
			A("tg.notify.endcorr", "La UT {0} acaba de reincorporar-se a la circulació després que {1} donés per acabada la reparació.", "La UT {0} acaba de reincorporarse a la circulación tras dar {1} por terminada la reparación.", "Unit {0} has just returned to service after {1} closed the repair.");
			A("tg.notify.incidence", "{1} ha obert incidència a la UT {0}: \"{2}\"", "{1} abrió incidencia a la UT {0}: \"{2}\"", "{1} opened an incident on unit {0}: \"{2}\"");
			A("tg.notify.withdraw", "{1} demana retirar el tren {0} de la circulació. \"{2}\"", "{1} pide retirar el tren {0} de la circulación. \"{2}\"", "{1} asks to withdraw train {0} from service. \"{2}\"");
			A("tg.notify.continue", "{1} considera que la UT {0} pot seguir en servei. La darrera nota és: \"{2}\"", "{1} considera que la UT {0} puede seguir en servicio. La última nota es:\"{2}\"", "{1} considers that unit {0} can stay in service. Latest note: \"{2}\"");
			A("tg.notify.begincorr", "{1} ha donat entrada a taller a la UT {0} per correctiu.", "{1} ha dado entrada en taller a la UT {0} para correctivo.", "{1} has sent unit {0} into the workshop for corrective work.");
			A("tg.notify.depotreq", "{1} sol·licita apartar la UT {0} per manteniment planificat.", "{1} solicita apartar la UT {0} para mantenimiento planificado.", "{1} requests to set unit {0} aside for planned maintenance.");
			A("tg.notify.depotacc", "{1} acaba d'apartar la UT {0} per manteniment planificat.", "{1} acaba de apartar la UT {0} para mantenimiento planificado.", "{1} has just set unit {0} aside for planned maintenance.");
			A("tg.notify.depotback", "{1} torna a la circulació la UT {0} que havia sol·licitat taller per manteniment planificat.", "{1} devuelve a la circulación la UT {0} que había solicitado taller para mantenimiento planificado.", "{1} returns unit {0} to service after a planned-maintenance workshop request.");
			A("tg.notify.standstill", "{1} envia la UT {0} a l'estat \"Stand-Still\".", "{1} envía la UT {0} al estado \"Stand-Still\" .", "{1} sends unit {0} to Stand-Still.");
			A("tg.notify.activate", "{1} acaba d'activar la UT {0} al sistema.", "{1} acaba de activar la UT {0} en el sistema.", "{1} has just activated unit {0} in the system.");
			A("tg.notify.rescue", "{1} ha reactivat la UT {0} des de Stand-Still. Ara està assignada a taller per revisió.", "{1} ha reactivado la UT {0} desde el estado de Stand-Still. Ahora está asignada a taller para revisión.", "{1} has reactivated unit {0} from Stand-Still. It is now assigned to the workshop for inspection.");
			A("tg.note.technical", "{0} ha escrit \"{1}\" (Nota tècnica del tren {2})", "{0} ha escrito \"{1}\" (Nota técnica del tren {2})", "{0} wrote \"{1}\" (technical note on train {2})");
			A("tg.note.media", "{0} ha enviat {1} del tren {2}.", "{0} ha enviado {1} del tren {2}.", "{0} sent {1} of train {2}.");
			A("tg.note.media.text", "{0} ha enviat {1} del tren {2}: \"{3}\"", "{0} ha enviado {1} del tren {2}: \"{3}\"", "{0} sent {1} of train {2}: \"{3}\"");

			return d;
		}
	}
}
