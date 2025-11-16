using System;
using System.Xml.Serialization;

namespace Laba3
{
    public enum CarType
    {
        Passenger,
        Truck
    }

    public interface ICarBrand
    {
        string BrandName { get; set; }
        string ModelName { get; set; }
        int Horsepower { get; set; }
        int MaxSpeed { get; set; }
        CarType Type { get; set; }
    }

    [Serializable]
    [XmlInclude(typeof(PassengerCarBrand))]
    [XmlInclude(typeof(TruckCarBrand))]
    public abstract class CarBrandBase : ICarBrand
    {
        public string BrandName { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public int Horsepower { get; set; }
        public int MaxSpeed { get; set; }
        public CarType Type { get; set; }

        protected CarBrandBase()
        {
        }

        protected CarBrandBase(CarType type)
        {
            Type = type;
        }

        public static CarBrandBase CloneWithType(ICarBrand source, CarType type)
        {
            CarBrandBase clone = type == CarType.Passenger
                ? new PassengerCarBrand()
                : new TruckCarBrand();

            clone.BrandName = source.BrandName;
            clone.ModelName = source.ModelName;
            clone.Horsepower = source.Horsepower;
            clone.MaxSpeed = source.MaxSpeed;
            clone.Type = type;

            return clone;
        }
    }

    public class PassengerCarBrand : CarBrandBase
    {
        public PassengerCarBrand() : base(CarType.Passenger)
        {
        }
    }

    public class TruckCarBrand : CarBrandBase
    {
        public TruckCarBrand() : base(CarType.Truck)
        {
        }
    }
}

