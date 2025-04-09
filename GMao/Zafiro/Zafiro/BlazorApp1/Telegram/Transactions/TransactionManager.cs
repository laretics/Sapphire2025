using System.IO.Pipelines;

namespace ZafiroGmao.Telegram.Transactions
{
	/// <summary>
	/// Administrador de sesiones de Telegram
	/// </summary>
	public class TransactionManager
	{
		internal Dictionary<long,Transaction> mcolTransactions = new Dictionary<long,Transaction>();//Contenedor de transacciones

		public TransactionManager() { }
		public Transaction? getTransaction(long id)
		{
			flush(); //Elimina transacciones caducadas
			//Devuelve la transacción actual para un usuario determinado.
			if(mcolTransactions.ContainsKey(id))
				return mcolTransactions[id];

			return null;
		}

		public void openTransaction(Transaction trans)
		{
			//Añade una transacción a la colección
			//Si existía una transacción abierta previamente la elimina.
			if(mcolTransactions.ContainsKey(trans.chatId))
			{
				mcolTransactions.Remove(trans.chatId);
			}
			mcolTransactions.Add(trans.chatId, trans);
		}

		public void flush()
		{
			//Elimina las transacciones caducadas
			List<Transaction> auxListaEliminar = new List<Transaction>();
			foreach(Transaction trans in mcolTransactions.Values)
			{
				if(trans.isEnded) auxListaEliminar.Add(trans);
			}
			foreach (Transaction auxTrans in auxListaEliminar)
			{
				mcolTransactions.Remove(auxTrans.chatId);
			}
		}


	}
}
