using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Laba3
{
    public static class Loader
    {
        private static readonly Dictionary<string, List<ICar>> Cache = new Dictionary<string, List<ICar>>();
        private static readonly Random Random = new Random();
        private static readonly object Locker = new object();

        private static double _progress;

        public static double GetProgress()
        {
            lock (Locker)
            {
                return _progress;
            }
        }

        public static Task<List<ICar>> LoadAsync(CarBrand brand, CancellationToken token)
        {
            if (brand == null)
            {
                throw new ArgumentNullException(nameof(brand));
            }

            lock (Locker)
            {
                if (Cache.TryGetValue(brand.Id, out var cached))
                {
                    _progress = 1.0;
                    return Task.FromResult(new List<ICar>(cached));
                }

                _progress = 0;
            }

            return Task.Run(async () =>
            {
                var list = new List<ICar>();
                int count = NextInt(10, 21);

                for (int i = 0; i < count; i++)
                {
                    token.ThrowIfCancellationRequested();

                    int delay = NextInt(0, 501);
                    if (delay > 0)
                    {
                        await Task.Delay(delay, token).ConfigureAwait(false);
                    }

                    ICar car;
                    if (brand.Type == CarType.Passenger)
                    {
                        car = new PassengerCar
                        {
                            RegistrationNumber = GenerateRegistrationNumber(),
                            MultimediaName = GenerateMultimediaName(),
                            AirbagCount = NextInt(2, 9)
                        };
                    }
                    else
                    {
                        car = new Truck
                        {
                            RegistrationNumber = GenerateRegistrationNumber(),
                            WheelCount = NextInt(4, 19),
                            BodyVolume = Math.Round(NextDouble() * 45 + 5, 1)
                        };
                    }

                    list.Add(car);

                    lock (Locker)
                    {
                        _progress = (double)(i + 1) / count;
                    }
                }

                lock (Locker)
                {
                    Cache[brand.Id] = new List<ICar>(list);
                    _progress = 1.0;
                }

                return list;
            }, token);
        }

        public static void Invalidate(string brandId)
        {
            if (string.IsNullOrWhiteSpace(brandId))
            {
                return;
            }

            lock (Locker)
            {
                if (Cache.ContainsKey(brandId))
                {
                    Cache.Remove(brandId);
                }
            }
        }

        public static void Clear()
        {
            lock (Locker)
            {
                Cache.Clear();
                _progress = 0;
            }
        }

        private static string GenerateRegistrationNumber()
        {
            lock (Locker)
            {
                string letters = $"{(char)Random.Next(65, 91)}{(char)Random.Next(65, 91)}";
                string digits = Random.Next(100, 999).ToString("D3");
                string suffix = $"{(char)Random.Next(65, 91)}{(char)Random.Next(65, 91)}";
                return $"{letters}-{digits}-{suffix}";
            }
        }

        private static string GenerateMultimediaName()
        {
            string[] systems =
            {
                "SkyLink",
                "NovaDrive",
                "PulseMedia",
                "HarmonyPlay",
                "Quantum Sound",
                "StreetBeat",
                "AuroraCast",
                "MetroWave"
            };

            lock (Locker)
            {
                return systems[Random.Next(systems.Length)];
            }
        }

        private static int NextInt(int minInclusive, int maxExclusive)
        {
            lock (Locker)
            {
                return Random.Next(minInclusive, maxExclusive);
            }
        }

        private static double NextDouble()
        {
            lock (Locker)
            {
                return Random.NextDouble();
            }
        }
    }
}

