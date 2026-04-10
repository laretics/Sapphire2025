namespace Tourmaline26.Components.Services.Logic
{
    public class MVB8100Data
    {
        public int brake { get; set; }
        public bool brake_loop_m1 { get; set; }
        public bool brake_loop_m2 { get; set; }
        public bool brake_loop_n1 { get; set; }
        public bool brake_loop_n2 { get; set; }
        public bool cab_enabled_m1 { get; set; }
        public bool cab_enabled_m2 { get; set; }
        public double current_speed { get; set; }
        public bool doors_enabled_left { get; set; }
        public bool doors_enabled_right { get; set; }
        public bool doors_loop { get; set; }
        public bool drive_command { get; set; }
        public bool handler_brake { get; set; }
        public bool handler_emergency_brake { get; set; }
        public bool handler_neutral { get; set; }
        public bool handler_traction { get; set; }
        public int inside_temp_m1 { get; set; }
        public int inside_temp_m2 { get; set; }
        public int inside_temp_n1 { get; set; }
        public int inside_temp_n2 { get; set; }
        public bool inverter_disconnected { get; set; }
        public bool inverter_forward { get; set; }
        public bool inverter_manoeuvre { get; set; }
        public bool inverter_reverse { get; set; }
        public bool inverter_tunnel { get; set; }
        public int odometer { get; set; }
        public int outside_temp_m1 { get; set; }
        public int outside_temp_m2 { get; set; }
        public int outside_temp_n1 { get; set; }
        public int outside_temp_n2 { get; set; }
        public bool passenger_light_m1 { get; set; }
        public bool passenger_light_m2 { get; set; }
        public bool passenger_light_n1 { get; set; }
        public bool passenger_light_n2 { get; set; }
        public bool speed_validator { get; set; }
        public bool speed_zero { get; set; }
        public DateTime system_time { get; set; }
        public int traction { get; set; }
        public bool traction_dir_m1 { get; set; }
        public bool traction_dir_m2 { get; set; }
        public bool traction_loop { get; set; }
    }
}
