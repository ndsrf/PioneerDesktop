using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using VSXControl;

namespace PioneerDesktop
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            IAvReceiverControl receiver = new VSX1123();

            Form1 mainForm = new Form1();
            mainForm.Receiver = receiver;

            mainForm.Show();

            IPAddress ipAddressAVReceiver = receiver.DiscoverAvReceiver();

            mainForm.SetupStart(ipAddressAVReceiver);

            Application.Run(mainForm);

        }
    }
}
