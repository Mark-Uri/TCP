using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

public interface INetworkClient
{
    Task ConnectAsync(string serverIp, int port);
    Task SendCoordinatesAsync(int x, int y);
    void Disconnect();
}

public interface ILoggerService
{
    void Log(string message);
    void LogError(string message);
}

public class ConsoleLogger : ILoggerService
{
    public void Log(string message) => Console.WriteLine(message);
    public void LogError(string message) => Console.Error.WriteLine(message);
}

public class TcpGameClient : INetworkClient
{
    private readonly ILoggerService logger;
    private TcpClient? client;
    private string? ip;
    private int port;
    private NetworkStream? stream;




    public TcpGameClient(ILoggerService logger)
    {
        this.logger = logger;
    }

    public async Task ConnectAsync(string serverIp, int port)
    {
        ip = serverIp;
        this.port = port;

        while (true)
        {
            try
            {
                client = new TcpClient();
                await client.ConnectAsync(serverIp, port);
                stream = client.GetStream();
                logger.Log("Подключение к серверу установлено");
                _ = Task.Run(ReceiveLoop);
                break;
            }
            catch
            {
                logger.LogError("Сервер не найден. Пытаемся запустить...");
                try
                {

                    Process.Start(@"C:\\Users\\USER\\Desktop\\Новая папка (2)\\Server\\bin\\Debug\\net9.0\\Server.exe");
                    await Task.Delay(2000);

                     
                }
                catch (Exception ex)
                {

                    logger.LogError("Не удалось запустить сервер: " + ex.Message);
                    return;
                }
            }


        }
    }

    public async Task SendCoordinatesAsync(int x, int y)
    {
        if (client == null || !client.Connected || stream == null)
        {
            logger.LogError("Клиент не подключен Попытка переподключения...");
            await ConnectAsync(ip!, port);
        }

        try
        {
            string msg = $"{x:D3} {y:D3}";
            var data = Encoding.UTF8.GetBytes(msg);
            await stream!.WriteAsync(data, 0, data.Length);
        }
        catch (Exception ex)
        {
            logger.LogError("Ошибка отправки: " + ex.Message);
        }
    }

    public void Disconnect()
    {
        client?.Close();
        logger.Log("Клиент отключен.");
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[512];
        try
        {
            while (true)
            {
                int bytesRead = await stream!.ReadAsync(buffer);



                if (bytesRead == 0) break;
                string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);


                Console.Clear();
                Console.WriteLine(response);
            }
        }
        catch
        {
            logger.LogError("Подключение к серверу потеряно");
        }
    }
}

class Program
{
    static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "CLIENT SIDE";
        var logger = new ConsoleLogger();
        var client = new TcpGameClient(logger);



        
        while (true)
        {
            await client.ConnectAsync("127.0.0.1", 27015);
            int x = 1, y = 1;

            while (true)
            {
                var key = Console.ReadKey(true).Key;
                int newX = x, newY = y;

                switch (key)
                {
                    case ConsoleKey.UpArrow: newY--; break;
                    case ConsoleKey.DownArrow: newY++; break;
                    case ConsoleKey.LeftArrow: newX--; break;
                    case ConsoleKey.RightArrow: newX++; break;
                    case ConsoleKey.Escape:
                        client.Disconnect();
                        return;
                }

                await client.SendCoordinatesAsync(newX, newY);
                x = newX;
                y = newY;
            }



            Console.Write("Играть ещё раз? (y/n): ");
            if (Console.ReadKey(true).Key != ConsoleKey.Y) break;
        }

        client.Disconnect();
    }
}
