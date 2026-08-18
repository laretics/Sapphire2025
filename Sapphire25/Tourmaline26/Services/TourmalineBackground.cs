using Diamond.Cabin;
using Diamond.Project;
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
        private static readonly TimeSpan GpsStaleTimeout = TimeSpan.FromSeconds(5);
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
        /// <summary>Última velocidad enviada al TE en modo normal (seguimiento PK).</summary>
        private int mvarLastExperienceSpeedSent = int.MinValue;
        private Task? mvarExperienceSyncTask;
        /// <summary>
        /// Ganancia de corrección: km/h por metro de desfase a lo largo de la marcha.
        /// p.ej. 0.20 → 100 m de retraso ≈ +20 km/h sobre la velocidad MVB
        /// (curva de acercamiento más dura que el 0.06 anterior).
        /// </summary>
        private const double ExperienceSyncGainKmhPerMeter = 0.20;
        /// <summary>
        /// Desfase (m) a partir del cual se fuerza recuperación o parada del tren simulado:
        /// retraso ≥ umbral → el sim no se detiene aunque el real esté parado;
        /// adelanto ≥ umbral → el sim se detiene aunque el real circule.
        /// </summary>
        private const int ExperienceSyncHardThresholdMeters = 100;
        /// <summary>Tope de velocidad al simulador (evita valores absurdos en TE).</summary>
        private const int ExperienceMaxSpeedKmh = 200;
        /// <summary>Estado anterior de MVB ZeroSpeed (null = aún no leído).</summary>
        private bool? mvarPrevMvbZeroSpeed;
        /// <summary>Dead-reckoning por odómetro MVB mientras no hay GPS.</summary>
        private readonly OdometerDeadReckoning mvarOdoReckoning = new OdometerDeadReckoning();
        private bool? mvarPrevDoorsOpen;
        private string? mvarOdoCirculationId;
        private DateTime mvarLastDummyOdoUpdate = DateTime.MinValue;

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

            mvarTourmaline.SessionConfig.ServiceMode.MVBEnabledChanged += enabled =>
            {
                if (enabled)
                    mvarMVBService.ResetRetries();
            };
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
                    CheckMVB();

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

                    if (null != mvarTourmaline.SessionConfig.Cabin)
                    {
                        mvarLogger.LogDebug("Setting clock");
                        CabinEnvironment cabin = mvarTourmaline.SessionConfig.Cabin;
                        cabin.ClockNow = DateTime.Now;
                        if (mvarLastDate.Day != DateTime.Today.Day)
                        {
                            mvarLogger.LogDebug("Today is changed");
                            cabin.RefreshDayProject();
                            mvarLastDate = DateTime.Today;
                        }
                    }
                    CalculateTelemetry();
                    UpdateDemoSpeed();
                    UpdateExperienceSpeedSync();
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
                            mvarTourmaline.SessionConfig.GPSOK = true;
                            return true;
                        }

                        if(null!=mvarTourmaline.SessionConfig.CurrentGPSData &&
                            DateTime.Now - mvarTourmaline.SessionConfig.GPSLastUpdate < GpsStaleTimeout)
                        {
                            mvarTourmaline.SessionConfig.GPSOK = true;
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
            mvarTourmaline.SessionConfig.GPSOK = false;
            mvarTourmaline.SessionConfig.CurrentGPSData = null;
            return false;
        }
        //Activamos la localización del tren (si es posible)
        private async Task<bool> PoolLinearLocation()
        {
            await Task.CompletedTask;
            if (null == mvarTourmaline.SessionConfig.Cabin)
                return false;
            CabinEnvironment cabin = mvarTourmaline.SessionConfig.Cabin;

            if (HasValidGpsLocation(mvarTourmaline.SessionConfig))
            {
                if (null == cabin.Topo)
                    return false;
                if (mvarOdoReckoning.Armed)
                {
                    mvarOdoReckoning.Disarm();
                    mvarPrevDoorsOpen = null;
                    mvarLogger.LogInformation("Localización: GPS recuperado; se deja de usar el odómetro MVB");
                }

                GPSData gps = mvarTourmaline.SessionConfig.CurrentGPSData!;
                // MissionAxes se actualizan al asignar Circulation en CabinEnvironment.
                if (cabin.LinearLocation.TryLocateBySatellite(
                    cabin.Topo,
                    gps.Latitude,
                    gps.Longitude))
                {
                    cabin.ApplyLinearLocation();
                    return true;
                }
                return false;
            }

            return UpdatePositionFromOdometer(cabin);
        }

        /// <summary>
        /// Sin GPS: avanza el PK con el odómetro MVB. Al abrir puertas, alinea el PK
        /// con la siguiente estación comercial de la ruta y reinicia el origen.
        /// </summary>
        private bool UpdatePositionFromOdometer(CabinEnvironment cabin)
        {
            SessionConfiguration session = mvarTourmaline.SessionConfig;
            MVBData? mvb = session.CurrentMVBData;
            if (null == mvb
                || (!session.ServiceMode.MVBEnabled && !session.ServiceMode.MVBDummy))
            {
                return false;
            }

            Circulation? circulation = cabin.Circulation;
            string? circulationId = circulation?.Id;
            if (!string.Equals(mvarOdoCirculationId, circulationId, StringComparison.Ordinal))
            {
                mvarOdoReckoning.Disarm();
                mvarPrevDoorsOpen = null;
                mvarOdoCirculationId = circulationId;
            }

            long? startPk = ResolveOdometerStartPk(cabin);
            if (startPk is null)
                return false;

            if (!mvarOdoReckoning.Armed)
            {
                mvarOdoReckoning.Arm(mvb.Odometer, startPk.Value);
                mvarLogger.LogInformation(
                    "Localización: sin GPS, odómetro MVB={Odo} desde PK={Pk}",
                    mvb.Odometer,
                    startPk.Value);
            }

            bool pkIncreasing = cabin.Asimilation is null
                || cabin.Asimilation.Sense != Diamond.Motion.CirculationSense.DecreasingPk;
            long projectedPk = mvarOdoReckoning.Project(mvb.Odometer, pkIncreasing);
            cabin.ApplyOdometerPk(projectedPk);

            bool doorsOpen = mvb.LeftDoors || mvb.RightDoors;
            bool openedNow = mvarPrevDoorsOpen == false && doorsOpen;
            mvarPrevDoorsOpen = doorsOpen;

            if (openedNow && circulation is not null)
            {
                TimedCall? snap = ResolveDoorSnapCall(cabin, circulation, mvarOdoReckoning.OriginPk);
                if (snap is not null)
                {
                    cabin.ApplyOdometerPk(snap.Pk);
                    mvarOdoReckoning.Resync(mvb.Odometer, snap.Pk);
                    mvarLogger.LogInformation(
                        "Localización: puertas abiertas → PK estación {Station} ({Pk}), odómetro={Odo}",
                        snap.Station.Name,
                        snap.Pk,
                        mvb.Odometer);
                }
            }

            return true;
        }

        private static long? ResolveOdometerStartPk(CabinEnvironment cabin)
        {
            if (cabin.LinearLocation.PKRef >= 0)
                return cabin.LinearLocation.PKRef;

            Circulation? circulation = cabin.Circulation;
            if (circulation is not null && circulation.Calls.Count > 0)
                return circulation.Calls[0].Pk;

            return null;
        }

        /// <summary>
        /// Estación a la que alinear al abrir puertas: la actual si no es el origen
        /// del tramo, o la siguiente comercial por delante de ese origen.
        /// </summary>
        private static TimedCall? ResolveDoorSnapCall(
            CabinEnvironment cabin,
            Circulation circulation,
            long segmentOriginPk)
        {
            if (cabin.CurrentStation is not null)
            {
                TimedCall? atStop = FindCallByStationId(circulation, cabin.CurrentStation.Id);
                if (atStop is not null && atStop.Pk != segmentOriginPk)
                    return atStop;
            }

            if (Math.Abs(cabin.PK - segmentOriginPk) >= 50)
            {
                IReadOnlyList<TimedCall> ahead = CabinItinerary.RemainingCommercialCalls(
                    circulation,
                    segmentOriginPk,
                    includeCurrentStation: false);
                if (ahead.Count > 0)
                    return ahead[0];
            }

            return null;
        }

        private static TimedCall? FindCallByStationId(Circulation circulation, string stationId)
        {
            int i = 0;
            while (i < circulation.Calls.Count)
            {
                TimedCall call = circulation.Calls[i];
                if (string.Equals(call.Station.Id, stationId, StringComparison.Ordinal))
                    return call;
                i++;
            }
            return null;
        }
        private void CheckMVB()
        {
            if (mvarTourmaline.SessionConfig.ServiceMode.MVBDummy)
                RefreshDummyMVB();
            else
            {
                if (mvarTourmaline.SessionConfig.ServiceMode.MVBEnabled)
                    RefreshRealMVB();
            }
        }
        private void RefreshDummyMVB()
        {
            // Conservar el MVB dummy (controles del panel / velocidad de demo).
            if (null == mvarTourmaline.SessionConfig.CurrentMVBData)
                mvarTourmaline.SessionConfig.CurrentMVBData = new MVBData();

            if (mvarTourmaline.SessionConfig.ServiceMode.DemoMode)
            {
                mvarTourmaline.SessionConfig.CurrentMVBData.Speed = mvarTourmaline.SessionConfig.SimulatedSpeed;
                mvarTourmaline.SessionConfig.CurrentMVBData.SimulateLoops();
            }

            AdvanceDummyOdometer(mvarTourmaline.SessionConfig.CurrentMVBData);
            mvarTourmaline.SessionConfig.MVBLastUpdate = DateTime.Now;
        }
        private void AdvanceDummyOdometer(MVBData mvb)
        {
            DateTime now = DateTime.UtcNow;
            if (mvarLastDummyOdoUpdate != DateTime.MinValue)
            {
                double dt = (now - mvarLastDummyOdoUpdate).TotalSeconds;
                if (dt > 0 && dt < 2.0)
                {
                    double meters = mvb.Speed * (1000.0 / 3600.0) * dt;
                    mvb.Odometer += (int)Math.Round(meters);
                }
            }
            mvarLastDummyOdoUpdate = now;
        }

        private void RefreshRealMVB()
        {
            MVB8100Data? data = mvarMVBService.CurrentData;
            if (null != data)
            {
                mvarTourmaline.SessionConfig.MVBLastUpdate = DateTime.Now;
                mvarTourmaline.SessionConfig.MVBError = string.Empty;
                mvarTourmaline.SessionConfig.CurrentMVBData = new MVBData(data);
            }
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
                null!= mvarTourmaline.SessionConfig.Cabin &&
                null!= mvarTourmaline.SessionConfig.Cabin.Circulation)
            {
                try
                {
                    Circulation cir = mvarTourmaline.SessionConfig.Cabin.Circulation;
                    string auxServiceId = cir.HasServiceNumber ? cir.ServiceNumber : cir.Id;
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
            if (!mvarTourmaline.SessionConfig.MainSwitches.TeleindicatorsEnabled)
            {
                await mvarLedService.Cls();
                return false;
            }

            if (mvarTourmaline.SessionConfig.InformationLevel == Enums.InformationLevel.Forbidden)
            {
                await LedPanelsShowOutOfService();
                return false;
            }

            if (!mvarTourmaline.SessionConfig.MainSwitches.PASEnabled)
            {
                await mvarLedService.Cls();
                return false;
            }

            CabinEnvironment? auxTn = mvarTourmaline.SessionConfig.Cabin;
            if(null!=auxTn)
            {
                if (null != auxTn.Circulation && null != auxTn.Asimilation)
                {
                    StationInfo? current = auxTn.CurrentStation;
                    StationInfo origin = auxTn.Asimilation.Origin;
                    bool atOrigin = current is not null
                        && string.Equals(current.Id, origin.Id, StringComparison.Ordinal);
                    bool beginOfTrip = mvarTourmaline.SessionConfig.InformationMode
                        == Enums.PassengerInformationMode.BeginOfTrip;

                    // En origen acabamos de elegir destino: no anunciar esa estación como próxima.
                    if (atOrigin || beginOfTrip)
                        await LedPanelsShowDestination();
                    else if (current is not null)
                        await LedPanelsStation(current.Name, auxTn.Asimilation.Destination.Name);
                    else
                        await LedPanelsShowInfo(auxTn.Circulation);
                }
                else
                    await LedPanelsShowInfo(auxTn.Circulation);
            }
            return false;
        }

        private async Task LedPanelsShowOutOfService()
        {
            string message = OutOfServiceDisplay.Combined;
            await mvarLedService.Print(true, message, true);
            await mvarLedService.Print(false, message, true);
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
                    int auxSpeed = Math.Clamp(mvarTourmaline.SessionConfig.CurrentSpeed, 0, 100);
                    if (auxSpeed > 40)
                        cadenaSpeed = $"   {auxSpeed}km/h";
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
                {
                    string label = auxCirc.HasServiceNumber ? auxCirc.ServiceNumber : auxCirc.Id;
                    await mvarLedService.Print(false, label, false);
                }
            }
        }
        private async Task LedPanelsShowDestination()
        {
            CabinEnvironment? enviro = mvarTourmaline.SessionConfig.Cabin;
            if (null != enviro &&
                null != enviro.Asimilation && 
                mvarTourmaline.SessionConfig.InformationLevel == Enums.InformationLevel.Route)
            {
                Asimilation asimila = enviro.Asimilation;
                bool updateExternal = mvarTourmaline.SessionConfig.MainSwitches.ExternalTeleindicatorsEnabled;
                await mvarLedService.PrintDestination(asimila.Destination.Name, updateExternal);
            }
            else
                await LedPanelsShowInfo(enviro?.Circulation);
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
                mvarDemoSetSpeedTask = SendExperienceSpeedAsync(newSpeed, "DemoMode");
            }
        }

        /// <summary>
        /// Modo normal (no Demo): el tren de Tourmaline Experience sigue al tren real.
        /// <list type="bullet">
        /// <item>Sin GPS válido (antena, túnel…): PK por odómetro MVB; el sim usa MVB si aún no hay PK.</item>
        /// <item>PK real: GPS → <see cref="LinearLocation.TryLocateBySatellite"/> (ya en el bucle).</item>
        /// <item>PK simulado: lat/lon de telemetría TE → mismo algoritmo.</item>
        /// <item>Velocidad base: MVB / emulación MVB (<see cref="SessionConfiguration.CurrentSpeed"/>).</item>
        /// <item>Corrección: desfase a lo largo de la marcha (positivo = sim retrasado → más velocidad).</item>
        /// <item>Retraso ≥ <see cref="ExperienceSyncHardThresholdMeters"/> m: el sim sigue aunque el real esté parado.</item>
        /// <item>Adelanto ≥ umbral: el sim se detiene aunque el real circule.</item>
        /// <item>Tope: <see cref="ExperienceMaxSpeedKmh"/> km/h.</item>
        /// </list>
        /// </summary>
        private void UpdateExperienceSpeedSync()
        {
            SessionConfiguration session = mvarTourmaline.SessionConfig;

            // Demo tiene su propia rampa; no mezclar.
            if (session.ServiceMode.DemoMode)
            {
                mvarLastExperienceSpeedSent = int.MinValue;
                return;
            }

            // Sin GPS ni PK de odómetro no hay referencia de ruta: el simulado copia la velocidad MVB.
            if (!HasValidGpsLocation(session)
                && session.LinearLocation.Source != LinearLocationSource.Odometer)
            {
                ApplyMvbOnlyExperienceSpeed(session, "MVB-noGPS");
                return;
            }

            // Sin misión / topología no hay eje de referencia para corrección PK.
            CabinEnvironment? env = session.Cabin;
            if (null == env?.Topo || null == env.Circulation)
            {
                ApplyMvbOnlyExperienceSpeed(session, "MVB-noTopo");
                return;
            }

            // GPS hay, pero PK real aún no resuelto → también MVB puro.
            if (env.PK < 0 || session.LinearLocation.PKRef < 0)
            {
                ApplyMvbOnlyExperienceSpeed(session, "MVB-noPK");
                return;
            }

            // Evitar solapar peticiones a TE.
            if (mvarExperienceSyncTask != null && !mvarExperienceSyncTask.IsCompleted)
                return;

            mvarExperienceSyncTask = ExperienceSpeedSyncAsync();
        }

        /// <summary>
        /// True si hay lectura GPS usable para localización lineal (no solo bandera de módulo).
        /// </summary>
        private static bool HasValidGpsLocation(SessionConfiguration session)
        {
            if (!session.GPSOK)
                return false;
            GPSData? gps = session.CurrentGPSData;
            return gps != null && gps.IsValid;
        }

        /// <summary>
        /// Envía al simulador la velocidad MVB tal cual, sin corrección por desfase PK.
        /// </summary>
        private void ApplyMvbOnlyExperienceSpeed(SessionConfiguration session, string context)
        {
            // No pisar un ciclo de sync PK en curso.
            if (mvarExperienceSyncTask != null && !mvarExperienceSyncTask.IsCompleted)
                return;

            int commanded = Math.Clamp(Math.Max(0, session.CurrentSpeed), 0, ExperienceMaxSpeedKmh);
            session.ExperienceCommandedSpeed = commanded;
            session.ExperiencePkLagMeters = 0;

            if (commanded != mvarLastExperienceSpeedSent)
            {
                mvarLastExperienceSpeedSent = commanded;
                mvarExperienceSyncTask = SendExperienceSpeedAsync(commanded, context);
            }
        }

        private async Task ExperienceSpeedSyncAsync()
        {
            SessionConfiguration session = mvarTourmaline.SessionConfig;
            CabinEnvironment? env = session.Cabin;
            if (null == env?.Topo)
                return;

            try
            {
                TourmalineTelemetryResponse? telemetry = await mvarExperience.GetTelemetry();
                if (null == telemetry || !telemetry.success)
                {
                    // Algunas builds de TE no rellenan success; aceptar coords no nulas.
                    if (null == telemetry
                        || (Math.Abs(telemetry.Latitude) < 1e-8 && Math.Abs(telemetry.Longitude) < 1e-8))
                        return;
                }

                Asimilation? asim = env.Asimilation;
                LinearLocation simLoc = session.SimulatedLinearLocation;
                simLoc.MissionAxes = env.LinearLocation.MissionAxes;

                if (!simLoc.TryLocateBySatellite(
                    env.Topo,
                    telemetry!.Latitude,
                    telemetry.Longitude))
                {
                    mvarLogger.LogDebug(
                        "TE sync: no se pudo localizar el tren simulado en la topología ({Lat},{Lon})",
                        telemetry.Latitude,
                        telemetry.Longitude);
                    return;
                }

                long realPk = env.PK;
                long simPk = simLoc.PKRef;
                if (realPk < 0 || simPk < 0)
                    return;

                // Sentido de la marcha a lo largo del PK: asimilación si hay, si no el del GPS real.
                bool ascending = asim is not null
                    ? asim.Sense == Diamond.Motion.CirculationSense.IncreasingPk
                    : env.PKIncreasing;

                // Desfase en el sentido de la circulación: + = simulado por detrás del real.
                long lagMeters = ascending
                    ? (realPk - simPk)
                    : (simPk - realPk);

                session.ExperiencePkLagMeters = lagMeters;

                // Velocidad del tren real (MVB), no GPS.
                int realSpeed = Math.Max(0, session.CurrentSpeed);
                int commanded;

                if (lagMeters <= -ExperienceSyncHardThresholdMeters)
                {
                    // Simulado muy por delante: parar aunque el real circule.
                    // Así se cede el espacio perdido en lugar de seguir abriendo el desfase.
                    commanded = 0;
                }
                else
                {
                    double correction = lagMeters * ExperienceSyncGainKmhPerMeter;
                    commanded = (int)Math.Round(realSpeed + correction);

                    // Simulado muy por detrás: no detenerse aunque el real esté parado.
                    // La parada del real es una oportunidad para recuperar metros.
                    if (lagMeters >= ExperienceSyncHardThresholdMeters && realSpeed <= 0)
                    {
                        commanded = Math.Max(commanded, (int)Math.Round(correction));
                        if (commanded < 1)
                            commanded = 1;
                    }

                    commanded = Math.Clamp(commanded, 0, ExperienceMaxSpeedKmh);
                }

                session.ExperienceCommandedSpeed = commanded;

                mvarLogger.LogDebug(
                    "TE sync: realPk={RealPk} simPk={SimPk} lag={Lag}m thr={Thr}m asc={Asc} realV={RealV} → cmd={Cmd}",
                    realPk, simPk, lagMeters, ExperienceSyncHardThresholdMeters, ascending, realSpeed, commanded);

                if (commanded != mvarLastExperienceSpeedSent)
                {
                    mvarLastExperienceSpeedSent = commanded;
                    await SendExperienceSpeedAsync(commanded, "Sync");
                }
            }
            catch (Exception ex)
            {
                mvarLogger.LogWarning(ex, "TE sync: error al sincronizar velocidad del simulador");
                mvarLastExperienceSpeedSent = int.MinValue;
            }
        }

        private async Task SendExperienceSpeedAsync(int speed, string context)
        {
            try
            {
                await mvarExperience.SetSpeed(speed);
                mvarLogger.LogDebug("TE {Context}: velocidad enviada = {Speed}", context, speed);
            }
            catch (Exception ex)
            {
                mvarLogger.LogWarning(ex, "TE {Context}: error enviando velocidad {Speed}", context, speed);
                // Permitir reintento en el siguiente ciclo.
                mvarLastDemoSpeedSent = int.MinValue;
                mvarLastExperienceSpeedSent = int.MinValue;
            }
        }

        /// <summary>
        /// Tras una parada, abandona la estación actual al arrancar.
        /// <list type="bullet">
        /// <item><b>DemoMode:</b> flanco velocidad 0 → &gt; 0 (sin exigir lazo de puertas).</item>
        /// <item><b>Servicio real:</b> flanco ZeroSpeed MVB con lazo de puertas cerrado.</item>
        /// </list>
        /// Así <see cref="CabinEnvironment.CurrentStation"/> puede volver a null
        /// y el panel de viajero entra en NextStopsList / Cruise.
        /// </summary>
        private void UpdateStationLeaveFromMvb()
        {
            SessionConfiguration session = mvarTourmaline.SessionConfig;

            if (null == session.Cabin)
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
                    session.Cabin.LeaveCurrentStation();
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
                session.Cabin.LeaveCurrentStation();
                mvarLogger.LogDebug(
                    "LeaveCurrentStation: lazo de puertas cerrado y pérdida de velocidad cero");
            }

            mvarPrevMvbZeroSpeed = mvbZeroSpeed;
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
            int band = speed < 10 ? 0 : speed < 45 ? 1 : speed < 65 ? 2 : 3;
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