/*
 * Telemedycyna i Technologie Sieciowe
 * Laboratorium. Gniazda sieciowe cz.2: Wielowatkowa obsluga polaczen
 * Klasa serwera echo (jednowatkowy)
 * v.0.1.a, 2018-03-12, Marcin.Rudzki@polsl.pl 
 */

using System;
using System.Collections.Generic;
//using System.Linq;
using System.Text;

using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace TCPSockets2
{
    class EchoServer : IServer
    {
        IPEndPoint ipEndPoint;
        Socket listeningSocket;

        public EchoServer(IPAddress ipAddress, int ipPort)
        {
            ipEndPoint = new IPEndPoint(ipAddress, ipPort);
            listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            Console.WriteLine("Server created.");
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

                // TODO: zmodyfikuj tak, aby serwer obslugiwal wielu klientow jednoczesnie
                //ProcessCommunication((object)clientSocket);
                Thread t = new Thread(ProcessCommunication);
                t.Start((object)clientSocket);
            }
            while (true);
        }

        public void Stop()
        {
            listeningSocket.Close();
        }

        // metoda obslugujaca polaczenie z jednym klientem
        public void ProcessCommunication(object socketAsObject)
        {
            NetworkStream ns; // strumien sieciowy "na gniezdzie"
            StreamReader sr;  // strumien do odbierania danych "na s.sieciowym"
            StreamWriter sw;  // strumien do wysylania danych "na s.sieciowym"

            Socket socket = (Socket)socketAsObject;
            ns = new NetworkStream(socket);
            sr = new StreamReader(ns);
            sw = new StreamWriter(ns);
            sw.AutoFlush = true;
            string message;

            // czy ktores operacje tu wykonywane moga spowodowac wyjatek?
            sw.WriteLine("Server says: Hi!");
            //sw.Flush();
            do
            {
                // czekaj na komunikat
                message = sr.ReadLine();
                Console.WriteLine(string.Format("Client @ {0} says: {1}", socket.RemoteEndPoint, message));

                // wyslij odpowiedz
                sw.WriteLine(string.Format("OK. Got [{0}] Thanks!", message));
                //sw.Flush();
            }
            while (message != "QUIT!");

            if (sw != null) sw.Close();
            if (sr != null) sr.Close();
            if (ns != null) ns.Close();
            if (socket != null) socket.Close();
        }

    } // class
} // namespace
