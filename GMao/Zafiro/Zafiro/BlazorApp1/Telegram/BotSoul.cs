using Microsoft.EntityFrameworkCore.Storage.Json;
using NuGet.Protocol.Plugins;
using System;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Requests;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using ZafiroGmao.Data;
using ZafiroGmao.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Transactions;



namespace ZafiroGmao.Telegram
{    
    /// <summary>
    /// Interacción con el bot SFM_GMAOBot
    /// Localizable en t.me/SFM_GMAOBot
    /// </summary>
    public class BotSoul
    {
        private static string Token = "popo";
        private TelegramBotClient mvarClient;
        internal IServiceScopeFactory mvarScopeFactory; //Necesario para cargar UserManager
        internal UserManager<SFMUser> userManager; //Lo necesito para verificar passwords
        internal Transactions.TransactionManager mcolTransactions; //Gestor de transacciones de Telegram.
        private long mvarChatId;

        public BotSoul(IServiceScopeFactory scopeFactory)
        {
            mvarClient = new TelegramBotClient(Token);
            mvarChatId = -1;
            mvarScopeFactory = scopeFactory;
            mvarClient.OnMessage += onIncomingMessage;
            mcolTransactions = new Transactions.TransactionManager();
        }

        public async Task sendMessage(string rhs)
        {
            if (-1 == mvarChatId) return;
            await mvarClient.SendMessage(mvarChatId, rhs);
        }
        public async Task sendToSubscriptors(string rhs)
        {
            using(ApplicationDbContext auxDb = new ApplicationDbContext())
            {
                IQueryable<SFMUser> subscriptors = auxDb.Users.Where(f => f.TelegramId != 0);
                foreach(SFMUser s in subscriptors)
                {
                    await mvarClient.SendMessage(s.TelegramId, rhs);
                }
            }
        }

        private async Task onIncomingMessage(global::Telegram.Bot.Types.Message message, UpdateType type)
        {            
            mvarChatId = message.Chat.Id;
            //Ahora necesito la referencia al usuario que tiene este chat. Si no la encuentro, pasaré a abrir una transacción de registro
            SFMUser? auxCurrentUser = null;
            using (ApplicationDbContext auxDb = new ApplicationDbContext())
            {
				auxCurrentUser = await auxDb.Users.Where(f => f.TelegramId == mvarChatId).FirstOrDefaultAsync();
            }
            //Obtengo la transacción abierta por este usuario
            Transactions.Transaction? auxTrans = mcolTransactions.getTransaction(mvarChatId);
            {
                if (null == auxTrans)
                {
                    //No tengo una transacción abierta. Debo comprobar que el usuario esté registrado
                    if (null == auxCurrentUser)
                    {
                        auxTrans = new Transactions.Register(mvarChatId, mvarScopeFactory);
                        mcolTransactions.openTransaction(auxTrans);
                        await mvarClient.SendMessage(mvarChatId, await auxTrans.initialMessage());
                    }
                    else
                    {
                        if (null != message && null != message.Text)
                        {
                            //Proceso la petición para ir a otra transacción
                            auxTrans = parseTransaction(message.Text,mvarChatId);
                            mcolTransactions.openTransaction(auxTrans);
                            await mvarClient.SendMessage(mvarChatId,await auxTrans.initialMessage());
                        }
                    }
                }
                else
                {
                    if (null != message && null != message.Text)
                    {
                        //Sigo el proceso de la transacción
                        string respuesta = await auxTrans.processMessage(message.Text);
                        await mvarClient.SendMessage(mvarChatId, respuesta);
                    }
                }
            }
        }



        private Transactions.Transaction parseTransaction(string message, long chatId)
        {
            return new Transactions.Help(chatId); //Por defecto devolvemos un chat de ayuda
        }
    //        if (null != message && null != message.Text)
    //        {


				//if (null == auxTrans)
    //            {
				//	//Reconoce el comando y abre una nueva transacción.
				//	switch (message.Text.ToUpper())
				//	{
				//		case "/START":
				//		case "/REG":
				//		case "/REGISTER":
				//			mvarChatId = message.Chat.Id;
				//			//Primero voy a ver si tengo ya registrado este usuario.



				//			await mvarClient.SendMessage(mvarChatId, "A partir de ahora, todos los mensajes de GMAO se transmitirán a este chat.");
				//			break;
				//		case "/UNREG":
				//		case "/UNREGISTER":
				//			mvarChatId = -1;
				//			await mvarClient.SendMessage(message.Chat.Id, "Se anula la suscripción a mensajes de GMAO.");
				//			break;
				//		default:
				//			if (!await auxProcessTrainReport(message))
				//				await mvarClient.SendMessage(message.Chat.Id, "No entiendo este mensaje.");
				//			break;
				//	}


				//}
    //            else
    //            {
    //                await mvarClient.SendMessage(mvarChatId, await auxTrans.processMessage(message.Text));
    //            }
    //        }            
        

        private async Task<bool> auxProcessTrainReport(global::Telegram.Bot.Types.Message message)
        {
            Debug.Assert(message != null);
            Debug.Assert(message.Text != null);
            //Proceso de un parte de averías remitido desde Telegram.
            //Train? auxTrain = await auxDetectTrainInText(message.Text);

            User? usuario = message.From;

            if (null != usuario)
            {
                await mvarClient.SendMessage(message.Chat.Id, string.Format("Eres el usuario {0} con el id {1} en el chat con id {2}", usuario.Username, usuario.Id, message.Chat.Id));                

            }


            
             


            return true;
        }

        private async Task<Train?> auxDetectTrainInText(string text)
        {
            //Busca un tren en la cadena de texto que se le pasa
            using (ApplicationDbContext auxDb = new ApplicationDbContext())
            {
                foreach (Train candidato in auxDb.Trains )
                {



                }
            }                                  
            return null;
        }

    }
}
