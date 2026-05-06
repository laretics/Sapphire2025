namespace Tourmaline26.Logic
{
	/// <summary>
	/// Este objeto contiene la información MVB convenientemente condensada desde el lector MVB.
	/// </summary>
	public class MVBData
	{
		public enum InverterPosition:byte
		{
			 Reverse = 0,
			 Iddle = 1,
			 Tunnel = 2,
			 Shunting = 3,
			 Forward = 4,
			 Unknown = 255			 
		}
		public enum HandlerPosition:byte
		{
			EmergencyBrake = 0,
			Braking = 1,
			Iddle = 2,
			Traction =3,
			Unknown = 255
		}
		public enum Habilitation:byte
		{
			None=0, //Cambio de cabina
			M1=1,
			M2=2,
			Other=3, //Composición acoplada
			Unknown=255
		}
		public InverterPosition Inverter{ get; set; }
		public HandlerPosition Handler{ get; set; }
		public Habilitation Cabin{ get; set; }
		public Habilitation Advance{ get; set; }
		public int TractionPower{ get; set; }
		public int BrakePower{ get; set; }
		public bool LeftDoors{ get; set; }
		public bool RightDoors{ get; set; }

		public bool DoorsLoop{ get; set; }
		public bool TractionLoop{ get; set; }
		public bool SpeedValidation{ get; set; }
		public bool DriveCommand{ get; set; }
		public bool ZeroSpeed{ get; set; }
		public int Odometer{ get; set; }
		public int Speed{ get; set; }

		public DateTime Time{ get; set; }

		public MVBWagon[] Wagons{ get; private set; }

		public MVBData()
		{
			Wagons = new MVBWagon[4] { new MVBWagon(), new MVBWagon(), new MVBWagon(), new MVBWagon() };
			Handler = HandlerPosition.Iddle;
			Inverter = InverterPosition.Iddle;
			SimulateLoops();
		}
		public MVBData(MVB8100Data source)
		{
			Wagons = new MVBWagon[4] { new MVBWagon(), new MVBWagon(), new MVBWagon(), new MVBWagon() };
			//Inversor
			if (source.inverter_disconnected)
				Inverter = InverterPosition.Iddle;
			else if (source.inverter_reverse)
				Inverter = InverterPosition.Reverse;
			else if (source.inverter_tunnel)
				Inverter = InverterPosition.Tunnel;
			else if (source.inverter_manoeuvre)
				Inverter = InverterPosition.Shunting;
			else if (source.inverter_forward)
				Inverter = InverterPosition.Forward;
			else
				Inverter = InverterPosition.Unknown;

			//Manipulador
			if (source.handler_traction)
				Handler = HandlerPosition.Traction;
			else if (source.handler_neutral)
				Handler = HandlerPosition.Iddle;
			else if (source.handler_brake)
				Handler = HandlerPosition.Braking;
			else if (source.handler_emergency_brake)
				Handler = HandlerPosition.EmergencyBrake;
			else
				Handler = HandlerPosition.Unknown;

			//Cabina habilitada
			if (source.cab_enabled_m1)
				Cabin = Habilitation.M1;
			else if (source.cab_enabled_m2)
				Cabin = Habilitation.M2;
			else
				Cabin = Habilitation.None;

			//Sentido de la marcha
			if (source.traction_dir_m1)
				Advance = Habilitation.M1;
			else if (source.traction_dir_m2)
				Advance = Habilitation.M2;
			else
				Advance = Habilitation.None;

			//Fuerza de tracción
			TractionPower = source.traction;
			//Fuerza de frenado
			BrakePower = source.brake;

			//Velocidad
			Speed = (int)source.current_speed;
			SpeedValidation = source.speed_validator;
			ZeroSpeed = source.speed_zero;
			Odometer = source.odometer;

			//Fecha
			Time = source.system_time;

			//Puertas
			LeftDoors = source.doors_enabled_left;
			RightDoors = source.doors_enabled_right;

			//Lazos
			DoorsLoop = source.doors_loop;
			TractionLoop = source.traction_loop;
			DriveCommand = source.drive_command;

			//Composición
			MVBWagon coche = Wagons[0]; //M1
			coche.PassengerLight = source.passenger_light_m1;
			coche.OutsideTemp = source.outside_temp_m1;
			coche.InsideTemp = source.inside_temp_m1;
			coche.BrakeLoop = source.brake_loop_m1;
			coche = Wagons[1]; //N1
			coche.PassengerLight = source.passenger_light_n1;
			coche.OutsideTemp = source.outside_temp_n1;
			coche.InsideTemp = source.inside_temp_n1;
			coche.BrakeLoop = source.brake_loop_n1;
			coche = Wagons[2]; //N2
			coche.PassengerLight = source.passenger_light_n2;
			coche.OutsideTemp = source.outside_temp_n2;
			coche.InsideTemp = source.inside_temp_n2;
			coche.BrakeLoop = source.brake_loop_n2;
			coche = Wagons[3]; //M2
			coche.PassengerLight = source.passenger_light_m2;
			coche.OutsideTemp = source.outside_temp_m2;
			coche.InsideTemp = source.inside_temp_m2;
			coche.BrakeLoop = source.brake_loop_m2;


		}

		/// <summary>
		/// Circunstancias en las que se mostrará el icono de advertencia.
		/// </summary>
		public bool WarningSign
		{
			get
			{
				//Hay múltiples razones por las que se podría encender el warning
				if (OpenLoop) return true;


				return false;
			}
		}
		public bool BrakeLoop
		{ 
			get => 
			Wagons[0].BrakeLoop && Wagons[1].BrakeLoop && Wagons[2].BrakeLoop && Wagons[3].BrakeLoop &&
			TractionLoop && (Cabin == Habilitation.M1 || Cabin == Habilitation.M2);
		}
		public bool OpenLoop { get => !TractionLoop || !DoorsLoop || !BrakeLoop; }

		public void SimulateLoops()
		{
			DoorsLoop = (!LeftDoors && !RightDoors);
			TractionLoop = DoorsLoop && 
				(Inverter != InverterPosition.Iddle)&&
				(Handler==HandlerPosition.Iddle || Handler==HandlerPosition.Traction);
		}

	}
	public class MVBWagon
	{
		public bool PassengerLight{ get; set; } //Luz de viajeros.
		public int OutsideTemp{ get; set; } //Temperatura de entrada del aire.
		public int InsideTemp { get; set; } //Temperatura de salida del aire a los viajeros.
		public bool BrakeLoop{ get; set; } //Lazo de freno en el coche
	}
}
