/*
 * Telemedycyna i Technologie Sieciowe
 * Laboratorium. Gniazda sieciowe cz.2: Wielowatkowa obsluga polaczen
 * Klasa serwera echo (wielowatkowy)
 * v.0.1.a, 2018-03-12, Marcin.Rudzki@polsl.pl 
 * fork -> mini serwer HTTP: v0.1, 2019-12-31, Jacek.Kawa@polsl.pl
 */

using System;
using System.Collections.Generic;
//using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Web;


namespace TCPSockets2
{
    class ThreadedHTTPServer : IServer
    {
        // klasa obslugujaca polaczenie z jednym klientem
        class ClientHelper
        {
            Socket socket;    // otwarte gniazdo polaczenia
            NetworkStream ns; // strumien sieciowy "na gniezdzie"
            StreamReader sr;  // strumien do odbierania danych "na s.sieciowym"
            StreamWriter sw;  // strumien do wysylania danych "na s.sieciowym"
			ThreadedHTTPServer server;
            
			const string BaseDirectory = @"d:";
			//const string BaseDirectory = @"/tmp";

            

            public ClientHelper(Socket socket, ThreadedHTTPServer server)
            {
                this.socket = socket;
                ns = new NetworkStream(this.socket);
                sr = new StreamReader(ns);
                sw = new StreamWriter(ns);
                sw.AutoFlush = true;
                this.server = server;
            }
			// pozyczone z https://stackoverflow.com/questions/2030847/best-way-to-read-a-large-file-into-a-byte-array-in-c?utm_medium=organic&utm_source=google_rich_qa&utm_campaign=google_rich_qa
			public byte[] FileToByteArray(string fileName)
			{
				byte[] buff = null;
				FileStream fs = new FileStream(fileName, 
					FileMode.Open, 
					FileAccess.Read);
				BinaryReader br = new BinaryReader(fs);
				long numBytes = new FileInfo(fileName).Length;
				buff = br.ReadBytes((int) numBytes);
				return buff;
			}
			private void GET(String uri, Dictionary<String, String> headers)
			{


				//dalej mało eleganckie
				if (uri == "/")
					uri = BaseDirectory + "/index.html";
				else
					uri = BaseDirectory + uri.Replace(@"/", @"\");
					//uri = BaseDirectory + uri;
				
				//uri = BaseDirectory + uri;
				// generuj odpowiedź
				String ct = "";
				switch (Path.GetExtension(uri).ToLower())
				{
				case "png":
					ct = "image/png";
					break;
				case "jpg":
				case "jpeg":
				case "jfif":
					ct = "image/jpg";
					break;
				case "txt":
				case "text":
				case "conf":
					ct = "text/plain";
					break;
				case "html":
					ct = "text/html";
					break;
				default:
					sw.WriteLine("HTTP/1.1 503 Service Unavailable");
					break;
				}
				byte [] content = null;
				try {
					content = FileToByteArray(uri);

	
				} catch (FileNotFoundException e)
				{
					sw.WriteLine("HTTP/1.1 404 File Not Found");
					//content length byłby wskazany
					sw.WriteLine("Content-Type: text/html");
					sw.WriteLine();
					sw.WriteLine("<http><body>BUUU</body></html>");
					return;
				}


				sw.WriteLine("HTTP/1.1 200 OK");
				sw.WriteLine("Content-Type: " + ct);
				// wypisz nagłówek długości
				sw.WriteLine("Content-Length: {0}", content.Length);
				// wypisz stronę/body
				sw.WriteLine();
				sw.BaseStream.Write(content, 0, content.Length);
			}
			private void POST(String uri, Dictionary<String, String> headers, byte [] body)
			{
				sw.WriteLine("HTTP/1.1 405  Method Not Allowed");
			}
			private void PUT(String uri, Dictionary<String, String> headers, byte [] body)
			{
				sw.WriteLine("HTTP/1.1 405  Method Not Allowed");
			}
			private void DELETE(String uri, Dictionary<String, String> headers)
			{
				sw.WriteLine("HTTP/1.1 405  Method Not Allowed");
			}

            public void ProcessCommunication()
            {
                

                String command = sr.ReadLine();
				Regex commandProcessor = new Regex("(?<method>GET|POST|PUT|DELETE|HEAD) *(?<uri>[^ ]*) HTTP/(?<httpVersion>1.[012])");
				if (command == null || !commandProcessor.IsMatch(command))
				{
					// brak dopasowania do komendy
					sw.WriteLine("HTTP/1.1 500 INTERNAL SERVER ERROR");
					Disconnect();
					server.RemoveClient(this);
					return;
				}
				Match commandParts = commandProcessor.Match(command);
					
				// dekoduj URL/URI (odzyskaj spaje itp.)
				String uri = HttpUtility.UrlDecode(commandParts.Groups["uri"].Value);
				// commandParts.Groups["method"] // GET/POST/PUT/DELETE/HEAD
				// commandParts.Groups["httpVersion"] //1.0, 1.1, 1.2


				// dekoduj nagłówki do słownika headers
				Dictionary<String,String> headers = new Dictionary<String, String>();
				Regex headerProcessor = new Regex("^(?<name>[^:]*): (?<content>.*)$");
				while (true)
				{
					String line = sr.ReadLine();
					if (line.Length == 0)
						break;
					if (headerProcessor.IsMatch(line))
					{
						Match x = headerProcessor.Match(line);
						headers[x.Groups["name"].Value] = x.Groups["content"].Value;
					}

				}

				//wybierz metodę do obsługi
				switch (commandParts.Groups["method"].ToString())
				{
				case "GET":
				case "HEAD":
					GET(uri, headers);
					break;
				case "DELETE":
					DELETE(uri, headers);
					break;
				case "POST":
					POST(uri, headers, null);
					break;
				case "PUT":
					PUT(uri, headers, null);
					break;
				default:
					throw new Exception("unknown method " + commandParts.Groups["method"]);
				};

						

				sw.Flush();
				// bye bye
                Disconnect();
                server.RemoveClient(this);
            }


            void Disconnect()
            {
                // to nie sprawdza czy strumien jest juz Disposed...
                if (sw != null) sw.Close();
                if (sr != null) sr.Close();
                if (ns != null) ns.Close();
                if (socket != null) socket.Close();
            }
        }


        IPEndPoint ipEndPoint;
        Socket listeningSocket;
        List<ClientHelper> activeClients;

        public ThreadedHTTPServer(IPAddress ipAddress, int ipPort)
        {
            ipEndPoint = new IPEndPoint(ipAddress, ipPort);
            listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            Console.WriteLine("Server created.");
            activeClients = new List<ClientHelper>();
        }

        public void Start()
        {
            listeningSocket.Bind(ipEndPoint);
            listeningSocket.Listen(1);

            Console.WriteLine("Server @ {0} started.", ipEndPoint);
            do
            {
                Console.WriteLine("Server @ {0} waits for a client...", ipEndPoint);
                Socket clientSocket = listeningSocket.Accept(); // czy to moze zglosic wyjatek?

                Console.WriteLine("Server @ {0} client connected @ {1}.", ipEndPoint, clientSocket.RemoteEndPoint);
                Console.WriteLine("Server @ {0} starting client thread.", ipEndPoint, clientSocket.RemoteEndPoint);

                // stworz obiekt obslugujacy polaczenie z klientem
                ClientHelper ch = new ClientHelper(clientSocket, this);
                activeClients.Add(ch);
                // stworz nowy watek, przekaz w ctorze metode, ktora ma byc uruchomiona
                Thread t = new Thread(ch.ProcessCommunication);

                // uruchom watek (przekazana w ctorze metode)
                t.Start();

                // ... i... zapomnij o tym (?)
            }
            while (true);
        }

        void RemoveClient(ClientHelper ch)
        {
            activeClients.Remove(ch);
        }

		public void Stop()
        {
            listeningSocket.Close();
            // a co z ew. klientami?
        }

    } // class
} // namespace
