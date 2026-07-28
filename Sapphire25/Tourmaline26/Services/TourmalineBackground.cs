using TimeNet2026.Production;
using TimeNet2026.Timed;
using Tourmaline26.Logic;
using Tourmaline26.Services.Armandito;
using Tourmaline26.Services.TourmalineExperience;
using System.Globalization;
using BlazorBootstrap;

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
        private Task<bool>? mvarMvbTask;
        private Task<bool>? mvarArmanditoTask;
        private Task<bool>? mvarInternetTask;
        private Task<bool>? mvarLocationTask;
        private Task<bool>? mvarLedPanelsTask;
        private Task<bool>? mvarMeteoTask; //Proceso de meteorología.

        private byte mvarScreen; //Pantalla a mostrar ahora.
        private DateTime mvarNextScreenChange = DateTime.MinValue; //Próximo cambio de pantalla

        /// <summary>Última velocidad enviada al simulador en DemoMode (evita spam de API).</summary>
        private int mvarLastDemoSpeedSent = int.MinValue;
        /// <summary>Aceleración/deceleración de la rampa de demo (km/h por segundo).</summary>
        private const double DemoAccelKmhPerSec = 8.0;
        private DateTime mvarLastDemoSpeedUpdate = DateTime.MinValue;
        private Task? mvarDemoSetSpeedTask;
        /// <summary>Estado anterior de MVB ZeroSpeed (null = aún no leído).</summary>
        private bool? mvarPrevMvbZeroSpeed;

        // --- Cámara automática Tourmaline Experience ---
        /// <summary>0=&lt;10 lateral, 1=10–49 drone, 2=50–69 cenital, 3=≥70 rotación.</summary>
        private int mvarLastCameraSpeedBand = -1;
        private TourmalineCameraOrder? mvarLastAutoCameraOrder;
        /// <summary>Órbita deseada para la vista actual (puede estar pendiente de la transición).</summary>
        private bool mvarLastAutoCameraOrbit;
        private bool mvarLastAutoCameraSide;
        /// <summary>
        /// Órbita ya activa en el simulador (tras el comando Order=Orbit).
        /// </summary>
        private bool mvarOrbitActive;
        /// <summary>
        /// Tras pasar a Drone, el sim necesita ~6 s de transición antes de aceptar órbita.
        /// Si no es null, a esa hora UTC hay que enviar Order=Orbit.
        /// </summary>
        private DateTime? mvarOrbitEnableAfterUtc;
        private DateTime mvarNextHighSpeedCameraChange = DateTime.MinValue;
        private Task? mvarAutoCameraTask;
        private const int HighSpeedCameraIntervalSeconds = 15;
        /// <summary>Tiempo de transición entre vistas (cenital→drone, etc.) antes de poder orbitar.</summary>
        private const int CameraTransitionSeconds = 6;

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
            mvarLogger.LogInformation("Starting system");
            HMIUpdateRequested?.Invoke(this, EventArgs.Empty); //Actualizamos HMI
            var initTask = Task.Run(async () =>
            {
                try
                {
                    await mvarTourmaline.EnsureInitialized();
                }
                catch (Exception ex)
                {
                    // EnsureInitialized ya marca Initialized=true en finally; esto es red de seguridad.
                    mvarLogger.LogError(ex, "Fallo inesperado durante EnsureInitialized");
                    mvarTourmaline.SessionConfig.Initialized = true;
                }
                finally
                {
                    HMIUpdateRequested?.Invoke(this, EventArgs.Empty); // Actualizamos HMI cuando termina
                }
            });
            DateTime auxLastMeteoCheck = DateTime.Today; //Momento de la última comprobación de la meteorología
            DateTime auxLastPanelsUpdate = DateTime.Today; //Momento de la última actualización de paneles led.            
            DateTime auxLastArmanditoUpdate = DateTime.Today; //Última recepción de mensajes de tierra
            DateTime auxLastPassengerLanguageChange = DateTime.Today; //Última vez que cambiamos de idioma en la información al viajero.
            mvarLogger.LogInformation("System started.");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if(mvarNextScreenChange<DateTime.Now)
                    {
                        mvarScreen++;
                        if (mvarScreen > 1) mvarScreen = 0;
                        mvarNextScreenChange = DateTime.Now.AddSeconds(40);
                        mvarLogger.LogDebug($"Screen change to {mvarScreen}");
                    }

                    //Destrucción de tasks en ejecución
                    if (null != mvarGpsTask && mvarGpsTask.IsCompleted)
                    {
                        if (mvarGpsTask.IsCompletedSuccessfully)
                            mvarTourmaline.SessionConfig.GPSOK = mvarGpsTask.Result;
                        else
                            mvarTourmaline.SessionConfig.GPSOK = false;
                        mvarGpsTask = null;
                    }
                    if (null != mvarLocationTask && mvarLocationTask.IsCompleted)
                        mvarLocationTask = null;
                    if (null != mvarMvbTask && mvarMvbTask.IsCompleted)
                    {
                        mvarMvbTask = null;
                    }
                    if (null != mvarInternetTask && mvarInternetTask.IsCompleted)
                    {
                        if (mvarInternetTask.IsCompletedSuccessfully)
                            mvarTourmaline.SessionConfig.InternetOK = mvarInternetTask.Result;
                        else
                            mvarTourmaline.SessionConfig.InternetOK = false;

                        mvarInternetTask = null;
                    }
                    if (null != mvarMeteoTask && mvarMeteoTask.IsCompleted)
                        mvarMeteoTask = null;
                    if (null != mvarLedPanelsTask && mvarLedPanelsTask.IsCompleted)
                        mvarLedPanelsTask = null;
                    if (null != mvarArmanditoTask && mvarArmanditoTask.IsCompleted)
                        mvarArmanditoTask = null;

                    //Arranque de tasks
                    if (null==mvarGpsTask)
                    {
                        mvarLogger.LogDebug("Pool GPS");
                        mvarGpsTask = PoolGPS();
                    }
                    if (null == mvarLocationTask)
                    {
                        mvarLogger.LogDebug("Pool Onix");
                        mvarLocationTask = PoolLinearLocation();
                    }
                    if (null == mvarMvbTask && mvarMVBService.IsOK)
                    {                        
                        mvarLogger.LogDebug("Pool MVB");
                        mvarMvbTask = PoolMVB();
                    }
                    if (null == mvarInternetTask)
                    {
                        mvarLogger.LogDebug("Pool Internet");
                        mvarInternetTask = PoolInternet();
                    }
                    if (null == mvarMeteoTask 
                        && mvarTourmaline.SessionConfig.InternetOK 
                        && auxLastMeteoCheck < DateTime.Now)
                    {
                        mvarLogger.LogDebug("Pool Meteo");
                        mvarMeteoTask = PoolMeteo();
                        auxLastMeteoCheck = DateTime.Now.AddSeconds(60);
                    }
                    if (null == mvarArmanditoTask
                        && mvarTourmaline.SessionConfig.InternetOK
                        && auxLastArmanditoUpdate < DateTime.Now)
                    {
                        mvarLogger.LogDebug("Pool Armandito");
                        mvarArmanditoTask = PoolArmandito();
                        auxLastArmanditoUpdate = DateTime.Now.AddSeconds(15);
                    }
                    if (null == mvarLedPanelsTask && auxLastPanelsUpdate < DateTime.Now)
                    {
                        mvarLogger.LogDebug("Pool Led Teleindicators");
                        mvarLedPanelsTask = PoolLedPanels();
                        auxLastPanelsUpdate = DateTime.Now.AddSeconds(4);
                    }

                    if(auxLastPassengerLanguageChange<DateTime.Now)
                    {
                        mvarLogger.LogDebug("PassengerLanguageChange");
                        mvarTourmaline.SessionConfig.IncLanguage();
                        auxLastPassengerLanguageChange = DateTime.Now.AddSeconds(20);
                    }

                    if (null != mvarTourmaline.SessionConfig.TNEnvironment)
                    {
                        mvarLogger.LogDebug("Setting clock");
                        mvarTourmaline.SessionConfig.TNEnvironment.Now = DateTime.Now; //Actualizamos la hora actual
                        if (mvarLastDate.Day != DateTime.Today.Day)
                        {
                            mvarLogger.LogDebug("Today is changed");
                            mvarTourmaline.SessionConfig.TNEnvironment.SetWeekDate(); //Actualiza el día de la semana actual
                            mvarLastDate = DateTime.Today;
                        }                        
                    }
                    CalculateTelemetry();
                    UpdateDemoSpeed();
                    UpdateStationLeaveFromMvb();
                    UpdateExperienceCamera();
                    mvarTourmaline.UpdatePassengerInformationMode();
                    mvarTourmaline.RaiseHMIUpdate();
                    if (cycleCount > 4)
                    {                        
                        cycleCount = 0;
                        PassengerUpdateRequested?.Invoke(this, EventArgs.Empty); //Actualizamos TFT
                        mvarLogger.LogDebug("Passenger mode updated");
                        mvarTourmaline.RaisePassengerUpdate();
                    }
                    cycleCount++;
                }
                catch (Exception ex)
                {
                    mvarLogger.LogError(ex, "Critical in main loop.");
                }
                try
                {
					await Task.Delay(500, stoppingToken);
				}
                catch(TaskCanceledException)
                { }                               
            }
            mvarLogger.LogInformation("Stopped!");
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
                    mvarLogger.LogError(ex, "Critical in Meteo pooling");
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
                        mvarLogger.LogDebug("Dummy location from Tourmaline Experience");
                        return true;
                    }
                }
                catch(Exception ex)
                {
                    mvarLogger.LogError(ex, "Critical reading Dummy location from Tourmaline Experience");
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
                        mvarTourmaline.SessionConfig.CurrentGPSData = null;
                        mvarLogger.LogError(ex, "Critical in GPS pooling");                        
                    }
                }
            }
            mvarTourmaline.SessionConfig.CurrentGPSData = null;
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
                    return true;
                }
            }
            return false;
        }
        private async Task<bool> PoolMVB()
        {
            if (mvarTourmaline.SessionConfig.ServiceMode.MVBDummy)
            {
                // Conservar el MVB dummy (controles del panel / velocidad de demo).
                if (null == mvarTourmaline.SessionConfig.CurrentMVBData)
                    mvarTourmaline.SessionConfig.CurrentMVBData = new MVBData();

                if (mvarTourmaline.SessionConfig.ServiceMode.DemoMode)
                {
                    mvarTourmaline.SessionConfig.CurrentMVBData.Speed = mvarTourmaline.SessionConfig.SimulatedSpeed;
                    mvarTourmaline.SessionConfig.CurrentMVBData.SimulateLoops();
                }

                mvarTourmaline.SessionConfig.MVBLastUpdate = DateTime.Now;
                return true;
            }

            mvarTourmaline.SessionConfig.CurrentMVBData = new MVBData();

            if (mvarTourmaline.SessionConfig.ServiceMode.MVBEnabled)
            {
                try
                {
                    MVB8100Data? salida = await mvarMVBService.GetMVBDataAsync();
                    if (null != salida)
                    {
                        mvarTourmaline.SessionConfig.MVBLastUpdate = DateTime.Now;
                        mvarTourmaline.SessionConfig.MVBError = string.Empty;
                        mvarTourmaline.SessionConfig.CurrentMVBData = new MVBData(salida);
                        return true;
                    }
                    else
                        mvarLogger.LogWarning("MVB data from GetMVBDataAsync() is null");
                }
                catch (TimeoutException ex)
                {
                    mvarLogger.LogWarning(ex, "Timeout en MVB.");
                    return true;
                }
                catch (Exception ex)
                {
                    mvarLogger.LogError(ex, "Critical in MVB");
                    mvarTourmaline.SessionConfig.MVBError = ex.Message;
                }
            }
            return false;
        }
        private async Task<bool> PoolInternet()
        {
            //Comprobación si hay internet, contra una dirección conocida y fiable.
            if (mvarTourmaline.SessionConfig.InternetEnabled)
            {
                try
                {
                    using HttpClient auxClient = new HttpClient();
                    auxClient.Timeout = TimeSpan.FromSeconds(3);
                    using HttpResponseMessage response = await auxClient.GetAsync("https://www.google.com");
                    mvarTourmaline.SessionConfig.InternetOK = response.IsSuccessStatusCode;
                    return mvarTourmaline.SessionConfig.InternetOK;
                }
                catch (Exception ex)
                {
                    mvarLogger.LogWarning(ex, "No internet");
                }
            }
            mvarTourmaline.SessionConfig.InternetOK = false;
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
                TimeNetEnvironment? auxTn = mvarTourmaline.SessionConfig.TNEnvironment;
                if(null!=auxTn)
                {
                    if (null != auxTn.Circulation && null!= auxTn.CurrentStation)
                    {
                        if (null != auxTn.Circulation.Parent && null != auxTn.Circulation.Parent.asimilation && null != auxTn.Circulation.Parent.asimilation.Destination)
                            await LedPanelsStation(auxTn.CurrentStation.Name, auxTn.Circulation.Parent.asimilation.Destination.Name);
                    }
                    else
                        await LedPanelsShowInfo(auxTn.Circulation);
                }
            }
            else
            {
                await mvarLedService.Cls();    
            }
            return false;
        }
        
        private async Task LedPanelsStation(string currentStation, string currentDestination)
        {
            await mvarLedService.Print(true,$"Propera estació {currentStation}",true);
            await mvarLedService.Print(false, currentDestination, false);
        }
        private async Task LedPanelsShowInfo(Circulation? auxCirc)
        {
            bool externalPriority = false;
            if(null==mvarTourmaline.SessionConfig)
            {
                //Si no tengo sessionConfig sólo puedo anunciar la hora.
                await mvarLedService.Print(true, $"{DateTime.Now:t}", false,Alignment.Center);
                await mvarLedService.Print(false, "S F M",false,Alignment.Center);
            }
            else
            {
                if(mvarTourmaline.SessionConfig.PassengerAnnouncementEnabled &&
                    null!=mvarTourmaline.SessionConfig.PassengerAnnouncement &&
                    mvarTourmaline.SessionConfig.PassengerAnnouncement.Importance>127)
                {
                    //Anuncio a los viajeros activado
                    string auxCadenaTotal = mvarTourmaline.SessionConfig.PassengerAnnouncement.MessageText.Replace("|", "   ");
                    await mvarLedService.Print(true, auxCadenaTotal);
                    if (mvarTourmaline.SessionConfig.PassengerAnnouncement.Importance > 200)
                    {
                        externalPriority = true;
                        await mvarLedService.Print(false, mvarTourmaline.SessionConfig.PassengerAnnouncement.MessageText);
                    }                       
                }
                else
                {
                    //Dentro muestran la hora actual.
                    string cadenaTemp = "";
                    string cadenaSpeed = "";
                    if (null != mvarTourmaline.SessionConfig.CurrentWeather)
                        cadenaTemp = string.Format(CultureInfo.InvariantCulture, "   {0}ºC", mvarTourmaline.SessionConfig.CurrentWeather.Temperature2m);
                    int auxSpeed = Math.Min(mvarTourmaline.SessionConfig.CurrentSpeed, 100);
                    if (auxSpeed > 40)
                        cadenaSpeed = $"   {auxSpeed}Km/h";
                    string auxMensaje = $"{DateTime.Now:t}{cadenaTemp}{cadenaSpeed}";
                    await mvarLedService.Print(true, auxMensaje, false);
                    //Fuera muestran el número de tren.
                }
            }
            if(!externalPriority)
            {
                if (null == auxCirc)
                    await mvarLedService.Print(false, " ", false);
                else
                    await mvarLedService.Print(false, auxCirc.name, false);
            }
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
                    string auxMensaje = $"Tren amb destinació {asimila.Destination.Name}";
                    await mvarLedService.Print(true,auxMensaje,true);
                }
                else
                    await LedPanelsShowInfo(null); 
            }
            else
                await LedPanelsShowInfo(null);
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

        /// <summary>
        /// En DemoMode, acerca <see cref="SessionConfiguration.SimulatedSpeed"/> a
        /// <see cref="SessionConfiguration.CurrentNeutralSpeed"/> y reenvía la
        /// velocidad actual al simulador (Tourmaline Experience).
        /// </summary>
        private void UpdateDemoSpeed()
        {
            SessionConfiguration session = mvarTourmaline.SessionConfig;
            if (!session.ServiceMode.DemoMode)
            {
                mvarLastDemoSpeedUpdate = DateTime.MinValue;
                mvarLastDemoSpeedSent = int.MinValue;
                return;
            }

            DateTime now = DateTime.UtcNow;
            if (mvarLastDemoSpeedUpdate == DateTime.MinValue)
                mvarLastDemoSpeedUpdate = now;

            double dt = (now - mvarLastDemoSpeedUpdate).TotalSeconds;
            mvarLastDemoSpeedUpdate = now;
            // Tras una pausa larga no dar un salto enorme.
            if (dt <= 0 || dt > 2.0)
                dt = 0.5;

            int target = session.CurrentNeutralSpeed;
            double current = session.SimulatedSpeed;
            double maxDelta = DemoAccelKmhPerSec * dt;

            if (Math.Abs(target - current) <= maxDelta)
                current = target;
            else
                current += Math.Sign(target - current) * maxDelta;

            int newSpeed = (int)Math.Round(Math.Clamp(current, 0, 140));
            session.SimulatedSpeed = newSpeed;

            if (null != session.CurrentMVBData && session.ServiceMode.MVBDummy)
            {
                session.CurrentMVBData.Speed = newSpeed;
                session.CurrentMVBData.SimulateLoops();
            }

            // Enviar al simulador solo si cambió y no hay un envío en curso.
            if (newSpeed != mvarLastDemoSpeedSent
                && (mvarDemoSetSpeedTask == null || mvarDemoSetSpeedTask.IsCompleted))
            {
                mvarLastDemoSpeedSent = newSpeed;
                mvarDemoSetSpeedTask = SendDemoSpeedAsync(newSpeed);
            }
        }

        /// <summary>
        /// Tras una parada, abandona la estación actual al arrancar.
        /// <list type="bullet">
        /// <item><b>DemoMode:</b> flanco velocidad 0 → &gt; 0 (sin exigir lazo de puertas).</item>
        /// <item><b>Servicio real:</b> flanco ZeroSpeed MVB con lazo de puertas cerrado.</item>
        /// </list>
        /// Así <see cref="TimeNetEnvironment.CurrentStation"/> puede volver a null
        /// y el panel de viajero entra en NextStopsList / Cruise.
        /// </summary>
        private void UpdateStationLeaveFromMvb()
        {
            SessionConfiguration session = mvarTourmaline.SessionConfig;

            if (null == session.TNEnvironment)
            {
                mvarPrevMvbZeroSpeed = null;
                return;
            }

            // Demo: basta con haber estado parado y volver a moverse.
            if (session.ServiceMode.DemoMode)
            {
                bool zeroSpeed = session.CurrentSpeed <= 0;
                if (mvarPrevMvbZeroSpeed == true && !zeroSpeed)
                {
                    session.TNEnvironment.LeaveCurrentStation();
                    mvarLogger.LogDebug(
                        "LeaveCurrentStation (DemoMode): arranque tras velocidad cero");
                }
                mvarPrevMvbZeroSpeed = zeroSpeed;
                return;
            }

            MVBData? mvb = session.CurrentMVBData;
            if (null == mvb
                || (!session.ServiceMode.MVBEnabled && !session.ServiceMode.MVBDummy))
            {
                mvarPrevMvbZeroSpeed = null;
                return;
            }

            bool mvbZeroSpeed = mvb.ZeroSpeed;
            bool doorsLoopClosed = mvb.DoorsLoop;

            // Flanco: estaba a velocidad cero y deja de estarlo, con lazo de puertas cerrado.
            if (mvarPrevMvbZeroSpeed == true && !mvbZeroSpeed && doorsLoopClosed)
            {
                session.TNEnvironment.LeaveCurrentStation();
                mvarLogger.LogDebug(
                    "LeaveCurrentStation: lazo de puertas cerrado y pérdida de velocidad cero");
            }

            mvarPrevMvbZeroSpeed = mvbZeroSpeed;
        }

        private async Task SendDemoSpeedAsync(int speed)
        {
            try
            {
                await mvarExperience.SetSpeed(speed);
                mvarLogger.LogDebug("DemoMode: velocidad enviada al simulador = {Speed}", speed);
            }
            catch (Exception ex)
            {
                mvarLogger.LogWarning(ex, "DemoMode: error enviando velocidad {Speed} al simulador", speed);
                // Permitir reintento en el siguiente ciclo.
                mvarLastDemoSpeedSent = int.MinValue;
            }
        }

        /// <summary>
        /// Cámara TE según velocidad:
        /// &lt;10 lateral; 10–49 drone sin órbita (lado aleatorio); 50–69 cenital;
        /// ≥70 alterna elevada+órbita / frontal / frikis cada 15 s
        /// (elevada con el doble de probabilidad que cada una de las otras).
        /// Órbita: primero Drone, esperar ~6 s de transición, luego Order=Orbit.
        /// </summary>
        private void UpdateExperienceCamera()
        {
            if (mvarAutoCameraTask != null && !mvarAutoCameraTask.IsCompleted)
                return;

            // Fase 2 de elevada+órbita: la transición a Drone ya terminó → enviar Orbit.
            if (mvarOrbitEnableAfterUtc.HasValue
                && DateTime.UtcNow >= mvarOrbitEnableAfterUtc.Value
                && !mvarOrbitActive
                && mvarLastAutoCameraOrbit
                && mvarLastAutoCameraOrder == TourmalineCameraOrder.Drone)
            {
                mvarAutoCameraTask = EnableOrbitAfterTransitionAsync(mvarLastAutoCameraSide);
                return;
            }

            // Si aún esperamos la transición a Drone, no cambiar de vista.
            if (mvarOrbitEnableAfterUtc.HasValue
                && DateTime.UtcNow < mvarOrbitEnableAfterUtc.Value
                && mvarLastAutoCameraOrbit)
                return;

            int speed = mvarTourmaline.SessionConfig.CurrentSpeed;
            if (speed < 0) speed = 0;

            // 0=&lt;10  1=10–49  2=50–69  3=≥70
            int band = speed < 10 ? 0 : speed < 50 ? 1 : speed < 70 ? 2 : 3;
            bool bandChanged = band != mvarLastCameraSpeedBand;

            TourmalineCameraOrder order;
            bool wantOrbit = false;
            bool side = mvarLastAutoCameraSide;
            bool needChange;

            if (band == 0)
            {
                order = TourmalineCameraOrder.Lateral;
                wantOrbit = false;
                needChange = bandChanged
                    || mvarLastAutoCameraOrder != order
                    || mvarOrbitActive
                    || mvarOrbitEnableAfterUtc.HasValue;
            }
            else if (band == 1)
            {
                order = TourmalineCameraOrder.Drone;
                wantOrbit = false;
                if (bandChanged)
                    side = Random.Shared.Next(2) == 1;
                needChange = bandChanged
                    || mvarLastAutoCameraOrder != order
                    || mvarOrbitActive
                    || mvarOrbitEnableAfterUtc.HasValue;
            }
            else if (band == 2)
            {
                order = TourmalineCameraOrder.Cenital;
                wantOrbit = false;
                needChange = bandChanged
                    || mvarLastAutoCameraOrder != order
                    || mvarOrbitActive
                    || mvarOrbitEnableAfterUtc.HasValue;
            }
            else
            {
                DateTime now = DateTime.UtcNow;
                bool hasHighSpeedView = IsHighSpeedCamera(mvarLastAutoCameraOrder);
                // Si la órbita está pendiente, no rotar todavía.
                bool orbitPending = mvarOrbitEnableAfterUtc.HasValue && mvarLastAutoCameraOrbit;
                bool intervalElapsed = now >= mvarNextHighSpeedCameraChange;
                bool mustApply = !orbitPending
                    && (bandChanged || !hasHighSpeedView || intervalElapsed);

                if (!mustApply)
                    return;

                (order, wantOrbit, side) = PickHighSpeedCamera(mvarLastAutoCameraOrder);
                needChange = true;
            }

            if (!needChange)
                return;

            mvarLogger.LogInformation(
                "Cámara automática: speed={Speed} band={Band} (lastBand={LastBand}) → {Order} orbit={Orbit} side={Side}",
                speed, band, mvarLastCameraSpeedBand, order, wantOrbit, side);

            mvarAutoCameraTask = SendAutoCameraAsync(order, side, wantOrbit, band);
        }

        private static bool IsHighSpeedCamera(TourmalineCameraOrder? order) =>
            order is TourmalineCameraOrder.Drone
                or TourmalineCameraOrder.Brakeman
                or TourmalineCameraOrder.TrackSide;

        /// <summary>
        /// Pesos: elevada+órbita = 2, frontal = 1, frikis = 1
        /// → 50% elevada, 25% frontal, 25% frikis.
        /// </summary>
        private static (TourmalineCameraOrder order, bool orbit, bool side) PickHighSpeedCamera(
            TourmalineCameraOrder? previous)
        {
            bool side = Random.Shared.Next(2) == 1;
            TourmalineCameraOrder order = TourmalineCameraOrder.Drone;
            bool orbit = true;

            for (int attempt = 0; attempt < 5; attempt++)
            {
                int roll = Random.Shared.Next(4); // 0,1 elevada; 2 frontal; 3 frikis
                if (roll < 2)
                {
                    order = TourmalineCameraOrder.Drone;
                    orbit = true;
                }
                else if (roll == 2)
                {
                    order = TourmalineCameraOrder.Brakeman;
                    orbit = false;
                }
                else
                {
                    order = TourmalineCameraOrder.TrackSide;
                    orbit = false;
                }

                if (order != previous)
                    return (order, orbit, side);
            }

            if (previous == TourmalineCameraOrder.Drone)
                return (TourmalineCameraOrder.Brakeman, false, side);
            return (TourmalineCameraOrder.Drone, true, side);
        }

        /// <summary>
        /// Segundo paso de elevada+órbita: la vista Drone ya se estabilizó (~6 s).
        /// </summary>
        private async Task EnableOrbitAfterTransitionAsync(bool side)
        {
            try
            {
                await mvarExperience.SetCamera(TourmalineCameraOrder.Orbit, side);
                mvarOrbitActive = true;
                mvarOrbitEnableAfterUtc = null;
                mvarLastAutoCameraOrbit = true;
                mvarLogger.LogInformation(
                    "Cámara automática: órbita ON (tras {Sec}s de transición a Drone)",
                    CameraTransitionSeconds);

                if (mvarLastCameraSpeedBand == 3)
                    mvarNextHighSpeedCameraChange = DateTime.UtcNow.AddSeconds(HighSpeedCameraIntervalSeconds);
            }
            catch (Exception ex)
            {
                mvarLogger.LogWarning(ex, "Cámara automática: error activando órbita tras transición");
                mvarOrbitEnableAfterUtc = DateTime.UtcNow.AddSeconds(2); // reintento breve
            }
        }

        /// <summary>
        /// Aplica la vista. Si se desea órbita y no estamos ya en Drone:
        /// 1) Drone ya, 2) Orbit solo tras <see cref="CameraTransitionSeconds"/> s.
        /// </summary>
        private async Task SendAutoCameraAsync(
            TourmalineCameraOrder order,
            bool side,
            bool wantOrbit,
            int band)
        {
            try
            {
                if (wantOrbit)
                    order = TourmalineCameraOrder.Drone;
                else if (order == TourmalineCameraOrder.Orbit)
                    order = TourmalineCameraOrder.Drone;

                if (wantOrbit)
                {
                    bool alreadyOnDrone = mvarLastAutoCameraOrder == TourmalineCameraOrder.Drone;

                    if (!alreadyOnDrone)
                    {
                        // 1) Primero elevada. La órbita va después de la transición.
                        await mvarExperience.SetCamera(TourmalineCameraOrder.Drone, side);
                        mvarLogger.LogInformation(
                            "Cámara automática: vista Drone side={Side}; órbita en {Sec}s",
                            side, CameraTransitionSeconds);

                        mvarLastCameraSpeedBand = band;
                        mvarLastAutoCameraOrder = TourmalineCameraOrder.Drone;
                        mvarLastAutoCameraSide = side;
                        mvarLastAutoCameraOrbit = true; // deseada, aún no activa
                        mvarOrbitActive = false;
                        mvarOrbitEnableAfterUtc = DateTime.UtcNow.AddSeconds(CameraTransitionSeconds);

                        // No rotar hasta completar órbita + intervalo de alta velocidad.
                        if (band == 3)
                        {
                            mvarNextHighSpeedCameraChange = DateTime.UtcNow
                                .AddSeconds(CameraTransitionSeconds + HighSpeedCameraIntervalSeconds);
                        }
                        return;
                    }

                    // Ya en Drone: activar órbita si procede (tras espera o si ya estaba lista).
                    if (!mvarOrbitActive)
                    {
                        if (mvarOrbitEnableAfterUtc.HasValue
                            && DateTime.UtcNow < mvarOrbitEnableAfterUtc.Value)
                        {
                            // Todavía en transición; no spamear.
                            mvarLastCameraSpeedBand = band;
                            mvarLastAutoCameraOrbit = true;
                            return;
                        }

                        await mvarExperience.SetCamera(TourmalineCameraOrder.Orbit, side);
                        mvarOrbitActive = true;
                        mvarOrbitEnableAfterUtc = null;
                        mvarLogger.LogInformation("Cámara automática: órbita ON (ya en Drone)");
                    }

                    mvarLastCameraSpeedBand = band;
                    mvarLastAutoCameraOrder = TourmalineCameraOrder.Drone;
                    mvarLastAutoCameraOrbit = true;
                    mvarLastAutoCameraSide = side;
                    if (band == 3)
                        mvarNextHighSpeedCameraChange = DateTime.UtcNow.AddSeconds(HighSpeedCameraIntervalSeconds);
                    return;
                }

                // --- Sin órbita: cancelar pendiente y aplicar vista ---
                mvarOrbitEnableAfterUtc = null;

                await mvarExperience.SetCamera(order, side);
                mvarLogger.LogInformation(
                    "Cámara automática: vista {Order} side={Side}", order, side);

                if (mvarOrbitActive)
                {
                    await mvarExperience.SetCamera(TourmalineCameraOrder.Orbit, side);
                    mvarOrbitActive = false;
                    mvarLogger.LogInformation("Cámara automática: órbita OFF");
                }

                mvarLastCameraSpeedBand = band;
                mvarLastAutoCameraOrder = order;
                mvarLastAutoCameraOrbit = false;
                mvarLastAutoCameraSide = side;
                if (band == 3)
                    mvarNextHighSpeedCameraChange = DateTime.UtcNow.AddSeconds(HighSpeedCameraIntervalSeconds);
            }
            catch (Exception ex)
            {
                mvarLogger.LogWarning(ex, "Cámara automática: error aplicando {Order} orbit={Orbit}", order, wantOrbit);
                mvarLastCameraSpeedBand = -1;
                mvarLastAutoCameraOrder = null;
                mvarOrbitEnableAfterUtc = null;
                if (band == 3)
                    mvarNextHighSpeedCameraChange = DateTime.MinValue;
            }
        }
    }

}