using Microsoft.Extensions.FileSystemGlobbing.Internal.PathSegments;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using TimeNet2026.Production;
using Tourmaline26.Logic;
using Tourmaline26.Services.TourmalineExperience;

namespace Tourmaline26.Services
{
    public class TourmalineBackground : BackgroundService
    {
        private readonly ILogger<TourmalineBackground> mvarLogger;
        private readonly TourmalineService mvarTourmaline;
        private readonly TourmalineExperienceService mvarExperience;
        private readonly GPSService mvarGPSService;
        private readonly MVBService mvarMVBService;
        private readonly LEDDisplayService mvarLedService;
        /// <summary>
        /// Flag para indicar al sistema que han terminado de cargar los datos.
        /// </summary>        
        public event EventHandler? PassengerUpdateRequested; //Ha ocurrido algo que requiere actualizar los TFT
        public event EventHandler? HMIUpdateRequested;
        private DateTime mvarLastDate = DateTime.MinValue; //Uso este valor para cambiar de fecha automáticamente.

        private Task<bool>? mvarGpsTask;
        private Task<MVB8100Data?>? mvarMvbTask;
        private Task<bool>? mvarInternetTask;
        private Task<bool>? mvarLocationTask;
        private Task<bool>? mvarLedPanelsTask;

        public TourmalineBackground(
            ILogger<TourmalineBackground> logger,
            TourmalineService tourmalineService,
            TourmalineExperienceService experienceService,
            MVBService mvbService,
            GPSService gpsService,
            LEDDisplayService ledService)
        {
            mvarLogger = logger;
            mvarTourmaline = tourmalineService;
            mvarExperience = experienceService;
            mvarMVBService = mvbService;
            mvarGPSService = gpsService;
            mvarLedService = ledService;
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
            mvarLogger.LogInformation("TourmalineBackground iniciado.");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
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
                        if(!mvarTourmaline.SessionConfig.MVBDummy)
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

                    if (null == mvarLedPanelsTask)
                        mvarLedPanelsTask = PoolLedPanels();

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
        private async Task<bool> PoolGPS()
        {
            if(mvarTourmaline.SessionConfig.GPSDummy)
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
                //Axis? auxAxis = auxEnvironment.TopoStorage.ColAxis.ne
                return mvarTourmaline.SessionConfig.LinearLocation.TryLocateBySatellite(mvarTourmaline.SessionConfig.CurrentGPSData.GeoLocation, auxEnvironment.TopoStorage);
            }
            else
            {
                //TODO: Modificar esto para poner el eje en el que estamos.
                return mvarTourmaline.SessionConfig.LinearLocation.TryLocateBySatellite(mvarTourmaline.SessionConfig.CurrentGPSData.GeoLocation, auxEnvironment.TopoStorage);
            }
        }
        private async Task<MVB8100Data?> PoolMVB()
        {
            if(!mvarMVBService.IsOK)
            {
                mvarTourmaline.SessionConfig.MVBEnabled = false;
                mvarTourmaline.SessionConfig.MVBDummy = true;
            }

            if (mvarTourmaline.SessionConfig.MVBEnabled)
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
            else if (mvarTourmaline.SessionConfig.MVBDummy)
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

        /// <summary>
        /// Actualiza los teleindicadores LED
        /// </summary>
        /// <returns></returns>
        private async Task<bool> PoolLedPanels()
        {
            if(mvarTourmaline.SessionConfig.TeleindicatorsEnabled && mvarTourmaline.SessionConfig.PASEnabled)
            {
                
                //Prioridad de los avisos:
                //* 1 Mensaje emergente de Armandito
                //* 2 Próxima parada
                //* 3 Destino
                //* 4 Hora y temperatura

            }
            else
            {
                await mvarLedService.ClearAsync();
            }
            return false;
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