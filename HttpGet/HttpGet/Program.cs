using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace HttpGet
{
    internal class Program
    {
        private const string GetHeader = @"GET / HTTP/1.1
Host: {hostname}
Connection: close
User-Agent: MyHttpGetter/1.0 (+http://www.baccano.com/)
Accept-Charset: ISO-8859-1,UTF-8;q=0.7,*;q=0.7
Cache-Control: no-cache
Accept-Language: en-us
Referer: http://www.baccano.com/
";

        private static async Task<string> HttpRequestAsync(string hostName)
        {
            using (var tcp = new TcpClient(hostName, 80))
            {
                using (var stream = tcp.GetStream())
                {
                    tcp.SendTimeout = 500;
                    tcp.ReceiveTimeout = 1000;
                    // Send request headers
                    var builder = new StringBuilder();
                    builder.AppendLine("GET /?scope=images&nr=1 HTTP/1.1");
                    builder.AppendLine($"Host: {hostName}");
                    //builder.AppendLine("Content-Length: " + data.Length);   // only for POST request
                    builder.AppendLine("Connection: close");
                    builder.AppendLine();
                    var header = Encoding.ASCII.GetBytes(builder.ToString());
                    await stream.WriteAsync(header, 0, header.Length);

                    // Send payload data if you are POST request
                    //await stream.WriteAsync(data, 0, data.Length);

                    // receive data
                    using (var memory = new MemoryStream())
                    {
                        await stream.CopyToAsync(memory);
                        memory.Position = 0;
                        var data = memory.ToArray();

                        var crlf = Encoding.ASCII.GetBytes("\r\n");
                        int i;
                        for (i = 0; i < data.Length - 4; ++i)
                        {
                            if (data[i] == crlf[0] && data[i + 1] == crlf[1] && data[i + 2] == crlf[0] && data[i + 3] == crlf[1])
                            {
                                i += 4;
                                break;
                            }
                        }

                        var responseHeaders = Encoding.ASCII.GetString(data, 0, i);
                        Console.WriteLine("Response headers: " + responseHeaders);
                        memory.Position = i;
                        return Encoding.UTF8.GetString(data, i, data.Length - i);
                    }
                }
            }
        }

        private static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Usage: HttpGet hostname");
                return 1;
            }

            var response = HttpRequestAsync(args[0]).Result;
            Console.WriteLine("Response body: " + response);

            Console.WriteLine("Press enter to continue...");
            Console.Read();
            return 0;

            var hostEntry = Dns.GetHostEntry(args[0]);
            if (hostEntry.AddressList.Length > 0)
            {
                var client = new TcpClient(hostEntry.HostName, 80) { NoDelay = true };

                // Translate the passed message into ASCII and store it as a Byte array.
                var data = Encoding.ASCII.GetBytes(GetHeader);

                // Get a client stream for reading and writing.
                //  Stream stream = client.GetStream();

                var stream = client.GetStream();

                // Send the message to the connected TcpServer. 
                stream.Write(data, 0, data.Length);

                Console.WriteLine("Sent get header");

                // Receive the TcpServer.response.

                // Buffer to store the response bytes.
                data = new byte[256];

                // String to store the response ASCII representation.

                // Read the first batch of the TcpServer response bytes.
                var bytes = stream.Read(data, 0, data.Length);
                var responseData = Encoding.ASCII.GetString(data, 0, bytes);
                Console.WriteLine("Received: {0}", responseData);

                // Close everything.
                stream.Close();
                client.Close();
            }

            return 0;
        }
    }
}