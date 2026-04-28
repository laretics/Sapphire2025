using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeNet2026
{
    [Flags]
    public enum Weekday : byte
    {
        None = 0,
        Monday = 1,
        Tuesday = 2,
        Wednesday = 4,
        Thursday = 8,
        Friday = 16,
        Saturday = 32,
        Sunday = 64,
        Festive = 128,
        Labour = Monday | Tuesday | Wednesday | Thursday | Friday,
        Weekend = Saturday | Sunday,
        AllFestives = Saturday | Sunday | Festive,
        All = Monday | Tuesday | Wednesday | Thursday | Friday | Saturday | Sunday | Festive
    }

    //Indica la procedencia de la ubicación lineal
    public enum LinearLocationSource:byte
    {
        None=0, //No hay ubicación
        Manual = 1, //Datos introducidos a mano
        Odometer = 2, //Actualizados por odómetro
        Satellite = 3, //Obtenida de GPS o Galileo
        Other = 255 //Cualquier otro medio
    }
}
