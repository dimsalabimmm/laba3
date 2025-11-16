using System;
using System.Collections.Generic;
using System.Threading;

namespace Laba3
{
    public static class Loader
    {
        private static readonly Dictionary<ICarBrand, List<Car>> CachedData = new Dictionary<ICarBrand, List<Car>>();
        private static readonly Random Random = new Random();
        private static int _progress;

        public static void Load(ICarBrand brand)
        {
            if (brand == null)
            {
                _progress = 0;
                return;
            }

            if (CachedData.ContainsKey(brand))
            {
                _progress = 100;
                return;
            }

            List<Car> cars = new List<Car>();
            int carCount = Random.Next(10, 21); // 10 to 20 cars

            for (int i = 0; i < carCount; i++)
            {
                Car car;
                if (brand.Type == CarType.Passenger)
                {
                    car = new PassengerCarInstance
                    {
                        RegistrationNumber = GenerateRegistrationNumber(),
                        MultimediaName = GenerateMultimediaName(),
                        AirbagCount = Random.Next(2, 9) // 2 to 8 airbags
                    };
                }
                else
                {
                    car = new TruckInstance
                    {
                        RegistrationNumber = GenerateRegistrationNumber(),
                        WheelCount = Random.Next(4, 19), // 4 to 18 wheels
                        BodyVolume = Math.Round(Random.NextDouble() * 50 + 10, 2) // 10 to 60 m³
                    };
                }

                cars.Add(car);

                // Random delay 0 to 0.5 seconds
                Thread.Sleep(Random.Next(0, 501));

                _progress = (int)((i + 1) * 100.0 / carCount);
            }

            CachedData[brand] = cars;
            _progress = 100;
        }

        public static int GetProgress()
        {
            return _progress;
        }

        public static List<Car> GetCars(ICarBrand brand)
        {
            if (brand != null && CachedData.ContainsKey(brand))
            {
                return CachedData[brand];
            }
            return new List<Car>();
        }

        private static string GenerateRegistrationNumber()
        {
            const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            return $"{letters[Random.Next(letters.Length)]}{letters[Random.Next(letters.Length)]}" +
                   $"{Random.Next(1000, 10000)}" +
                   $"{letters[Random.Next(letters.Length)]}{letters[Random.Next(letters.Length)]}";
        }

        private static string GenerateMultimediaName()
        {
            string[] systems = { "Android Auto", "Apple CarPlay", "Tesla Infotainment", "BMW iDrive", "Mercedes MBUX", "Audi MMI" };
            return systems[Random.Next(systems.Length)];
        }
    }
}

