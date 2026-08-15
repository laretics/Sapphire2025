using Microsoft.AspNetCore.Mvc;
using Sapphire2025Models;
using Sapphire2025Models.Aeneas;
using Sapphire2026.Data;
using Sapphire2026.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Sapphire2025Server.Controllers
{
	public partial class SapphireAeneasController
	{
		[HttpPost("addnote")]
		public async Task<bool> AddNote(NoteModel note)
		{
			if (note.Type == 3) //Nota técnica
			{
				User? usuario = await retrieveUserStatic(note.UserId, mvarConfig);
				TrainModel? tren = await TrainInfo(note.parent.ToString());
				if (null != usuario && null != tren)
					await SendTelegramBroadcast(
						string.Format("{0} ha escrito \"{1}\" (Nota técnica del tren {2})", usuario.UserName, note.Text, tren.name),
						false,
						new Common.UserRole[] { Common.UserRole.Inspector, Common.UserRole.Expert, Common.UserRole.Oficial, Common.UserRole.Mechanic }
						);
			}
			// El registro de actividad se hace dentro de addNoteStatic (también cubre Telegram).
			return await addNoteStatic(note, mvarConfig, clientHostPoint());
		}

		[HttpPost("getnotes")]
		public async Task<List<NoteModel>> RetrieveNotes(NoteChatRequestModel model)
		{
			List<NoteModel> salida = new List<NoteModel>();
			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				List<Note> auxNotas;
				if (model.TakeMax > 0)
					auxNotas = await almacen.Notes.Where(x => x.Parent == model.ParentId).OrderByDescending(x => x.TimeStamp).Take(model.TakeMax).ToListAsync();
				else
					auxNotas = await almacen.Notes.Where(x => x.Parent == model.ParentId).OrderByDescending(x => x.TimeStamp).ToListAsync();

				foreach (Note auxNota in auxNotas)
					salida.Add(noteFromNote(auxNota));
			}
			return salida;
		}

		[HttpPost("searchnotes")]
		public async Task<IEnumerable<NoteModel>> SearchNotes(NoteSearchRequestModel model)
		{
			List<NoteModel> salida = new List<NoteModel>();
			if (null == model)
				return salida;

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				IQueryable<Note> query = almacen.Notes.AsNoTracking();

				if (model.ParentId.HasValue && Guid.Empty != model.ParentId)
					query = query.Where(x => x.Parent == model.ParentId.Value);

				if (model.TrainIds is { Count: > 0 })
				{
					List<Guid> trainIds = model.TrainIds.Where(id => id != Guid.Empty).Distinct().ToList();
					if (trainIds.Count > 0)
						query = query.Where(x => trainIds.Contains(x.Parent));
				}

				if (model.Types is { Count: > 0 })
					query = query.Where(x => model.Types.Contains(x.Type));
				else if (model.Type.HasValue)
					query = query.Where(x => x.Type == model.Type.Value);

				if (model.UserId.HasValue && Guid.Empty != model.UserId)
					query = query.Where(x => x.UserId == model.UserId.Value);

				if (model.UserIds is { Count: > 0 })
				{
					List<Guid> userIds = model.UserIds.Where(id => id != Guid.Empty).Distinct().ToList();
					if (userIds.Count > 0)
						query = query.Where(x => userIds.Contains(x.UserId));
				}

				if (model.FromTimeStamp.HasValue)
					query = query.Where(x => x.TimeStamp >= model.FromTimeStamp.Value);

				if (model.ToTimeStamp.HasValue)
					query = query.Where(x => x.TimeStamp <= model.ToTimeStamp.Value);

				if (model.IsValid.HasValue)
					query = query.Where(x => x.IsValid == model.IsValid.Value);

				if (model.IsSymptom.HasValue)
					query = query.Where(x => x.IsSymptom == model.IsSymptom.Value);

				if (model.SystemsAffected is { Count: > 0 })
					query = query.Where(x => model.SystemsAffected.Contains(x.SystemAffected));

				if (model.Keywords is { Count: > 0 })
				{
					foreach (string raw in model.Keywords)
					{
						string kw = (raw ?? string.Empty).Trim();
						if (kw.Length == 0)
							continue;
						// EF traduce Contains a LIKE; se evalúa por palabra (AND).
						query = query.Where(x => x.Text != null && x.Text.Contains(kw));
					}
				}

				int take = model.TakeMax is > 0 and <= 5000 ? model.TakeMax.Value : 500;
				List<Note> notas = await query
					.OrderByDescending(x => x.TimeStamp)
					.Take(take)
					.ToListAsync();

				foreach (Note auxNota in notas)
					salida.Add(noteFromNote(auxNota));
			}
			return salida;
		}

		/// <summary>
		/// Consulta compleja unificada de notas y cambios de estado.
		/// Registra el uso en SessionEvents (quién consultó y qué filtros aplicó).
		/// </summary>
		[HttpPost("incidencequery")]
		public async Task<IncidenceQueryResponse> IncidenceQuery(IncidenceQueryRequest? request)
		{
			IncidenceQueryResponse salida = new IncidenceQueryResponse();
			if (null == request)
				return salida;

			int take = request.MaxRecords;
			if (take <= 0) take = 500;
			if (take > 5000) take = 5000;

			List<Guid> trainFilter = (request.TrainIds ?? new List<Guid>())
				.Where(id => id != Guid.Empty).Distinct().ToList();
			List<Guid> userFilter = (request.UserIds ?? new List<Guid>())
				.Where(id => id != Guid.Empty).Distinct().ToList();
			List<byte> noteTypes = (request.NoteTypes ?? new List<byte>()).Distinct().ToList();
			List<byte> systems = (request.SystemsAffected ?? new List<byte>()).Distinct().ToList();
			List<byte> operations = (request.Operations ?? new List<byte>()).Distinct().ToList();
			List<string> keywords = (request.Keywords ?? new List<string>())
				.Select(k => (k ?? string.Empty).Trim())
				.Where(k => k.Length > 0)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			// Pre-carga de nombres de trenes y usuarios (para enriquecer resultados).
			Dictionary<Guid, string> trainNames = new Dictionary<Guid, string>();
			Dictionary<Guid, (string Name, string Cf)> userInfo = new Dictionary<Guid, (string, string)>();

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				List<Train> trenes = await almacen.Trains.AsNoTracking().ToListAsync();
				foreach (Train t in trenes)
					trainNames[t.Guid] = t.Name ?? string.Empty;

				// Notas
				if (request.IncludeNotes)
				{
					IQueryable<Note> noteQuery = almacen.Notes.AsNoTracking();

					if (request.FromUtc.HasValue)
						noteQuery = noteQuery.Where(x => x.TimeStamp >= request.FromUtc.Value);
					if (request.ToUtc.HasValue)
						noteQuery = noteQuery.Where(x => x.TimeStamp <= request.ToUtc.Value);
					if (trainFilter.Count > 0)
						noteQuery = noteQuery.Where(x => trainFilter.Contains(x.Parent));
					if (userFilter.Count > 0)
						noteQuery = noteQuery.Where(x => userFilter.Contains(x.UserId));
					if (noteTypes.Count > 0)
						noteQuery = noteQuery.Where(x => noteTypes.Contains(x.Type));
					if (request.IsValid.HasValue)
						noteQuery = noteQuery.Where(x => x.IsValid == request.IsValid.Value);
					if (request.IsSymptom.HasValue)
						noteQuery = noteQuery.Where(x => x.IsSymptom == request.IsSymptom.Value);
					if (systems.Count > 0)
						noteQuery = noteQuery.Where(x => systems.Contains(x.SystemAffected));

					foreach (string kw in keywords)
						noteQuery = noteQuery.Where(x => x.Text != null && x.Text.Contains(kw));

					salida.TotalNotes = await noteQuery.CountAsync();
					List<Note> notes = await noteQuery
						.OrderByDescending(x => x.TimeStamp)
						.Take(take)
						.ToListAsync();

					foreach (Note n in notes)
					{
						IncidenceQueryItem item = new IncidenceQueryItem
						{
							Kind = "note",
							Id = n.Id,
							TimeStamp = n.TimeStamp,
							TrainId = n.Parent,
							TrainName = trainNames.TryGetValue(n.Parent, out string? tn) ? tn : string.Empty,
							UserId = n.UserId,
							NoteType = n.Type,
							Text = n.Text,
							IsValid = n.IsValid,
							IsSymptom = n.IsSymptom,
							SystemAffected = n.SystemAffected,
							ClosureTime = n.ClosureTime,
							ClosureUserId = n.ClosureUser
						};
						salida.Items.Add(item);
					}
				}

				// Cambios de estado
				if (request.IncludeStatusChanges)
				{
					// Si hay filtros exclusivos de nota (keywords, etiquetas NLP, tipos de nota),
					// los cambios de estado no aplican a menos que el usuario los pida sin esos filtros.
					// Igual se consultan si IncludeStatusChanges; keywords/etiquetas no se aplican aquí.
					IQueryable<StatusChange> scQuery = almacen.StatusChanges.AsNoTracking();

					if (request.FromUtc.HasValue)
						scQuery = scQuery.Where(x => x.TimeStamp >= request.FromUtc.Value);
					if (request.ToUtc.HasValue)
						scQuery = scQuery.Where(x => x.TimeStamp <= request.ToUtc.Value);
					if (trainFilter.Count > 0)
						scQuery = scQuery.Where(x => trainFilter.Contains(x.TrainId));
					if (userFilter.Count > 0)
						scQuery = scQuery.Where(x => userFilter.Contains(x.UserId));
					if (operations.Count > 0)
						scQuery = scQuery.Where(x => operations.Contains(x.mvarOperationId));

					// Status se calcula en memoria (no está mapeado en columna).
					List<StatusChange> allMatching = await scQuery
						.OrderByDescending(x => x.TimeStamp)
						.ToListAsync();

					if (request.Statuses is { Count: > 0 })
					{
						HashSet<byte> statusSet = request.Statuses.ToHashSet();
						allMatching = allMatching
							.Where(x => statusSet.Contains((byte)x.Status))
							.ToList();
					}

					salida.TotalStatusChanges = allMatching.Count;
					foreach (StatusChange sc in allMatching.Take(take))
					{
						IncidenceQueryItem item = new IncidenceQueryItem
						{
							Kind = "status",
							Id = sc.Guid,
							TimeStamp = sc.TimeStamp,
							TrainId = sc.TrainId,
							TrainName = trainNames.TryGetValue(sc.TrainId, out string? tn) ? tn : string.Empty,
							UserId = sc.UserId,
							Operation = (byte)sc.Operation,
							Status = (byte)sc.Status
						};
						salida.Items.Add(item);
					}
				}

				// Enriquecer con nombres de usuario
				HashSet<string> neededUserIds = salida.Items
					.Select(i => i.UserId.ToString())
					.Where(s => !string.IsNullOrWhiteSpace(s))
					.ToHashSet(StringComparer.OrdinalIgnoreCase);

				if (neededUserIds.Count > 0)
				{
					List<User> users = await almacen.Users.AsNoTracking()
						.Where(u => neededUserIds.Contains(u.Id))
						.ToListAsync();
					foreach (User u in users)
					{
						if (!userInfo.ContainsKey(u.guid))
							userInfo[u.guid] = (u.UserName ?? string.Empty, u.CF ?? string.Empty);
					}
				}

				foreach (IncidenceQueryItem item in salida.Items)
				{
					if (userInfo.TryGetValue(item.UserId, out var info))
					{
						item.UserName = info.Name;
						item.UserCf = info.Cf;
					}
				}
			}

			// Orden cronológico descendente del conjunto unificado
			salida.Items = salida.Items
				.OrderByDescending(i => i.TimeStamp)
				.ToList();

			int notesReturned = salida.Items.Count(i => i.Kind == "note");
			int statusReturned = salida.Items.Count(i => i.Kind == "status");
			salida.Truncated =
				(request.IncludeNotes && salida.TotalNotes > notesReturned) ||
				(request.IncludeStatusChanges && salida.TotalStatusChanges > statusReturned);

			// Auditoría: quién consultó y qué filtros usó
			try
			{
				if (!Guid.Empty.Equals(request.SessionToken))
				{
					User? actor = await retrieveSessionUser(request.SessionToken);
					if (null != actor && !string.IsNullOrWhiteSpace(actor.Id))
					{
						string detail = BuildIncidenceQueryLogDetail(request, salida, take);
						string ip = clientHostPoint() ?? string.Empty;
						string host = string.IsNullOrWhiteSpace(detail)
							? ip
							: (string.IsNullOrWhiteSpace(ip) ? detail : $"{detail}|{ip}");
						await addLoginRecord(actor.Id, Common.sessionEventType.incidenceQuery, host);
					}
				}
			}
			catch
			{
				// No tumbar la consulta por un fallo de log.
			}

			return salida;
		}

		private static string BuildIncidenceQueryLogDetail(
			IncidenceQueryRequest request,
			IncidenceQueryResponse response,
			int take)
		{
			List<string> parts = new List<string>();
			if (request.FromUtc.HasValue)
				parts.Add($"from={request.FromUtc.Value:yyyy-MM-dd}");
			if (request.ToUtc.HasValue)
				parts.Add($"to={request.ToUtc.Value:yyyy-MM-dd}");
			if (request.TrainIds is { Count: > 0 })
				parts.Add($"trains={request.TrainIds.Count}");
			if (request.UserIds is { Count: > 0 })
				parts.Add($"users={request.UserIds.Count}");
			if (request.IncludeNotes)
				parts.Add("notes");
			if (request.IncludeStatusChanges)
				parts.Add("status");
			if (request.NoteTypes is { Count: > 0 })
				parts.Add($"ntypes={string.Join(',', request.NoteTypes)}");
			if (request.IsValid.HasValue)
				parts.Add($"valid={request.IsValid.Value}");
			if (request.IsSymptom.HasValue)
				parts.Add($"symptom={request.IsSymptom.Value}");
			if (request.SystemsAffected is { Count: > 0 })
				parts.Add($"sys={string.Join(',', request.SystemsAffected)}");
			if (request.Keywords is { Count: > 0 })
				parts.Add($"kw={request.Keywords.Count}");
			parts.Add($"hit={response.TotalMatched}");
			parts.Add($"take={take}");
			return string.Join(';', parts);
		}

		/// <summary>
		/// Etiquetado manual de una nota para entrenamiento del clasificador.
		/// Solo Root, Engineer u Oficial. La nota debe ser posterior a la última
		/// IsValid=true del mismo tren. Tras guardar, IsValid pasa a true.
		/// </summary>
		[HttpPost("labelnote")]
		public async Task<bool> LabelNote(NoteLabelRequestModel? request)
		{
			if (request is null || request.NoteId == Guid.Empty)
				return false;

			if (request.SystemAffected == (byte)Common.TrainSystem.undefined ||
				!Enum.IsDefined(typeof(Common.TrainSystem), request.SystemAffected))
				return false;

			User? actor = await retrieveSessionUser(request.SessionToken);
			if (null == actor)
				return false;

			List<Common.UserRole> roles = await retrieveBasicRoles(actor.Id);
			bool allowed =
				roles.Contains(Common.UserRole.Root) ||
				roles.Contains(Common.UserRole.Engineer) ||
				roles.Contains(Common.UserRole.Oficial);
			if (!allowed)
				return false;

			using (DataStorage almacen = new DataStorage(mvarConfig))
			{
				Note? nota = await almacen.Notes.FirstOrDefaultAsync(x => x.Id == request.NoteId);
				if (null == nota)
					return false;

				DateTime? lastValid = await almacen.Notes
					.AsNoTracking()
					.Where(x => x.Parent == nota.Parent && x.IsValid)
					.MaxAsync(x => (DateTime?)x.TimeStamp);

				if (lastValid.HasValue && nota.TimeStamp <= lastValid.Value)
					return false;

				nota.IsSymptom = request.IsSymptom;
				nota.SystemAffected = request.SystemAffected;
				nota.IsValid = true;
				return await almacen.SaveChangesAsync() > 0;
			}
		}

		public static async Task<string> lastNoteStatic(Guid trainId, IConfiguration config)
		{
			using (DataStorage almacen = new DataStorage(config))
			{
				Note? auxNota = await almacen.Notes.AsNoTracking()
					.Where(x => x.Parent == trainId)
					.OrderByDescending(x => x.TimeStamp)
					.FirstOrDefaultAsync();
				if (null != auxNota && null != auxNota.Text)
					return auxNota.Text;
			}
			return string.Empty;
		}

		public static async Task<bool> addNoteStatic(NoteModel note, IConfiguration config, string hostPoint = "static")
		{
			bool salida = false;
			//Todos los usuarios tienen permiso para añadir notas.
			using (DataStorage almacen = new DataStorage(config))
			{
				if (null != note.Text && note.Text.Length > 0)
				{
					Note nuevaNota = new Note();
					nuevaNota.Id = Guid.NewGuid();
					nuevaNota.Parent = note.parent;
					nuevaNota.TimeStamp = DateTime.UtcNow;
					nuevaNota.UserId = note.UserId;
					nuevaNota.Text = note.Text;
					nuevaNota.Type = note.Type;
					nuevaNota.ClosureUser = note.ClosureUser;
					nuevaNota.ClosureTime = note.ClosureTime;
					nuevaNota.IsValid = note.IsValid;
					nuevaNota.IsSymptom = note.IsSymptom;
					nuevaNota.SystemAffected = note.SystemAffected;
					almacen.Notes.Add(nuevaNota);
					salida = (await almacen.SaveChangesAsync() > 0);
					// También cubre invocaciones desde Telegram u otras vías estáticas.
					if (salida && !Guid.Empty.Equals(note.UserId))
						await addSessionEventStatic(config, note.UserId, Common.sessionEventType.noteAdded, hostPoint);
				}
			}
			return salida;
		}

		protected NoteModel noteFromNote(Note rhs)
		{
			NoteModel salida = new NoteModel();
			salida.Id = rhs.Id;
			salida.parent = rhs.Parent;
			salida.Text = rhs.Text;
			salida.TimeStamp = rhs.TimeStamp;
			salida.UserId = rhs.UserId;
			salida.Type = rhs.Type;
			salida.ClosureTime = rhs.ClosureTime;
			salida.ClosureUser = rhs.ClosureUser;
			salida.IsValid = rhs.IsValid;
			salida.IsSymptom = rhs.IsSymptom;
			salida.SystemAffected = rhs.SystemAffected;
			return salida;
		}
	}
}
