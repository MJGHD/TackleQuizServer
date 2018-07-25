using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TackleServer
{
    class Program
    {
        
        static void Main()
        {
            TcpListener server = new TcpListener(IPAddress.Any, 8888);
            server.Start();

            //Loop that constantly checks whether there's an incoming request
            Console.WriteLine("Client checking loop started");
            while (true)
            {
                Console.WriteLine("test");
                if (server.Pending())
                {
                    Socket client = server.AcceptSocket();
                    Console.WriteLine("Client handling thread started");

                    Thread clientReceiveThread = new Thread(() =>
                    {
                        byte[] message = new byte[100];
                        client.Receive(message);
                        string messageStr = Encoding.Default.GetString(message);
                        client.Close();
                    });

                    clientReceiveThread.Start();
                }
                Thread.Sleep(300);
            }
        }
    }
}
