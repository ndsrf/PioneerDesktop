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
        public event EventHandler<bool> SetOnOffEvent;

        private readonly ManualResetEvent connectDone = new ManualResetEvent(false);
        private readonly ManualResetEvent sendDone = new ManualResetEvent(false);
        private readonly ManualResetEvent receiveDone = new ManualResetEvent(false);
        private Socket _sck;
        private int _port = 23;
        private Utilities _utilities;

        public VSX1123()
        {
            _utilities = new Utilities();
            _utilities.ReceiverThumbprint = "";

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
                if (response.StartsWith("PWR"))
                {
                    if (response.StartsWith("PWR0"))
                    {
                        SetOnOffEvent(this, false);
                    }
                    if (response.StartsWith("PWR1"))
                    {
                        SetOnOffEvent(this, false);
                    }
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




        public void QueryOnOff()
        {
            SendMessage("?P");

        }

        public void TurnOn()
        {
            SendMessage("PO");
        }

        public void TurnOff()
        {
            SendMessage("PF");
        }


        public IPAddress DiscoverAvReceiver()
        {
            return _utilities.DiscoverAvReceiver();
        }
    }
}
