using System;
using System.Xml.Serialization;

namespace Laba3
{
    public enum CarType
    {
        Passenger,
        Truck
    }

    [XmlInclude(typeof(PassengerCar))]
    [XmlInclude(typeof(Truck))]
    [XmlRoot("CarBrand")]
    public abstract class CarBrand : IEquatable<CarBrand>
    {
        public string BrandName { get; set; }
        public string ModelName { get; set; }
        public int Horsepower { get; set; }
        public int MaxSpeed { get; set; }
        public abstract CarType Type { get; }

        public bool Equals(CarBrand other)
        {
            if (other == null) return false;
            return BrandName == other.BrandName &&
                   ModelName == other.ModelName &&
                   Horsepower == other.Horsepower &&
                   MaxSpeed == other.MaxSpeed &&
                   Type == other.Type;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CarBrand);
        }

        public override int GetHashCode()
        {
            return (BrandName?.GetHashCode() ?? 0) ^
                   (ModelName?.GetHashCode() ?? 0) ^
                   Horsepower ^
                   MaxSpeed ^
                   Type.GetHashCode();
        }
    }

    [XmlRoot("PassengerCar")]
    public class PassengerCar : CarBrand
    {
        public override CarType Type => CarType.Passenger;
    }

    [XmlRoot("Truck")]
    public class Truck : CarBrand
    {
        public override CarType Type => CarType.Truck;
    }
}

