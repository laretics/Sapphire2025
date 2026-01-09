using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TimeNet2026.DBStorage;
using TimeNet2026.Timed;
using TimeNet2026.Topo;

namespace TimeNet2026.Storage
{
	public class OnyxStorage
	{
		private OnyxDatabase mvarStorage;
		private Dictionary<Guid,TopoStorage> mcolTopoStorages;

		public OnyxStorage(OnyxDatabase db)
		{
			mcolTopoStorages = new Dictionary<Guid, TopoStorage>();
			mvarStorage = db;
			mvarStorage.Database.EnsureCreated(); //Se asegura de que existe la base de datos.
		}
		public async Task EmptyDatabase()
		{
			await mvarStorage.TotalRemove();
		}
		public async Task Init()
		{
			//Primero sacamos los topos
			mcolTopoStorages = await mvarStorage.DeserializeTopoStorages();
			//Luego cargamos los rautas.
			foreach(TopoStorage auxTopo in mcolTopoStorages.Values)
			{
                Dictionary<Guid,Rauta> rautatie = await mvarStorage.DeserializeRautatie(auxTopo);
				auxTopo.mcolRauta = rautatie;
            }
                
		}
	
		public Dictionary<Guid,TopoStorage> Storages { get => mcolTopoStorages; }

		/// <summary>
		/// Carga el nodo que viene y deserializa automáticamente lo que contenga.
		/// </summary>
		/// <param name="root"></param>
		public async Task deserializeXML(XmlNode root)
		{
			switch (root.Name)
			{
				case "layout":
					await deserializeTopo(root);
					break;
				case "rautatie":
					await deserializeRauta(root);
					break;
				default:
					break;
			}
		}

		internal async Task deserializeTopo(XmlNode root)
		{
			//Root es el nodo "layout"
			TopoStorage nuevo = new TopoStorage(root);
			await mvarStorage.SerializeTopoStorage(nuevo);
		}
		internal async Task deserializeRauta(XmlNode root)
		{
			//Lo primero que tenemos que hacer es buscar el TopoStorage compatible
			//await Init();
			Guid auxId = Rauta.TopoStorageId(root);
			if(Guid.Empty!=auxId && mcolTopoStorages.ContainsKey(auxId))
			{
				TopoStorage auxTopoStorage = mcolTopoStorages[auxId];
				Rauta auxRauta = new Rauta(root, auxTopoStorage);
				auxTopoStorage.mcolRauta.Add(auxRauta.Header.Id, auxRauta);
				await mvarStorage.SerializeRautatie(auxTopoStorage);
            }                            
		}

	}
}
