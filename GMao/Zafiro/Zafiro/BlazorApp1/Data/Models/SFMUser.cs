using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZafiroGmao.Data.Models
{
    public class SFMUser : IdentityUser
    {
        public SFMUser():base()
        {
            CF = string.Empty;
            TelegramId = 0;
        }

        [PersonalData]
        public string CF { get; set; } //Carnet ferroviario

        [PersonalData]
        public long TelegramId { get; set; } //Código de chat de Telegram.


        [NotMapped]
        public bool hasTelegram { get => 0 != TelegramId; }
    }
}
