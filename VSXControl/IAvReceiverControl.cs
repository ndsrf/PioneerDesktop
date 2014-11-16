using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace VSXControl
{
    public interface IAvReceiverControl
    {
        event EventHandler<string> SetVolumeEvent;
        IPAddress DiscoverAvReceiver();
        int VolumeUp();
        int VolumeDown();
        int QueryVolume();
        bool MuteUnmute();
        void Connect(IPAddress ip);
        void Disconnect();

    }
}
