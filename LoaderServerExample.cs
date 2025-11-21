using System;
using System.Windows.Forms;

namespace Laba3
{
    // Пример использования LoaderServer
    // Можно создать отдельное консольное приложение или добавить в форму
    public class LoaderServerExample
    {
        public static void RunServer(string ipAddress, int port)
        {
            var server = new LoaderServer(ipAddress, port);
            try
            {
                server.Start();
                Console.WriteLine($"Loader server started on {ipAddress}:{port}");
                Console.WriteLine("Press any key to stop...");
                Console.ReadKey();
                server.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        // Пример запуска нескольких серверов:
        // RunServer("127.0.0.1", 8080);
        // RunServer("127.0.0.1", 8081);
        // RunServer("127.0.0.1", 8082);
    }
}

