using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TimeNet2026.Timed;
using TimeNet2026.Topo;
using Microsoft.Data.Sqlite;

namespace TimeNet2026.Storage
{
	public class OnyxStorage
	{
		internal string mvarFileName { get; set; } //Archivo de la conexión.
		internal List<TopoStorage> mcolTopoStorage; //Colección de topologías de distintos sitios
		internal Dictionary<string, Plan> mcolPlans; //Colección de planes de explotación.

		public OnyxStorage()
		{
			mcolPlans = new Dictionary<string, Plan>();
			mcolTopoStorage = new List<TopoStorage>();
			mvarFileName = string.Empty;			
		}

		public List<TopoStorage> TopoStorage { get => mcolTopoStorage; }

		public string StorageFile 
		{ 
			get => mvarFileName;
			set => mvarFileName = value;
		}

		/// <summary>
		/// Carga el nodo que viene y deserializa automáticamente lo que contenga.
		/// </summary>
		/// <param name="root"></param>
		public void deserializeXML(XmlNode root)
		{
			switch (root.Name)
			{
				case "layout":
					deserializeTopo(root);
					break;
				case "rautatie":
					deserializeRauta(root);
					break;
				default:
					break;
			}

		}

		internal void deserializeTopo(XmlNode root)
		{
			//Root es el nodo "layout"
			TopoStorage nuevo = new TopoStorage(root);
			RemoveTopo(nuevo.Header.Id); //Elimino cualquier topografía existente que tenga el mismo Id.
			mcolTopoStorage.Add(nuevo);
		}
		internal void deserializeRauta(XmlNode root)
		{

		}

		internal void CreateStructure(SqliteConnection connection)
		{
			CreateTable(connection, "RefPunctual", RefPunctual.Descriptor());

		}

		internal void CreateTable
			(SqliteConnection connection,
			string tableName, List<OnyxField> fields
			)
		{
			using SqliteCommand comando = connection.CreateCommand();
			StringBuilder fifi = new StringBuilder();
			foreach (OnyxField campo in fields)
			{
				fifi.Append(campo.Descriptor);
				if (campo != fields.Last())
					fifi.AppendLine(",");
			}

			comando.CommandText = string.Format("CREATE TABLE IF NOT EXISTS {0}\n ({1} \n);",
				tableName,fifi.ToString());
			comando.ExecuteNonQuery();
		}


		private void RemoveTopo(Guid id)
		{
			List<TopoStorage> auxCol = new List<TopoStorage>();
			foreach(TopoStorage candidate in mcolTopoStorage)
			{
				if (candidate.Header.Id != id)
					auxCol.Add(candidate);
			}
			mcolTopoStorage = auxCol;
		}







	}
}
