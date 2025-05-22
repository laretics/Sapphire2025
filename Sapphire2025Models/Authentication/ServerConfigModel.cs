using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sapphire2025Models.Authentication
{
	internal class ServerConfigModel:BasicRequestModel
	{
		public bool TelegramEnabled { get; set; } //Telegram activado
		public bool TelegramMulticastEnabled { get; set; } //Habilitación de los mensajes a varios destinatarios
		public bool TelegramPrivateEnabled { get; set; } //Mensajes en conversaciones privadas habilitado.
		public bool TelegramPairingEnabled { get; set; } //Habilitación del emparejamiento de nuevos usuarios con una sesión de telegram.
	}
}
