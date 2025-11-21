using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;

namespace Laba3
{
    public class LoaderClient
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private BinaryFormatter _formatter;
        private bool _isConnected;
        private readonly string _ipAddress;
        private readonly int _port;
        private CancellationTokenSource _cancellationTokenSource;

        public event Action<CarBrand> BrandReceived;
        public event Action<bool> ConnectionStatusChanged;

        public string IpAddress => _ipAddress;
        public int Port => _port;
        public bool IsConnected => _isConnected;

        public LoaderClient(string ipAddress, int port)
        {
            _ipAddress = ipAddress;
            _port = port;
            _formatter = new BinaryFormatter();
        }

        public async Task ConnectAsync()
        {
            if (_isConnected)
            {
                return;
            }

            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(_ipAddress, _port);
                _stream = _client.GetStream();
                _isConnected = true;
                _cancellationTokenSource = new CancellationTokenSource();

                ConnectionStatusChanged?.Invoke(true);
                Task.Run(() => ReceiveData(_cancellationTokenSource.Token));
                Task.Run(() => PingLoop(_cancellationTokenSource.Token));
            }
            catch
            {
                _isConnected = false;
                ConnectionStatusChanged?.Invoke(false);
            }
        }

        public void Disconnect()
        {
            _isConnected = false;
            _cancellationTokenSource?.Cancel();

            try
            {
                _stream?.Close();
                _client?.Close();
            }
            catch { }

            ConnectionStatusChanged?.Invoke(false);
        }

        private async Task ReceiveData(CancellationToken cancellationToken)
        {
            while (_isConnected && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_stream != null && _client.Connected && _stream.DataAvailable)
                    {
                        var brand = (CarBrand)_formatter.Deserialize(_stream);
                        if (brand != null)
                        {
                            BrandReceived?.Invoke(brand);
                        }
                    }
                    await Task.Delay(100, cancellationToken);
                }
                catch (Exception)
                {
                    _isConnected = false;
                    ConnectionStatusChanged?.Invoke(false);
                    break;
                }
            }
        }

        private async Task PingLoop(CancellationToken cancellationToken)
        {
            while (_isConnected && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, cancellationToken); // Ping каждую секунду

                    if (_client != null && _client.Connected)
                    {
                        // Простая проверка соединения
                        if (!_client.Client.Poll(0, SelectMode.SelectRead) || _client.Available > 0)
                        {
                            ConnectionStatusChanged?.Invoke(true);
                        }
                        else
                        {
                            _isConnected = false;
                            ConnectionStatusChanged?.Invoke(false);
                        }
                    }
                    else
                    {
                        _isConnected = false;
                        ConnectionStatusChanged?.Invoke(false);
                    }
                }
                catch
                {
                    _isConnected = false;
                    ConnectionStatusChanged?.Invoke(false);
                    break;
                }
            }
        }

        public void SendRequest(byte[] requestData)
        {
            if (_isConnected && _stream != null && _stream.CanWrite)
            {
                try
                {
                    _stream.Write(requestData, 0, requestData.Length);
                    _stream.Flush();
                }
                catch
                {
                    _isConnected = false;
                    ConnectionStatusChanged?.Invoke(false);
                }
            }
        }
    }
}

