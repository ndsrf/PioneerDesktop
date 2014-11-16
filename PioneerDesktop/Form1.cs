using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using VSXControl;

namespace PioneerDesktop
{
    public partial class Form1 : Form
    {
        private Boolean mute = false;
        private IAvReceiverControl receiver;

        public IAvReceiverControl Receiver
        {
            get { return receiver; }
            set { receiver = value; }
        }

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            label1.Text = "Searching for AV Receivers...";
            button1.Enabled = false;
            button2.Enabled = false;
            button3.Enabled = false;
            receiver.SetVolumeEvent += (o, s) => this.UIThread(() => lblVolume.Text = s);
        }

        public void SetupStart(IPAddress ipAddressAVReceiver)
        {
            if (ipAddressAVReceiver == null)
            {
                MessageBox.Show("No AV receivers found...");
                button1.Enabled = false;
                button2.Enabled = false;
                button3.Enabled = false;
            }
            else
            {
                label1.Text = "IP Address = " + ipAddressAVReceiver.ToString();
                button1.Enabled = true;
                button2.Enabled = true;
                button3.Enabled = true;
                receiver.Connect(ipAddressAVReceiver);
                receiver.QueryVolume();
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            mute = !mute;
            receiver.MuteUnmute();
            receiver.QueryVolume();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            receiver.VolumeUp();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            receiver.VolumeDown();
        }
    }
}
