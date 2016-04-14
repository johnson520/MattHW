using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HttpGet
{
    internal static class Program
    {
        private const string CRLF = "\r\n";
        private const string CRLFx2 = CRLF + CRLF;

        private static int Main()
        {
            Uri uri;
            IPHostEntry hostEntry;
            do
            {
                string url;
                do
                {
                    Console.WriteLine("\nPlease enter an URL:");
                    url = Console.ReadLine();
                } while (url == null || url.Trim() == "");

                try
                {
                    //  parse the URL
                    uri = new Uri(url);

                    if (uri.Scheme != "http")
                        throw new Exception("http is the only supported protocol");

                    //  resolve the host name
                    hostEntry = Dns.GetHostEntry(uri.Host);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    uri = null;
                    hostEntry = null;
                }

            } while (uri == null || hostEntry == null);

            //  if we have an IP address, we're good
            if (hostEntry.AddressList.Length == 0)
            {
                Console.WriteLine("Failed to resolve hostname " + uri.Host);
                return 1;
            }

            //  use .NET's tcp client to send and receive
            var client = new TcpClient();

            //  connect to the IP address using the port specified in the URL (or 80 by default)
            client.Connect(hostEntry.AddressList[0], uri.Port);

            //  format a minimal HTTP 1.1 GET header
            var header = "GET " + uri.PathAndQuery + " HTTP/1.1" + CRLF
                         + "Host: " + uri.Host + CRLF
                         + "Connection: close" + CRLFx2;

            //  transform the unicode string to ASCII
            var headerBytes = Encoding.ASCII.GetBytes(header);

            //  get the network stream associated with our tcp client
            var stream = client.GetStream();

            //  send the GET header
            stream.Write(headerBytes, 0, headerBytes.Length);

            //  use a StringBuilder class to accumulate the response string
            var sb = new StringBuilder();

            //  keep reading until there's nothing left to read appending each chunk to our StringBuilder
            int count;
            do
            {
                var buffer = new byte[1024];
                count = stream.Read(buffer, 0, buffer.Length);
                sb.Append(Encoding.ASCII.GetString(buffer, 0, count));
            } while (count > 0);

            //  close both the stream and the client
            stream.Close();
            client.Close();

            //  get the full response string out of the StringBuilder
            var fullResponse = sb.ToString();

            //  find the end of the response header denoated by two CRLFs
            var endOfHeaderIndex = fullResponse.IndexOf(CRLFx2);

            //  if we found the end of the header, dump our header and body to the console
            if (endOfHeaderIndex != -1)
            {
                Console.WriteLine("Response header:\n\n" + fullResponse.Substring(0, endOfHeaderIndex));

                Console.WriteLine("\n\nPress a key to continue...");
                Console.ReadKey();

                Console.WriteLine("Response body:\n\n" + fullResponse.Substring(endOfHeaderIndex + CRLF.Length));
            }
            else
            {
                //  if we failed to find two CRLFs in a row, just dump the whole response
                Console.WriteLine("Received: {0}", fullResponse);
            }

            Console.WriteLine("\n\nPress a key to continue...");
            Console.ReadKey();

            return 0;
        }
    }
}