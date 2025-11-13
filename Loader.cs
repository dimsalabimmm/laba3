using System;
using System.Collections.Generic;
using System.Threading;

namespace Laba3
{
    public static class Loader
    {
        private static Dictionary<CarBrand, List<Car>> _cachedData = new Dictionary<CarBrand, List<Car>>();
        private static int _progress = 0;
        private static bool _isLoading = false;
        private static Random _random = new Random();

        public static void Load(CarBrand brand)
        {
            if (_cachedData.ContainsKey(brand))
            {
                _progress = 100;
                return;
            }

            _isLoading = true;
            _progress = 0;

            List<Car> cars = new List<Car>();
            int carCount = _random.Next(10, 21); // 10 to 20 cars

            for (int i = 0; i < carCount; i++)
            {
                Car car;
                if (brand.Type == CarType.Passenger)
                {
                    car = new PassengerCarInstance
                    {
                        RegistrationNumber = GenerateRegistrationNumber(),
                        MultimediaName = GenerateMultimediaName(),
                        AirbagCount = _random.Next(2, 9) // 2 to 8 airbags
                    };
                }
                else
                {
                    car = new TruckInstance
                    {
                        RegistrationNumber = GenerateRegistrationNumber(),
                        WheelCount = _random.Next(4, 19), // 4 to 18 wheels
                        BodyVolume = Math.Round(_random.NextDouble() * 50 + 10, 2) // 10 to 60 m³
                    };
                }

                cars.Add(car);

                // Random delay 0 to 0.5 seconds
                Thread.Sleep(_random.Next(0, 501));

                _progress = (int)((i + 1) * 100.0 / carCount);
            }

            _cachedData[brand] = cars;
            _progress = 100;
            _isLoading = false;
        }

        public static int GetProgress()
        {
            return _progress;
        }

        public static List<Car> GetCars(CarBrand brand)
        {
            if (_cachedData.ContainsKey(brand))
            {
                return _cachedData[brand];
            }
            return new List<Car>();
        }

        private static string GenerateRegistrationNumber()
        {
            string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            return $"{letters[_random.Next(letters.Length)]}{letters[_random.Next(letters.Length)]}" +
                   $"{_random.Next(1000, 10000)}" +
                   $"{letters[_random.Next(letters.Length)]}{letters[_random.Next(letters.Length)]}";
        }

        private static string GenerateMultimediaName()
        {
            string[] systems = { "Android Auto", "Apple CarPlay", "Tesla Infotainment", "BMW iDrive", "Mercedes MBUX", "Audi MMI" };
            return systems[_random.Next(systems.Length)];
        }
    }
}

