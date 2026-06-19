using Microsoft.Extensions.FileSystemGlobbing.Internal.PathSegments;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using TimeNet2026.Production;
using TimeNet2026.Timed;
using Tourmaline26.Logic;
using Tourmaline26.Services.Armandito;
using Tourmaline26.Services.TourmalineExperience;

namespace Tourmaline26.Services
{
    public class TourmalineBackground : BackgroundService
    {
        private readonly ILogger<TourmalineBackground> mvarLogger;
        private readonly TourmalineService mvarTourmaline;
        private readonly ArmanditoService mvarArmandito;
        private readonly TourmalineExperienceService mvarExperience;
        private readonly GPSService mvarGPSService;
        private readonly MVBService mvarMVBService;
        private readonly LEDDisplayService mvarLedService;
        private readonly MeteoService mvarMeteoService;
        /// <summary>
        /// Flag para indicar al sistema que han terminado de cargar los datos.
        /// </summary>        
        public event EventHandler? PassengerUpdateRequested; //Ha ocurrido algo que requiere actualizar los TFT
        public event EventHandler? HMIUpdateRequested;
        private DateTime mvarLastDate = DateTime.MinValue; //Uso este valor para cambiar de fecha automáticamente.

        private Task<bool>? mvarGpsTask;
        private Task<MVB8100Data?>? mvarMvbTask;
        private Task<bool>? mvarArmanditoTask;
        private Task<bool>? mvarInternetTask;
        private Task<bool>? mvarLocationTask;
        private Task<bool>? mvarLedPanelsTask;
        private Task<bool>? mvarMeteoTask; //Proceso de meteorología.

        private byte mvarScreen; //Pantalla a mostrar ahora.
        private DateTime mvarNextScreenChange = DateTime.MinValue; //Próximo cambio de pantalla

        public TourmalineBackground(
            ILogger<TourmalineBackground> logger,
            TourmalineService tourmalineService,
            ArmanditoService armanditoService,
            TourmalineExperienceService experienceService,
            MVBService mvbService,
            GPSService gpsService,
            LEDDisplayService displayService,
            MeteoService meteoService)
        {
            mvarLogger = logger;
            mvarTourmaline = tourmalineService;
            mvarArmandito = armanditoService;
            mvarExperience = experienceService;
            mvarMVBService = mvbService;
            mvarGPSService = gpsService;
            mvarMeteoService = meteoService;
            mvarLedService = displayService;
        }
        /// <summary>
        /// Actualización express de los paneles HMI.
        /// </summary>
        public void RaiseEvents()
        {
            HMIUpdateRequested?.Invoke(this, EventArgs.Empty); //Actualizamos HMI
        }
        /// <summary>
        /// Bucle principal del servicio. Se ejecuta indefinidamente hasta el fin de ejecución
        /// </summary>
        /// <param name="stoppingToken"></param>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int cycleCount = 0;
            HMIUpdateRequested?.Invoke(this, EventArgs.Empty); //Actualizamos HMI
            var initTask = Task.Run(async () =>
            {
                mvarTourmaline.SessionConfig.Initialized = await mvarTourmaline.EnsureInitialized();
                HMIUpdateRequested?.Invoke(this, EventArgs.Empty); // Actualizamos HMI cuando termina
            });
            DateTime auxLastMeteoCheck = DateTime.Today; //Momento de la última comprobación de la meteorología
            DateTime auxLastPanelsUpdate = DateTime.Today; //Momento de la última actualización de paneles led.            
            DateTime auxLastArmanditoUpdate = DateTime.Today; //Última recepción de mensajes de tierra
            mvarLogger.LogInformation("TourmalineBackground iniciado.");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if(mvarNextScreenChange<DateTime.Now)
                    {
                        mvarScreen++;
                        if (mvarScreen > 1) mvarScreen = 0;
                        mvarNextScreenChange = DateTime.Now.AddSeconds(40);
                    }

                    if(null!=mvarGpsTask && mvarGpsTask.IsCompleted)
                    {
                        if (mvarGpsTask.IsCompletedSuccessfully)
                            mvarTourmaline.SessionConfig.GPSOK = mvarGpsTask.Result;
                        else
                            mvarTourmaline.SessionConfig.GPSOK = false;
                        mvarGpsTask = null;
                    }
                    if(null==mvarGpsTask)
                        mvarGpsTask = PoolGPS();

                    if (null!=mvarMvbTask && mvarMvbTask.IsCompleted)
                    {
                        if(!mvarTourmaline.SessionConfig.ServiceMode.MVBDummy)
                        {
							if (mvarMvbTask.IsCompletedSuccessfully && null!=mvarMvbTask.Result)
								mvarTourmaline.SessionConfig.CurrentMVBData = new MVBData(mvarMvbTask.Result);
							else
								mvarTourmaline.SessionConfig.CurrentMVBData = null;
						}                        
                        mvarMvbTask = null;
                    }
                    if(null == mvarMvbTask)
                        mvarMvbTask = PoolMVB();

                    if (null != mvarInternetTask && mvarInternetTask.IsCompleted)
                    {
                        if(mvarInternetTask.IsCompletedSuccessfully)
                            mvarTourmaline.SessionConfig.InternetOK = mvarInternetTask.Result;
                        else
                            mvarTourmaline.SessionConfig.InternetOK = false;

                        mvarInternetTask = null;
                    }
                    if (null == mvarInternetTask) 
                        mvarInternetTask = PoolInternet();

                    if (null != mvarLocationTask && mvarLocationTask.IsCompleted)
                        mvarLocationTask = null;

                    if(null==mvarLocationTask)
                        mvarLocationTask = PoolLinearLocation();

                    if (null != mvarLedPanelsTask && mvarLedPanelsTask.IsCompleted)
                        mvarLedPanelsTask = null;

                    if (null == mvarLedPanelsTask && auxLastPanelsUpdate <DateTime.Now)
                    {
                        mvarLedPanelsTask = PoolLedPanels();
                        auxLastPanelsUpdate = DateTime.Now.AddSeconds(4);
                    }                        
                    
                    if (null != mvarMeteoTask && mvarMeteoTask.IsCompleted)
                        mvarMeteoTask = null;
                    if (null == mvarMeteoTask && auxLastMeteoCheck < DateTime.Now)
                    {
                        mvarMeteoTask = PoolMeteo();
                        auxLastMeteoCheck = DateTime.Now.AddSeconds(30);
                    }

                    if (null != mvarArmanditoTask && mvarArmanditoTask.IsCompleted)
                        mvarArmanditoTask = null;
                    if(null==mvarArmanditoTask && auxLastArmanditoUpdate < DateTime.Now)
                    {
                        mvarArmanditoTask = PoolArmandito();
                        auxLastArmanditoUpdate = DateTime.Now.AddSeconds(15);
                    }
                    
                }
                catch (Exception ex)
                {
                    mvarLogger.LogError(ex, "Error en el ciclo de TourmalineBackground.");
                }
                try
                {
					await Task.Delay(500, stoppingToken);
				}
                catch(TaskCanceledException)
                { }                
                if(null!=mvarTourmaline.SessionConfig.TNEnvironment)
                {
					mvarTourmaline.SessionConfig.TNEnvironment.Now = DateTime.Now; //Actualizamos la hora actual
                    if(mvarLastDate.Day!=DateTime.Today.Day)
						mvarTourmaline.SessionConfig.TNEnvironment.SetWeekDate(); //Actualiza el día de la semana actual
                    mvarLastDate = DateTime.Today;
				}
                CalculateTelemetry();
                mvarTourmaline.RaiseHMIUpdate();
                if (cycleCount > 4)
                {
                    cycleCount = 0;
                    PassengerUpdateRequested?.Invoke(this, EventArgs.Empty); //Actualizamos TFT
                    mvarTourmaline.RaisePassengerUpdate();
                }
                cycleCount++;
            }
            mvarLogger.LogInformation("TourmalineBackground detenido.");
        }

        private async Task<bool> PoolMeteo()
        {
            if (mvarTourmaline.SessionConfig.InternetEnabled)
            {
                try
                {
                    return await mvarMeteoService.ReadLoop();
                }
                catch (Exception ex)
                {
                    mvarLogger.LogError(ex, "Error al obtener datos de meteorología. {0}", ex.Message);
                }
            }
            return false;
        }
        private async Task<bool> PoolGPS()
        {
            if(mvarTourmaline.SessionConfig.ServiceMode.GPSDummy)
            {
                //En modo Dummy obtenemos la posición usando la API Rest de Tourmaline Experience
                //El simulador nos ofrece una posición.
                try
                {
                    TourmalineTelemetryResponse? auxTelemetry = await mvarExperience.GetTelemetry();
                    if (null != auxTelemetry)
                    {
                        mvarTourmaline.SessionConfig.GPSLastUpdate = DateTime.Now;
                        GPSData nuevo = new GPSData();
                        nuevo.Altitude = 0;
                        nuevo.Latitude = auxTelemetry.Latitude;
                        nuevo.Longitude = auxTelemetry.Longitude;
                        nuevo.FixQuality = 1;
                        nuevo.SatellitesUsed = 8;
                        nuevo.SpeedKmh = auxTelemetry.Speed;
                        nuevo.Time = DateTime.Now;
                        mvarTourmaline.SessionConfig.CurrentGPSData = nuevo;
                        return true;
                    }
                }
                catch(Exception ex)
                {
                    mvarLogger.LogError(ex, "Error al obtener datos dummy de localización desde Tourmaline Experience. {0}", ex.Message);
                }
            }
            else
            {
                if (mvarTourmaline.SessionConfig.GPSEnabled)
                {
                    try
                    {
                        if (mvarGPSService.ReadLoop())
                        {
                            mvarTourmaline.SessionConfig.GPSLastUpdate = DateTime.Now;
                            mvarTourmaline.SessionConfig.CurrentGPSData = mvarGPSService.CurrentData;
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        mvarLogger.LogError(ex, "Error al obtener datos de localización GPS. {0}", ex.Message);
                    }
                }
                else
                {
                    //Si no tengo localización por satélite, devuelvo null.
                    mvarTourmaline.SessionConfig.CurrentGPSData = null;
                }
            }
            return false;
        }
        //Activamos la localización del tren (si es posible)
        private async Task<bool> PoolLinearLocation()
        {
            if (null == mvarTourmaline.SessionConfig.CurrentGPSData)
                return false;
            if(null == mvarTourmaline.SessionConfig.TNEnvironment)
                return false;
            TimeNetEnvironment auxEnvironment = mvarTourmaline.SessionConfig.TNEnvironment;
            if (null == auxEnvironment.TopoStorage)
                return false;
            if(null==auxEnvironment.Circulation)
            {
                //Localización por ejes cercanos.
                //Buscamos el eje en el que estamos y obtenemos cualquiera de las asimilaciones.
                if (mvarTourmaline.SessionConfig.LinearLocation.TryLocateBySatellite(mvarTourmaline.SessionConfig.CurrentGPSData.GeoLocation, auxEnvironment.TopoStorage))
                {
                    auxEnvironment.PK = mvarTourmaline.SessionConfig.LinearLocation.PKRef;
                    if(null==auxEnvironment.Asimilation)
                    {
                        auxEnvironment.SetAsimilationByAxis();
                    }
                    return true;
                }
            }
            else
            {
                //TODO: Modificar esto para poner el eje en el que estamos.
                if( mvarTourmaline.SessionConfig.LinearLocation.TryLocateBySatellite(mvarTourmaline.SessionConfig.CurrentGPSData.GeoLocation, auxEnvironment.TopoStorage))
                {
                    auxEnvironment.PK = mvarTourmaline.SessionConfig.LinearLocation.PKRef;
                    auxEnvironment.Axis = mvarTourmaline.SessionConfig.LinearLocation.Axis;
                }
            }
            return false;
        }
        private async Task<MVB8100Data?> PoolMVB()
        {
            if(!mvarMVBService.IsOK)
            {
                mvarTourmaline.SessionConfig.ServiceMode.MVBEnabled = false;
                mvarTourmaline.SessionConfig.ServiceMode.MVBDummy = true;
            }

            if (mvarTourmaline.SessionConfig.ServiceMode.MVBEnabled)
            {
                try
                {                    
                    MVB8100Data? salida = await mvarMVBService.GetMVBDataAsync();
                    if (null != salida)
                    {
                        mvarTourmaline.SessionConfig.MVBLastUpdate = DateTime.Now;
                        mvarTourmaline.SessionConfig.MVBError = string.Empty;
                        return salida;
                    }
                }
                catch (Exception ex)
                {
                    mvarLogger.LogError(ex, "Error al obtener datos MVB. {0}", ex.Message);
                    mvarTourmaline.SessionConfig.MVBError = ex.Message;
                }
            }
            else if (mvarTourmaline.SessionConfig.ServiceMode.MVBDummy)
            {
                if(null==mvarTourmaline.SessionConfig.CurrentMVBData)
                {
                    mvarTourmaline.SessionConfig.CurrentMVBData = new MVBData();
                    mvarTourmaline.SessionConfig.MVBLastUpdate = DateTime.Now;
                }
            }
            return null;
        }
        private async Task<bool> PoolInternet()
        {
            if (mvarTourmaline.SessionConfig.InternetEnabled)
            {
                try
                {
                    using HttpClient auxClient = new HttpClient();
                    auxClient.Timeout = TimeSpan.FromSeconds(3);
                    using HttpResponseMessage response = await auxClient.GetAsync("https://www.google.com");
                    return response.IsSuccessStatusCode;
                }
                catch (Exception ex)
                {
                    mvarLogger.LogWarning(ex, "Error comprobando conexión a Internet");
                }
            }
            return false;
        }

        private async Task<bool> PoolArmandito()
        {
            if(mvarTourmaline.SessionConfig.MainSwitches.ArmanditoEnabled && 
                null!= mvarTourmaline.SessionConfig.TNEnvironment &&
                null!= mvarTourmaline.SessionConfig.TNEnvironment.Circulation)
            {
                try
                {
                    string auxServiceId = mvarTourmaline.SessionConfig.TNEnvironment.Circulation.name;
                    mvarTourmaline.SessionConfig.EarthMessages = await mvarArmandito.GetMessagesAsync(auxServiceId);
                }
                catch (Exception ex)
                {
                    mvarLogger.LogWarning(ex, "Earth messages error");
                }
            }
            return false;
        }

        /// <summary>
        /// Actualiza los teleindicadores LED
        /// </summary>
        /// <returns></returns>
        private async Task<bool> PoolLedPanels()
        {           
            if(mvarTourmaline.SessionConfig.MainSwitches.TeleindicatorsEnabled && mvarTourmaline.SessionConfig.MainSwitches.PASEnabled)
            {
                switch (mvarScreen)
                {
                    case 1: await LedPanelsShowDestination(); break;
                    default: await LedPanelsShowTime(); break;
                }


                //Prioridad de los avisos:
                //* 1 Mensaje emergente de Armandito
                //* 2 Próxima parada

                //* 3 Destino

                //* 4 Hora y temperatura
                await LedPanelsShowTime();
            }
            else
            {
                await mvarLedService.Cls();    
            }
            return false;
        }
        
        private async Task LedPanelsShowTime()
        {
            string cadenaTemp = "";
            string cadenaSpeed = "";
            if (null != mvarTourmaline.SessionConfig.CurrentWeather)
                cadenaTemp = $"   {mvarTourmaline.SessionConfig.CurrentWeather.Temperature2m}.C";
            int auxSpeed = Math.Min(mvarTourmaline.SessionConfig.CurrentSpeed, 100);
            if (auxSpeed > 40)
            {
                cadenaSpeed = $"   {auxSpeed}Km/h";
            }
            string auxMensaje = $"{DateTime.Now:t}{cadenaTemp}{cadenaSpeed}";
            await mvarLedService.Print(auxMensaje, false);
        }
        private async Task LedPanelsShowDestination()
        {
            TimeNetEnvironment? enviro = mvarTourmaline.SessionConfig.TNEnvironment;
            if (null != enviro &&
                null != enviro.Asimilation && 
                mvarTourmaline.SessionConfig.InformationLevel == Enums.InformationLevel.Route)
            {
                Asimilation asimila = enviro.Asimilation;
                if (null != asimila && null!=asimila.Destination)
                {
                    string auxMensaje = $"Aquest tren es dirigeix a {asimila.Destination.Name}";
                    await mvarLedService.Print(auxMensaje,true);
                }
                else
                    await LedPanelsShowTime();                
            }
            else
                await LedPanelsShowTime();
        }
        
        /// <summary>
        /// Hace las actualizaciones de los controles y los cálculos de la malla
        /// </summary>
        private void CalculateTelemetry()
        {
            SessionConfiguration auxSesion = mvarTourmaline.SessionConfig;
			if (auxSesion.CurrentLimitSpeed < 0)
				auxSesion.CurrentLimitSpeed = 0;
			if (auxSesion.CurrentLimitSpeed > 140)
				auxSesion.CurrentLimitSpeed = 140;
			if (auxSesion.CurrentNeutralSpeed < 0)
				auxSesion.CurrentNeutralSpeed = 0;
			if (auxSesion.CurrentNeutralSpeed > 140)
				auxSesion.CurrentNeutralSpeed = 140;
		}
    }

}