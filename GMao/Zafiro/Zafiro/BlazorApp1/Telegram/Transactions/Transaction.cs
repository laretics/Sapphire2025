namespace ZafiroGmao.Telegram.Transactions
{
	/// <summary>
	/// Una transacción es un diágolo entre un usuario y el bot de telegram.
	/// Las transacciones están relacionadas con el usuario de telegram de forma que cada usuario
	/// sólo puede tener mantenida una transacción al mismo tiempo.
	/// </summary>
	public abstract class Transaction
	{
		public long chatId { get; private set; }
		protected bool mvarIsEnded = false;
		public virtual bool isEnded { get => mvarIsEnded||(DateTime.Now > mvarExpiration); }
		public static TimeSpan Timeout { get; set; } //Establece el tiempo de vida de una transacción
		internal DateTime mvarExpiration { get;private set; } //Tiempo en que se termina esta transacción por duración
		static Transaction()
		{
			Timeout = new TimeSpan(0, 5, 0);//Por defecto, el tiempo máximo para responder es de 5 minutos.
		}
		public Transaction(long chatId)
		{
			this.chatId = chatId;
			mvarExpiration = DateTime.Now.Add(Timeout);
		}
		public virtual async Task <string> initialMessage()
		{
			return "¿En qué puedo ayudarle?";
		}

		public virtual async Task<string> processMessage(string rhs)
		{
			mvarExpiration = DateTime.Now.Add(Timeout); //Refresco la expiración.
														//Por defecto devolvemos un eco con la cadena limpia y en mayúsculas.
			return rhs.Trim().ToUpperInvariant();
		}
		public override string ToString() //Descripción de este diálogo
		{
			return "Conversación genérica por defecto.";
		}


	}
}
