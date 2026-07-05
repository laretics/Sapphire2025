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
			return await addNoteStatic(note, mvarConfig);
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
			if(null!=model)
			{
				using (DataStorage almacen = new DataStorage(mvarConfig))
				{
					IQueryable<Note> query = almacen.Notes.AsNoTracking();

					if (model.ParentId.HasValue && Guid.Empty != model.ParentId)
						query = query.Where(x => x.Parent == model.ParentId.Value);

					if(model.Type.HasValue)
						query = query.Where(x => x.Type == model.Type.Value);

					if(model.UserId.HasValue && Guid.Empty != model.UserId)
						query = query.Where(x => x.UserId == model.UserId.Value);

					if(model.FromTimeStamp.HasValue)
						query =	query.Where(x => x.TimeStamp >=model.FromTimeStamp.Value);

					if(model.ToTimeStamp.HasValue)
						query = query.Where(x => x.TimeStamp <=model.ToTimeStamp.Value);




				}
			}
			return salida;
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

		public static async Task<bool> addNoteStatic(NoteModel note, IConfiguration config)
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
					almacen.Notes.Add(nuevaNota);
					salida = (await almacen.SaveChangesAsync() > 0);
				}
			}
			return salida;
		}

		protected NoteModel noteFromNote(Note rhs)
		{
			NoteModel salida = new NoteModel();
			salida.parent = rhs.Parent;
			salida.Text = rhs.Text;
			salida.TimeStamp = rhs.TimeStamp;
			salida.UserId = rhs.UserId;
			salida.Type = rhs.Type;
			salida.ClosureTime = rhs.ClosureTime;
			salida.ClosureUser = rhs.ClosureUser;
			return salida;
		}
	}
}
