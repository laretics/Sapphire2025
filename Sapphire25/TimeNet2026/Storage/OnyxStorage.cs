using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
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
		internal Dictionary<string, Plan> mcolPlans; //Colección de planes de explotación.
		private OnyxDatabase mvarStorage;
		private List<TopoStorage> mcolTopoStorages;

		public OnyxStorage(OnyxDatabase db)
		{
			mcolPlans = new Dictionary<string, Plan>();
			mcolTopoStorages = new List<TopoStorage>();
			mvarStorage = db;
			mvarStorage.Database.EnsureCreated(); //Se asegura de que existe la base de datos.
			mcolTopoStorages = new List<TopoStorage>();			
		}
		public async Task EmptyDatabase()
		{
			await mvarStorage.TotalRemove();
		}
		public async Task Init()
		{
			//await mvarStorage.TotalRemove();
			mcolTopoStorages = await mvarStorage.GetTopoStorages();
		}
	
		public List<TopoStorage> Storages { get => mcolTopoStorages; }

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
			await mvarStorage.Insert(nuevo);
		}
		internal async Task deserializeRauta(XmlNode root)
		{
			TopoStorage? currentStorage = null;
			foreach(XmlNode hijo in root.ChildNodes)
			{
				switch(hijo.Name)
				{
					case "info": //Cabecera del hijo
						Header auxCabecera = new Header();
						auxCabecera.deserialize(hijo);
						foreach(TopoStorage candidato in mcolTopoStorages)
						{

						}
						break;
					case "plans": //Colección de planes

						break;

				}
			}


		}









	}
}
