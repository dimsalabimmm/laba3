using System;

namespace Laba3
{
    public abstract class Car
    {
        public string RegistrationNumber { get; set; }
    }

    public class PassengerCarInstance : Car
    {
        public string MultimediaName { get; set; }
        public int AirbagCount { get; set; }
    }

    public class TruckInstance : Car
    {
        public int WheelCount { get; set; }
        public double BodyVolume { get; set; }
    }
}

