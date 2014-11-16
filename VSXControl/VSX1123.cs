using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VSXControl
{
    public class VSX1123 : IAvReceiverControl
    {
        public event EventHandler<string> SetVolumeEvent;

        private readonly ManualResetEvent connectDone = new ManualResetEvent(false);
        private readonly ManualResetEvent sendDone = new ManualResetEvent(false);
        private readonly ManualResetEvent receiveDone = new ManualResetEvent(false);
        private Socket _sck;
        private int _port = 23;

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
                Task.WaitAll(probeNetworkInterfacesList.ToArray(), 2000, token);
                
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
            IPEndPoint LocalEndPoint = new IPEndPoint(hostAddress, 60000);
            IPEndPoint MulticastEndPoint = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);

            Socket UdpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            UdpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            UdpSocket.Bind(LocalEndPoint);
            UdpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                new MulticastOption(MulticastEndPoint.Address, IPAddress.Any));
            UdpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 2);
            UdpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);

            string SearchString =
                "M-SEARCH * HTTP/1.1\r\nHOST:239.255.255.250:1900\r\nMAN:\"ssdp:discover\"\r\nST:ssdp:all\r\nMX:3\r\n\r\n";

            UdpSocket.SendTo(Encoding.UTF8.GetBytes(SearchString), SocketFlags.None, MulticastEndPoint);

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

        public void Connect(IPAddress ip)
        {
            _sck = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint endPoint = new IPEndPoint(ip, _port);
            Connect(endPoint, _sck);
            Receive();
        }

        private void Connect(EndPoint remoteEP, Socket client)
        {
            client.BeginConnect(remoteEP,
                new AsyncCallback(ConnectCallback), client);

            connectDone.WaitOne();
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            // Retrieve the socket from the state object.
            Socket client = (Socket)ar.AsyncState;

            // Complete the connection.
            client.EndConnect(ar);

            Debug.WriteLine("Socket connected to {0}",
                client.RemoteEndPoint.ToString());

            // Signal that the connection has been made.
            connectDone.Set();
        }

        public void Disconnect()
        {
            if (_sck.Connected)
            {
                _sck.Disconnect(true);
            }
        }

        //private async void ReceiveMessages()
        //{
        //    //buffer = new byte[255];
        //    //StringBuilder sbStringBuilder = new StringBuilder();
        //    //while (_sck.Receive(buffer) > 0)    
        //    //{
        //    //    sbStringBuilder.Append(Encoding.Default.GetString(buffer));
        //    //}
        //    //return sbStringBuilder.ToString();
            
        //}

        private void Receive()
        {
            // Create the state object.
            StateObject state = new StateObject();
            state.workSocket = _sck;

            // Begin receiving the data from the remote device.
            _sck.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReceiveCallback), state);
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            // Retrieve the state object and the client socket 
            // from the asynchronous state object.
            StateObject state = (StateObject)ar.AsyncState;
            Socket client = state.workSocket;
            // Read data from the remote device.
            int bytesRead = client.EndReceive(ar);
            if (bytesRead > 0)
            {
                string response = Encoding.ASCII.GetString(state.buffer, 0, bytesRead);
                if (response.StartsWith("VOL") || response.StartsWith("M"))
                {
                    SetVolumeEvent(this, response);
                }
                // There might be more data, so store the data received so far.
                state.sb.Append(response);
                //  Get the rest of the data.
                client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReceiveCallback), state);
            }
            else
            {
                // All the data has arrived; put it in response.
                if (state.sb.Length > 1)
                {
                    string response = state.sb.ToString();
                    SetVolumeEvent(this, response);
                }
                // Signal that all bytes have been received.
                receiveDone.Set();
            }
        }

        private void SendMessage(string message)
        {
            byte[] buffer = Encoding.Default.GetBytes(message + "\r\n");

            _sck.BeginSend(buffer, 0, buffer.Length, SocketFlags.None,
        new AsyncCallback(SendCallback), _sck);
            Receive();
        }

        private void SendCallback(IAsyncResult ar)
        {
            // Retrieve the socket from the state object.
            Socket client = (Socket)ar.AsyncState;

            // Complete sending the data to the remote device.
            int bytesSent = client.EndSend(ar);
            Debug.WriteLine("Sent {0} bytes to server.", bytesSent);

            // Signal that all bytes have been sent.
            sendDone.Set();
        }


        public int VolumeUp()
        {
            SendMessage("VU");
            return 0;
        }

        public int VolumeDown()
        {
            SendMessage("VD");
            return 0;
        }

        public int QueryVolume()
        {
            SendMessage("?V");
            return 0;
        }

        public bool MuteUnmute()
        {
            SendMessage("MZ");
            return true;
        }


    }
}
