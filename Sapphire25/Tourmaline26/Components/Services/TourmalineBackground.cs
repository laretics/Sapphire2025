using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Tourmaline26.Components.Services.Logic;

namespace Tourmaline26.Components.Services
{
    public class TourmalineBackground : BackgroundService
    {
        private readonly ILogger<TourmalineBackground> mvarLogger;
        private readonly TourmalineService mvarTourmaline;
        private readonly GPSService mvarGPSService;
        private readonly MVBService mvarMVBService;
        /// <summary>
        /// Flag para indicar al sistema que han terminado de cargar los datos.
        /// </summary>
        public bool SystemInitialized { get; private set; } = false;
        public event EventHandler? PassengerUpdateRequested; //Ha ocurrido algo que requiere actualizar los TFT
        public event EventHandler? HMIUpdateRequested;
        private DateTime mvarLastDate = DateTime.MinValue; //Uso este valor para cambiar de fecha automáticamente.

        private Task<bool>? mvarGpsTask;
        private Task<MVB8100Data?>? mvarMvbTask;
        private Task<bool>? mvarInternetTask;

        public TourmalineBackground(
            ILogger<TourmalineBackground> logger,
            TourmalineService tourmalineService,
            MVBService mvbService,
            GPSService gpsService)
        {
            mvarLogger = logger;
            mvarTourmaline = tourmalineService;
            mvarMVBService = mvbService;
            mvarGPSService = gpsService;
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
            SystemInitialized = await mvarTourmaline.EnsureInitialized();
            HMIUpdateRequested?.Invoke(this, EventArgs.Empty); //Actualizamos HMI
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
                        if (mvarMvbTask.IsCompletedSuccessfully)
                            mvarTourmaline.SessionConfig.CurrentMVBData = mvarMvbTask.Result;
                        else
                            mvarTourmaline.SessionConfig.CurrentMVBData = null;
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
                }
                catch (Exception ex)
                {
                    mvarLogger.LogError(ex, "Error en el ciclo de TourmalineBackground.");
                }
                await Task.Delay(500, stoppingToken);
                if(null!=mvarTourmaline.SessionConfig.TNEnvironment)
                {
					mvarTourmaline.SessionConfig.TNEnvironment.Now = DateTime.Now; //Actualizamos la hora actual
                    if(mvarLastDate.Day!=DateTime.Today.Day)
						mvarTourmaline.SessionConfig.TNEnvironment.SetWeekDate(); //Actualiza el día de la semana actual
                    mvarLastDate = DateTime.Today;
				}
                    
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
            if (mvarTourmaline.SessionConfig.GPSEnabled)
            {
                try
                {
					if(mvarGPSService.ReadLoop())
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
            return false;
        }
        private async Task<MVB8100Data?> PoolMVB()
        {
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
                catch
                {
                }
            }
            return false;
        }
    }
}