using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Timers;



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



public class TcpGameServer
{
    private const int Port = 27015;
    private const int buffer = 512;
    private readonly ILoggerService logger;
    private TcpListener? listener;
    private TcpClient? client;
    private NetworkStream? stream;
    private System.Timers.Timer? gameTimer;






    private char[,] map = new char[7, 13];
    private (int x, int y) clientPos;
    private (int x, int y) serverPos;
    private int clientScore;
    private int serverScore;


    public TcpGameServer(ILoggerService logger)
    {
        this.logger = logger;
    }

    public async Task StartAsync()
    {
        while (true)
        {

            InitGame();
            listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();


            logger.Log("Сервер запущен. Ожидание клиента...");

            client = await listener.AcceptTcpClientAsync();
            stream = client.GetStream();

            logger.Log("Клиент подключился");

            gameTimer = new System.Timers.Timer(60000);
            gameTimer.Elapsed += (s, e) => EndGame();
            gameTimer.Start();




            await SendMapAsync();
            await ListenAsync();


            client.Close();
            logger.Log("Клиент отключился.");

            Console.Write("Играть ещё раз? (y/n): ");
            if (Console.ReadKey(true).Key != ConsoleKey.Y) break;
        }
    }


    private void InitGame()
    {
        map = new char[,]
        {
            { '#', '#', '#', '#', '#', '#', '#', '#', '#', '#', '#', '#', '#' },
            { '#', '.', '.', '.', 'S', '.', '.', '.', '.', 'S', '.', '.', '#' },
            { '#', '.', '#', '#', '#', '.', '#', '#', '#', '.', '#', '.', '#' },
            { '#', '.', '.', '.', '.', '.', '.', '.', '.', '.', '.', '.', '#' },
            { '#', '.', '#', '#', '#', '#', '#', '#', '#', '#', '#', '.', '#' },
            { '#', '.', '.', '.', '.', '.', '.', 'S', '.', '.', '.', 'F', '#' },
            { '#', '#', '#', '#', '#', '#', '#', '#', '#', '#', '#', '#', '#' },
        };
        clientPos = (1, 1);
        serverPos = (1, 2);
        clientScore = 0;
        serverScore = 0;
    }





    private async Task ListenAsync()
    {
        var buffer = new byte[TcpGameServer.buffer];


        while (true)
        {
            int bytesRead = await stream!.ReadAsync(buffer);
            if (bytesRead == 0) break;



            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            if (message.Length >= 7 && int.TryParse(message[..3], out int cx) && int.TryParse(message.Substring(4, 3), out int cy))
            {

                if (map[cy, cx] != '#')
                {

                    if (map[cy, cx] == 'S') { clientScore++; map[cy, cx] = '.'; }
                    if (map[cy, cx] == 'F') EndGame();


                    clientPos = (cx, cy);
                }

                var moves = new[] { (0, 1), (1, 0), (0, -1), (-1, 0) };
                var rnd = new Random();


                foreach (var move in moves.OrderBy(x => rnd.Next()))
                {
                    int nx = serverPos.x + move.Item1;
                    int ny = serverPos.y + move.Item2;


                    if (map[ny, nx] != '#' && (nx, ny) != clientPos)
                    {
                        if (map[ny, nx] == 'S') { serverScore++; map[ny, nx] = '.'; }
                        if (map[ny, nx] == 'F') EndGame();
                        serverPos = (nx, ny);
                        break;


                    }
                }

                await SendMapAsync();
            }
        }
    }





    private async Task SendMapAsync()
    {
        var sb = new StringBuilder();
        for (int y = 0; y < map.GetLength(0); y++)
        {
            for (int x = 0; x < map.GetLength(1); x++)
            {
                if ((x, y) == clientPos) sb.Append('C');
                else if ((x, y) == serverPos) sb.Append('K');
                else sb.Append(map[y, x]);
            }
            sb.AppendLine();
        }
        sb.AppendLine($"Очки C (клиент): {clientScore} | Очки K (сервер): {serverScore}");
        sb.AppendLine("Задача: собрать больше сундуков (S) или добраться до финиша (F) до истечения времени!");
        byte[] data = Encoding.UTF8.GetBytes(sb.ToString());
        await stream!.WriteAsync(data);



    }

    private void EndGame()
    {
        string result = clientScore > serverScore ? "Клиент победил!" : clientScore < serverScore ? "Сервер победил!" : "Ничья!";
        byte[] endMsg = Encoding.UTF8.GetBytes("Игра окончена! " + result);
        try
        {
            stream?.Write(endMsg, 0, endMsg.Length);
        }
        catch { }


        stream?.Close();
        client?.Close();
        logger.Log("Игра завершена");
    }
}

class Program
{
    static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "SERVER SIDE";
        var logger = new ConsoleLogger();
        var server = new TcpGameServer(logger);
        await server.StartAsync();
    }
}
