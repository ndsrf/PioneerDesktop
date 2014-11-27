using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VSXControl
{
    class Utilities
    {
        private const int _waitTimeForDevicesInMsec = 2000;
        private const int _localEndPointPort = 6000;

        private const string _searchString =
    "M-SEARCH * HTTP/1.1\r\nHOST:239.255.255.250:1900\r\nMAN:\"ssdp:discover\"\r\nST:ssdp:all\r\nMX:3\r\n\r\n";

        public string ReceiverThumbprint { get; set; }


        public IPAddress DiscoverAvReceiver()
        {
            IPAddress discoveredIP = null;
            var tokenSource = new CancellationTokenSource();
            var token = tokenSource.Token;

            string computerName = Environment.GetEnvironmentVariable("COMPUTERNAME");

            if (computerName == null)
            {
                throw new ApplicationException("Unable to retrieve COMPUTERNAME via environment variables.");
            }
            else
            {
                List<Task<IPAddress>> probeNetworkInterfacesList = new List<Task<IPAddress>>();
                foreach (
                    var hostAddress in
                        Dns.GetHostAddresses(computerName).Where(ia => (ia.AddressFamily == AddressFamily.InterNetwork))
                    )
                {
                    probeNetworkInterfacesList.Add(
                        Task.Factory.StartNew(() => GetListOfAvReceivers(hostAddress, token))
                        );

                }

                // Wait for 1 second to get the IP...
                Task.WaitAll(probeNetworkInterfacesList.ToArray(), _waitTimeForDevicesInMsec, token);

                foreach (var task in probeNetworkInterfacesList)
                {
                    if (task.IsCompleted && task.Result != null)
                    {
                        discoveredIP = task.Result;
                        break;
                    }
                }
                // tasks are still running here... let's stop them to make the GC life easier
                tokenSource.Cancel();
            }

            return discoveredIP;
        }

        private IPAddress GetListOfAvReceivers(IPAddress hostAddress, CancellationToken ct)
        {
            IPEndPoint LocalEndPoint = new IPEndPoint(hostAddress, _localEndPointPort);
            IPEndPoint MulticastEndPoint = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);

            Socket UdpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            UdpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            UdpSocket.Bind(LocalEndPoint);
            UdpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                new MulticastOption(MulticastEndPoint.Address, IPAddress.Any));
            UdpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 2);
            UdpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);

            UdpSocket.SendTo(Encoding.UTF8.GetBytes(_searchString), SocketFlags.None, MulticastEndPoint);

            Debug.WriteLine("M-Search sent...\r\n");

            byte[] ReceiveBuffer = new byte[64000];

            int ReceivedBytes = 0;

            while (!ct.IsCancellationRequested)
            {
                if (UdpSocket.Available > 0)
                {
                    ReceivedBytes = UdpSocket.Receive(ReceiveBuffer, SocketFlags.None);

                    if (ReceivedBytes > 0)
                    {
                        string received = Encoding.UTF8.GetString(ReceiveBuffer, 0, ReceivedBytes);

                        Debug.WriteLine(received);

                        if (received.Contains("KnOS/3.2 UPnP/1.0 DMP/3.5"))
                        {
                            return GetReceiverIPFromSSDPResponse(received);
                        }
                    }
                }
                Debug.WriteLine("Waiting for: " + hostAddress.ToString());
            }

            return null;
        }

        private IPAddress GetReceiverIPFromSSDPResponse(string responseSSDP)
        {
            IPAddress theIP;

            Debug.Print("Receiver found!\n" + responseSSDP);
            int index1 = responseSSDP.IndexOf("http://") + 7;
            int index2 = responseSSDP.IndexOf(":8080/");
            string ipString = responseSSDP.Substring(index1, index2 - index1);
            IPAddress.TryParse(ipString, out theIP);

            return theIP;
        }
    }
}
