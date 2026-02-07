
using Microsoft.EntityFrameworkCore;
using Sapphire2026.Data;
using Sapphire2026.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2026Telegram.Operative
{
    /// <summary>
    /// El módulo de telegram necesitará obtener toda la información posible de un usuario que está
    /// conectado al bot, así como sus permisos.
    /// </summary>
    internal class UserContext
    {
        internal User? mvarUser { get; private set; }
        internal IConfiguration mvarConfig;

        internal UserContext(long telegramChatId, IConfiguration config)
        {
            mvarConfig = config;
            if(telegramChatId >=0)
            {
                using (DataStorage almacen = new DataStorage(mvarConfig))
                {
                    Sapphire2026.Data.Models.User? auxUser = await almacen.Users
                        .Where(x => x.TelegramId == telegramChatId).FirstOrDefaultAsync();


				}              
            }
        }




    }
}
