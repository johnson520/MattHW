using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace hw3
{
    internal static class Program
    {
        private const string CRLF = "\r\n";
        private const string CRLFx2 = CRLF + CRLF;
//        private const string CacheFolder = @"C:\hw3cache\";
        private const string CacheFolder = @"D:\hw3cache\";

        //  invoke with 127.0.0.1 and port# 5000
        //  set browser proxy settings to these parameters
        internal static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage: IPAddress Port");
                return;
            }

            if (!Directory.Exists(CacheFolder))
            {
                Console.WriteLine($"Cache folder {CacheFolder} does not exist!");
                return;
            }

            var ip = IPAddress.Parse(args[0]);
            var port = int.Parse(args[1]);

            var tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var local = new IPEndPoint(ip, port);

            tcpSocket.Bind(local);
            tcpSocket.Listen(10);

            Console.WriteLine($"Set browser proxy to {ip}:{port}");

            while (true)
            {
                var source = tcpSocket.Accept();
                var state = new State(source);
                source.BeginReceive(state.Buffer, 0, state.Buffer.Length, 0, OnDataReceive, state);
            }
        }

        //  Get and Send Data based on Project #1 implementation
        private static void GetDataAndSend(string url, State state)
        {
            Uri uri;
            IPHostEntry hostEntry;
            var buffer = new byte[10240];

            // make a file name based on our URL for saving in the cache
            var cacheFileName = state.RequestHash.ToString("x8") + " "
                + Regex.Replace(url, $"[{Regex.Escape(string.Join(string.Empty, Path.GetInvalidFileNameChars()))}]+", "-", RegexOptions.Compiled).Trim('-');

            if (cacheFileName.Length > 200)
                cacheFileName = cacheFileName.Substring(0, 200);

            cacheFileName += ".txt";

            if (File.Exists(CacheFolder + cacheFileName))
            {
                //Console.WriteLine("Getting " + url + " from cache");
                var cacheFile = File.OpenRead(CacheFolder + cacheFileName);

                int nBytes;
                do
                {
                    nBytes = cacheFile.Read(buffer, 0, buffer.Length);

                    if (nBytes > 0)
                        state.SourceSocket.Send(buffer, nBytes, SocketFlags.None);
                } while (nBytes > 0);

                cacheFile.Close();
                return;
            }

            try
            {
                //  parse the URL
                if (url.Contains("//"))
                    uri = new Uri(url);
                else if (url.Contains(":443"))
                    uri = new Uri("https://" + url);
                else
                    uri = new Uri("http://" + url);

                if (uri.Scheme != "http")
                    throw new Exception($"{uri.Scheme} is not a supported protocol");

                //Console.WriteLine($"Resolving host {uri.Host} from {url}...");
                //  resolve the host name
                hostEntry = Dns.GetHostEntry(uri.Host);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception {ex.Message} parsing URL");
                throw;
            }

            //  if we have an IP address, we're good
            if (hostEntry.AddressList.Length == 0)
            {
                throw new Exception("Failed to resolve hostname " + uri.Host);
            }

            //Console.WriteLine("Resolved " + uri.Host + " as " + hostEntry.AddressList[0]);
            Console.WriteLine("Getting " + uri.AbsoluteUri);

            //  use .NET's tcp client to send and receive
            var client = new TcpClient();

            //  connect to the IP address using the port specified in the URL (or 80 by default)
            client.Connect(hostEntry.AddressList[0], uri.Port);

            //  get the network stream associated with our tcp client
            var stream = client.GetStream();

            ////Console.WriteLine("Sending data...");
            ////Console.WriteLine(header);

            //  send the bytes we received
            stream.Write(state.Buffer, 0, state.NumBytes);
            //Console.WriteLine("Sent received bytes");

            // create a cache file to save to
            var file = File.Create(CacheFolder + cacheFileName);

            //  keep reading and sending until there's nothing left to read
            var expectedLength = 0;
            var totalReceived = 0;

            do
            {
                //Console.WriteLine("About to read");
                var count = stream.Read(buffer, 0, buffer.Length);
                totalReceived += count;
                //Console.WriteLine("Read " + count + " bytes");

                if (count == 0)
                    break;

                if (expectedLength == 0)
                    expectedLength = GetExpectedLength(buffer);

                file.Write(buffer, 0, count);

                state.SourceSocket.Send(buffer, count, SocketFlags.None);
                //Console.WriteLine("Sent " + count + " bytes");
            } while (expectedLength == 0 || totalReceived < expectedLength);

            Console.WriteLine($"\tDone reading and sending, expected {expectedLength}, received {totalReceived}");

            file.Close();

            //  close both the stream and the client
            stream.Close();
            client.Close();
        }

        private static int GetExpectedLength(byte[] buffer)
        {
            var header = Encoding.ASCII.GetString(buffer);

            var endOfHeader = header.IndexOf(CRLFx2);

            if (endOfHeader == -1)
                return 0;

            var match = Regex.Match(header, @"Content-Length:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            if (match.Success)
            {
                return int.Parse(match.Groups[1].Value) + endOfHeader + CRLFx2.Length;
            }
            return 0;
        }

        private static void OnDataReceive(IAsyncResult result)
        {
            var state = (State)result.AsyncState;
            try
            {
                state.NumBytes = state.SourceSocket.EndReceive(result);
                if (state.NumBytes > 0)
                {
                    ParseRequestAndRespond(state);
                    state.SourceSocket.BeginReceive(state.Buffer, 0, state.Buffer.Length, 0, OnDataReceive, state);
                }
            }
            catch (Exception ex)
            {
                if (!(ex is ArgumentException))
                {
                    Console.WriteLine($"Caught exception {ex.GetType().FullName}: {ex.Message}");
                    Console.WriteLine("Stack trace:");
                    Console.WriteLine(ex.StackTrace);
                }
                state.SourceSocket.Close();
            }
        }

        private static void ParseRequestAndRespond(State state)
        {
            //  convert received bytes to a string
            var receivedString = Encoding.ASCII.GetString(state.Buffer, 0, state.NumBytes);

            // find end of HTTP/1.1 header
            state.RequestHash = receivedString.GetHashCode();

            var endOfHeaderLine1 = receivedString.IndexOf(CRLF);

            if (endOfHeaderLine1 == -1)
                throw new ArgumentException("Didn't find the end of the first header line");

            //  extract header from receivedString
            var headerLine1 = receivedString.Substring(0, endOfHeaderLine1);

            //  split line0 into parts on spaces
            var line1Parts = headerLine1.Split(' ');

            if (line1Parts.Length != 3)
                throw new ArgumentException($"{headerLine1} not recognized as an HTTP/1.1 header");

            if (line1Parts[0] != "GET" && line1Parts[0] != "HEAD" && line1Parts[0] != "POST")
                throw new ArgumentException($"{headerLine1} is not a GET, HEAD, or POST");

            //  grab the url from the first line
            var url = line1Parts[1];
            //Console.WriteLine("Fetching data for " + url);

            // get data from url and send to our socket
            GetDataAndSend(url, state);
        }

        //  declare a class for the private state of our asynchronous callback
        private class State
        {
            public State(Socket source)
            {
                SourceSocket = source;
                Buffer = new byte[8192];
                Array.Clear(Buffer, 0, Buffer.Length);
                NumBytes = 0;
            }

            public Socket SourceSocket { get; }
            public byte[] Buffer { get; }
            public int NumBytes { get; set; }
            public int RequestHash { get; set; }
        }
    }
}