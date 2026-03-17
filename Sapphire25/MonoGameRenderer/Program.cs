using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FreeTrainSimulator.Models.Settings;
using Orts.ActivityRunner.Viewer3D;

namespace Orts.ActivityRunner
{
    internal static class Program
    {
        private static readonly char[] optionSeparators = new[] { '=', ':' };

        public static Viewer Viewer;        

        /// <summary>
        /// Punto de entrada principal para la aplicación
        /// </summary>
        private static async Task Main(string[] args)
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);

            List<string> argumentList = args.ToList();
            string profileName = ParseCommandLineOption(argumentList, "Profile");

            //ProfileModel profile = await (string.IsNullOrEmpty(profileName) ?
            //    ((ProfileModel)null).Current(CancellationToken.None).ConfigureAwait(false) :
            //    (((ProfileModel)null).Get(profileName, CancellationToken.None)).ConfigureAwait(false));

            //ProfileUserSettingsModel userSettings = await profile.LoadSettingsModel<ProfileUserSettingsModel>(CancellationToken.None).ConfigureAwait(false);
            //userSettings.MultiPlayer = !string.IsNullOrEmpty(ParseCommandLineOption(argumentList, "MultiplayerClient"));

            //enables loading of dll for specific architecture(32 or 64bit) from distinct folders, useful when both versions require same name (as for soft_oal.dll)
            //string path = Path.Combine(RuntimeInfo.ApplicationFolder, "Native", (Environment.Is64BitProcess) ? "x64" : "x86");
            //NativeMethods.SetDllDirectory(path);

//            using (GameHost game = new GameHost(userSettings))
//            {
//#pragma warning disable CA2000 // Dispose objects before losing scope
//                game.PushState(new GameStateRunActivity(argumentList.ToArray()));
//#pragma warning restore CA2000 // Dispose objects before losing scope
//                game.Run();
//            }
        }

        /// <summary>
        /// Lee la línea de comandos para cargar las opciones.
        /// </summary>
        /// <param name="arguments"></param>
        /// <param name="argumentName"></param>
        /// <returns></returns>
        private static string ParseCommandLineOption(List<string> arguments, string argumentName)
        {

            string argumentValue = arguments.Where(a => a.StartsWith($"-{argumentName}", StringComparison.OrdinalIgnoreCase) ||
            a.StartsWith($"/{argumentName}", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

            if (!string.IsNullOrEmpty(argumentValue))
            {
                arguments.RemoveAll(a => a.StartsWith($"-{argumentName}", StringComparison.OrdinalIgnoreCase) ||
                a.StartsWith($"/{argumentName}", StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(argumentValue))
            {
                string[] kvp = argumentValue.Split(optionSeparators, 2);

                string v = kvp.Length > 1 ? kvp[1] : "yes";
                return v;
            }
            return argumentValue;
        }
    }
}