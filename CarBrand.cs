using System;
using System.Xml.Serialization;

namespace Laba3
{
    public enum CarType
    {
        Passenger,
        Truck
    }

    public interface ICar
    {
        string RegistrationNumber { get; set; }
    }

    [Serializable]
    [XmlInclude(typeof(PassengerCar))]
    [XmlInclude(typeof(Truck))]
    public class CarBrand
    {
        public CarBrand()
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                Id = Guid.NewGuid().ToString();
            }
        }

        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string BrandName { get; set; } = string.Empty;

        public string ModelName { get; set; } = string.Empty;

        public int HorsePower { get; set; }

        public int MaxSpeed { get; set; }

        public CarType Type { get; set; } = CarType.Passenger;
    }

    [Serializable]
    public class PassengerCar : ICar
    {
        public string RegistrationNumber { get; set; } = string.Empty;
        public string MultimediaName { get; set; } = string.Empty;
        public int AirbagCount { get; set; }
    }

    [Serializable]
    public class Truck : ICar
    {
        public string RegistrationNumber { get; set; } = string.Empty;
        public int WheelCount { get; set; }
        public double BodyVolume { get; set; }
    }
}

