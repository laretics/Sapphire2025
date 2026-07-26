namespace Diamond.Topo
{
	/// <summary>
	/// Metadatos del paquete topográfico (elemento XML info).
	/// </summary>
	public sealed class LayoutInfo
	{
		private string mvarName;
		private string mvarDescription;
		private string mvarComment;
		private string mvarLicense;
		private string mvarAuthor;
		private string mvarFirstDate;
		private string mvarLastDate;
		private string mvarVersion;
		private string mvarBitmap;
		private string mvarId;

		public LayoutInfo()
		{
			mvarName = string.Empty;
			mvarDescription = string.Empty;
			mvarComment = string.Empty;
			mvarLicense = string.Empty;
			mvarAuthor = string.Empty;
			mvarFirstDate = string.Empty;
			mvarLastDate = string.Empty;
			mvarVersion = string.Empty;
			mvarBitmap = string.Empty;
			mvarId = string.Empty;
		}

		public string Name
		{
			get { return mvarName; }
			set { mvarName = value ?? string.Empty; }
		}

		public string Description
		{
			get { return mvarDescription; }
			set { mvarDescription = value ?? string.Empty; }
		}

		public string Comment
		{
			get { return mvarComment; }
			set { mvarComment = value ?? string.Empty; }
		}

		public string License
		{
			get { return mvarLicense; }
			set { mvarLicense = value ?? string.Empty; }
		}

		public string Author
		{
			get { return mvarAuthor; }
			set { mvarAuthor = value ?? string.Empty; }
		}

		/// <summary>
		/// Fecha de alta en el formato del fichero (texto libre, p. ej. 10/04/2023).
		/// </summary>
		public string FirstDate
		{
			get { return mvarFirstDate; }
			set { mvarFirstDate = value ?? string.Empty; }
		}

		public string LastDate
		{
			get { return mvarLastDate; }
			set { mvarLastDate = value ?? string.Empty; }
		}

		public string Version
		{
			get { return mvarVersion; }
			set { mvarVersion = value ?? string.Empty; }
		}

		public string Bitmap
		{
			get { return mvarBitmap; }
			set { mvarBitmap = value ?? string.Empty; }
		}

		public string Id
		{
			get { return mvarId; }
			set { mvarId = value ?? string.Empty; }
		}
	}
}
