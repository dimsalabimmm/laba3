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

        public async Task<string> ConnectAsync()
        {
            if (_isConnected)
            {
                return "Already connected";
            }

            try
            {
                _client = new TcpClient();
                _client.ReceiveTimeout = 5000;
                _client.SendTimeout = 5000;
                
                // Add connection timeout (10 seconds)
                var connectTask = _client.ConnectAsync(_ipAddress, _port);
                var timeoutTask = Task.Delay(10000);
                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    // Connection timed out
                    _client?.Close();
                    _client = null;
                    _isConnected = false;
                    ConnectionStatusChanged?.Invoke(false);
                    return $"Connection timeout: Unable to connect to {_ipAddress}:{_port} (10 seconds)";
                }
                
                // Wait for the connect task to complete
                await connectTask;
                
                _stream = _client.GetStream();
                
                // Wait a bit to ensure connection is stable
                await Task.Delay(100);
                
                if (_client.Connected)
                {
                    _isConnected = true;
                    _cancellationTokenSource = new CancellationTokenSource();

                    ConnectionStatusChanged?.Invoke(true);
                    // Запускаем асинхронные задачи в фоне (fire-and-forget)
                    var receiveTask = Task.Run(async () => await ReceiveData(_cancellationTokenSource.Token).ConfigureAwait(false));
                    var pingTask = Task.Run(async () => await PingLoop(_cancellationTokenSource.Token).ConfigureAwait(false));
                    // Намеренно не ждем завершения этих задач - они работают в фоне
                    return null; // Success
                }
                else
                {
                    // Connection failed, dispose resources
                    _stream?.Close();
                    _client?.Close();
                    _stream = null;
                    _client = null;
                    _isConnected = false;
                    ConnectionStatusChanged?.Invoke(false);
                    return $"Connection failed: Unable to establish connection to {_ipAddress}:{_port}";
                }
            }
            catch (Exception ex)
            {
                // Exception occurred, dispose resources
                try
                {
                    _stream?.Close();
                    _client?.Close();
                }
                catch { }
                
                _stream = null;
                _client = null;
                _isConnected = false;
                ConnectionStatusChanged?.Invoke(false);
                return $"Connection error: {ex.Message}";
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
                    await Task.Delay(3000, cancellationToken); // Check every 3 seconds

                    if (_client != null && _client.Connected)
                    {
                        // Check if socket is still connected
                        bool isSocketConnected = !(_client.Client.Poll(1000, SelectMode.SelectRead) && _client.Client.Available == 0);
                        
                        if (isSocketConnected)
                        {
                            // Connection is still alive
                            ConnectionStatusChanged?.Invoke(true);
                        }
                        else
                        {
                            // Connection is dead
                            _isConnected = false;
                            ConnectionStatusChanged?.Invoke(false);
                            break;
                        }
                    }
                    else
                    {
                        _isConnected = false;
                        ConnectionStatusChanged?.Invoke(false);
                        break;
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

