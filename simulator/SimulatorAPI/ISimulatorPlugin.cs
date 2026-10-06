#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.Drawing;

namespace Automatak.Simulator.API
{
    public sealed class PluginCsvLoadResult
    {
        public PluginCsvLoadResult(ISimulatorNode channel, ISimulatorNode? master, ISimulatorNode? outstation)
        {
            Channel = channel;
            Master = master;
            Outstation = outstation;
        }

        public ISimulatorNode Channel { get; }
        public ISimulatorNode? Master { get; }
        public ISimulatorNode? Outstation { get; }
    }

    public interface ISimulatorPluginCsvLoader
    {
        PluginCsvLoadResult? LoadFromCsvConfiguration(string alias, string host, ushort port, ushort masterAddress, ushort slaveAddress, ISimulatorNodeCallbacks callbacks);
    }

    public interface ISimulatorPlugin
    {
        Image PluginImage
        {
            get;
        }

        ImageList NodeImageList
        {
            get;
        }

        String RootDisplayName
        {
            get;
        }

        String UniqueId
        {
            get;
        }

        ISimulatorNode? Create(ISimulatorNodeCallbacks callbacks);
    }
}
