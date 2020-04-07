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
            string login;

            public string Login { get { return login; } }

            public ClientHelper(Socket socket, ThreadedHTTPServer server)
            {
                this.socket = socket;
                ns = new NetworkStream(this.socket);
                sr = new StreamReader(ns);
                sw = new StreamWriter(ns);
                sw.AutoFlush = true;
                this.server = server;
            }

            public void ProcessCommunication()
            {
                

                String command = sr.ReadLine();
				Regex commandProcessor = new Regex("(?<method>GET|POST|PUT|DELETE|HEAD) *(?<uri>[^ ]*) HTTP/(?<httpVersion>1.[012])");
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
					Match x = headerProcessor.Match(line);
					headers[x.Groups["name"].Value] = x.Groups["content"].Value;

				}

				// generuj odpowiedź
				sw.WriteLine("HTTP/1.1 200 OK");
				sw.WriteLine("Content-Type: text/html");

				// strona z odpowiedzią
				String message = "<html><head><title>Hello World</title><meta charset=\"utf-8\" /></head><body><h1>Hello World!</h1><p>ęółćżó! :)</p></body></html>";
				byte [] bMessage = Encoding.UTF8.GetBytes(message);
				// wypisz nagłówek długości
				sw.WriteLine("Content-Length: {0}", bMessage.Length);
				// wypisz stronę/body
				sw.WriteLine();
				sw.BaseStream.Write(bMessage, 0, bMessage.Length);
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
