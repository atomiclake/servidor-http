using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SimpleHttpServer;

internal class Program
{
    private const int HTTP_SERVER_PORT = 80;
    private const int BUFFER_SIZE = 512;

    static void Main(string[] args)
    {
        // Check if the server files are present
        string currentPath = Environment.CurrentDirectory;

        string serverRootPath = Path.Combine(currentPath, "server_root");

        if (!Directory.Exists(serverRootPath))
        {
            Console.WriteLine($"Could not find server root path: {serverRootPath}");
            return;
        }

        string serverIndexPage = Path.Combine(serverRootPath, "index.html");

        if (!File.Exists(serverIndexPage))
        {
            Console.WriteLine($"Could not find server index file: {serverIndexPage}");
            return;
        }

        string notFoundPageSource = """
            <!DOCTYPE html>

            <html>
            <head>
                <meta charset="utf-8" />
                <title>Not found</title>
            </head>

            <body>
                <h1>Not found</h1>
                <p>The server could not find the resource you were looking for.</p>
            </body>
            </html>

            """;

        // Server setup
        IPAddress serverAddress = IPAddress.Loopback;
        int serverPort = HTTP_SERVER_PORT;
        IPEndPoint localEndPoint = new(serverAddress, serverPort);
        Socket serverSocket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        serverSocket.Bind(localEndPoint);
        serverSocket.Listen();

        Console.WriteLine($"Listening on {serverAddress}:{serverPort}");

        List<byte> data = [];
        Span<byte> dataBuffer = stackalloc byte[BUFFER_SIZE];
        StringBuilder responseBuilder = new();

        // Main loop
        while (true)
        {
            // Receive client data
            Socket clientSocket = serverSocket.Accept();
            string clientAddress = (((IPEndPoint?)clientSocket.RemoteEndPoint)?.Address.ToString()) ?? "<No remote address>";

            Console.WriteLine($"Client {clientAddress} connected");

            int bytesRead = clientSocket.Receive(dataBuffer);

            while (true)
            {
                data.AddRange(dataBuffer[0..bytesRead]);
                bytesRead = clientSocket.Receive(dataBuffer);

                if (bytesRead != BUFFER_SIZE)
                {
                    data.AddRange(dataBuffer[0..bytesRead]);
                    break;
                }
            }

            string request = Encoding.ASCII.GetString(data.ToArray());

            if (string.IsNullOrEmpty(request))
            {
                Console.WriteLine("Empty request");

                // Cleanup
                data.Clear();
                clientSocket.Disconnect(true);
                continue;
            }

            Console.WriteLine($"Got {request.Length} bytes from the client");

            // Handle request
            string[] lines = request.Split('\n');

            string[] firstLine = lines[0].Split(' ');

            switch (firstLine[0])
            {
                case "GET":
                    // Parse the header
                    string resource = firstLine[1];
                    string httpVersion = firstLine[2];
                    string targetResource = "";

                    if (resource == "/")
                    {
                        targetResource = "index.html";
                    }

                    Console.WriteLine($"Get resource {targetResource}");

                    // Get the resource content (if it exists)
                    string resourceFullPath = Path.Combine(serverRootPath, targetResource.Replace("/", ""));
                    string content = "";

                    if (!File.Exists(resourceFullPath))
                    {
                        content = notFoundPageSource;
                        Console.WriteLine($"Could not locate resource {resourceFullPath}");
                        
                        _ = responseBuilder.AppendLine("HTTP/1.1 404 Not Found")
                            .AppendLine("Server: Custom")
                            .AppendLine("Content-Type: text/html")
                            .AppendLine($"Content-Length: {content.Length}");
                    }
                    else
                    {
                        string fileContent = File.ReadAllText(resourceFullPath);
                        content = fileContent;
                        Console.WriteLine($"Reading file {resourceFullPath}");

                        _ = responseBuilder.AppendLine("HTTP/1.1 200 OK")
                            .AppendLine("Server: Custom")
                            .AppendLine("Content-Type: text/html")
                            .AppendLine($"Content-Length: {content.Length}");
                    }

                    _ = responseBuilder.AppendLine()
                        .Append(content);

                    // Send response to client
                    string response = responseBuilder.ToString();
                    byte[] responseData = Encoding.UTF8.GetBytes(response);

                    _ = clientSocket.Send(responseData);
                    Console.WriteLine($"Sent {responseData.Length} bytes to the client");
                    break;

                default:
                    Console.WriteLine($"Unknown method {firstLine[0]}");
                    break;
            }

            // Cleanup
            responseBuilder.Clear();
            data.Clear();
            clientSocket.Disconnect(true);
        }
    }
}
