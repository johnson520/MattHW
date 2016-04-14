using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HttpGet
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Usage: HttpGet hostname");
                return 1;
            }

            var uri = new Uri(args[0]);

            var hostEntry = Dns.GetHostEntry(uri.Host);
            if (hostEntry.AddressList.Length > 0)
            {
                using (var client = new TcpClient())
                {
                    client.NoDelay = true;
                    client.Connect(hostEntry.AddressList[0], 80);

                    const string CRLF = "\r\n";
                    const string CRLFx2 = CRLF + CRLF;
                    var header = "GET " + uri.LocalPath + " HTTP/1.1" + CRLF
                                 + "Host: " + uri.Host + CRLF
                                 + "Connection: close" + CRLFx2;

                    var headerBytes = Encoding.ASCII.GetBytes(header);

                    using (var stream = client.GetStream())
                    {
                        stream.Write(headerBytes, 0, headerBytes.Length);

                        var sb = new StringBuilder();
                        int count;
                        do
                        {
                            var buffer = new byte[1024];
                            count = stream.Read(buffer, 0, buffer.Length);
                            sb.Append(Encoding.ASCII.GetString(buffer, 0, count));
                        } while (count > 0);

                        var fullResponse = sb.ToString();
                        var endOfHeaderIndex = fullResponse.IndexOf(CRLFx2);
                        if (endOfHeaderIndex != -1)
                        {
                            Console.WriteLine("Response header:\n\n" + fullResponse.Substring(0, endOfHeaderIndex));

                            Console.WriteLine("\n\nPress a key to continue...");
                            Console.ReadKey();

                            Console.WriteLine("Response body:\n\n" + fullResponse.Substring(endOfHeaderIndex + CRLF.Length));
                        }
                        else
                        {
                            Console.WriteLine("Received: {0}", fullResponse);
                        }

                        stream.Close();
                    }

                    client.Close();
                }
            }

            Console.WriteLine("\n\nPress a key to continue...");
            Console.ReadKey();

            return 0;
        }
    }
}