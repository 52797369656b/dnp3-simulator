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
        public PluginCsvLoadResult(ISimulatorNode channel, IEnumerable<ISimulatorNode> children)
        {
            Channel = channel;
            Children = children.ToList();
        }

        public ISimulatorNode Channel { get; }
        public IReadOnlyList<ISimulatorNode> Children { get; }
    }

    public interface ISimulatorPluginCsvLoader
    {
        PluginCsvLoadResult? LoadFromCsvConfiguration(IReadOnlyList<IReadOnlyDictionary<string, string>> configurations, ISimulatorNodeCallbacks callbacks);
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
