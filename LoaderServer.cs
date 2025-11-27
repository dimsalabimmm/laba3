using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;

namespace Laba3
{
    public class LoaderServer
    {
        private TcpListener _listener;
        private bool _isRunning;
        private readonly Random _random = new Random();
        private readonly List<TcpClient> _clients = new List<TcpClient>();
        private readonly object _clientsLock = new object();

        public int Port { get; private set; }
        public string IpAddress { get; private set; }

        public LoaderServer(string ipAddress, int port)
        {
            IpAddress = ipAddress;
            Port = port;
        }

        public void Start()
        {
            if (_isRunning)
            {
                return;
            }

            try
            {
                IPAddress ip = IPAddress.Parse(IpAddress);
                _listener = new TcpListener(ip, Port);
                _listener.Start();
                _isRunning = true;

                Task.Run(() => AcceptClients());
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to start server: {ex.Message}");
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();

            lock (_clientsLock)
            {
                foreach (var client in _clients)
                {
                    try
                    {
                        client.Close();
                    }
                    catch { }
                }
                _clients.Clear();
            }
        }

        private async Task AcceptClients()
        {
            while (_isRunning)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    lock (_clientsLock)
                    {
                        _clients.Add(client);
                    }
                    _ = Task.Run(() => HandleClientRequests(client));
                }
                catch
                {
                    break;
                }
            }
        }

        private async Task HandleClientRequests(TcpClient client)
        {
            try
            {
                var stream = client.GetStream();
                var buffer = new byte[1024];

                while (_isRunning && client.Connected)
                {
                    if (stream.DataAvailable)
                    {
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            var brand = GenerateRandomBrand();
                            SendBrand(client, brand);
                        }
                    }
                    await Task.Delay(100);
                }
                
                lock (_clientsLock)
                {
                    if (_clients.Contains(client))
                    {
                        _clients.Remove(client);
                    }
                }
                
                try
                {
                    stream?.Close();
                    client?.Close();
                }
                catch { }
            }
            catch
            {
                lock (_clientsLock)
                {
                    if (_clients.Contains(client))
                    {
                        _clients.Remove(client);
                    }
                }
                
                try
                {
                    client.Close();
                }
                catch { }
            }
        }

        private void SendBrand(TcpClient client, CarBrand brand)
        {
            try
            {
                var stream = client.GetStream();
                var formatter = new BinaryFormatter();
                formatter.Serialize(stream, brand);
                stream.Flush();
            }
            catch { }
        }

        private CarBrand GenerateRandomBrand()
        {
            var brandNames = new[] { "Toyota", "Honda", "Ford", "BMW", "Mercedes", "Audi", "Volkswagen", "Nissan", "Hyundai", "Kia" };
            var modelNames = new[] { "Model A", "Model B", "Model C", "Model X", "Model Y", "Classic", "Sport", "Premium" };

            return new CarBrand
            {
                BrandName = brandNames[_random.Next(brandNames.Length)],
                ModelName = modelNames[_random.Next(modelNames.Length)],
                HorsePower = _random.Next(100, 500),
                MaxSpeed = _random.Next(150, 300),
                Type = _random.Next(2) == 0 ? CarType.Passenger : CarType.Truck
            };
        }
    }
}
