using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Automatak.DNP3.Interface;
using Automatak.Simulator.DNP3.API;
using Automatak.Simulator.DNP3.Commons;
using Automatak.Simulator.API;

namespace Automatak.Simulator.DNP3
{
    class OutstationNode : ISimulatorNode, ISimulatorNodeCsvConfiguration
    {
        readonly IOutstation outstation;
        readonly IOutstationInstance instance;                     
        readonly ISimulatorNodeCallbacks callbacks;        
        readonly ISimulatorNodeAction openAction;       
        readonly IReadOnlyDictionary<string, string> csvConfiguration;
                
        string ISimulatorNode.Alias
        {
            get
            {
                return instance.DisplayName;
            }
        }

        public OutstationNode(IOutstation outstation, IOutstationInstance instance, ISimulatorNodeCallbacks callbacks, IReadOnlyDictionary<string, string>? csvConfiguration = null)
        {            
            this.outstation = outstation;
            this.instance = instance;
            this.callbacks = callbacks;                     
            this.csvConfiguration = csvConfiguration ?? new Dictionary<string, string>();

            this.openAction = new NodeAction("Open", () => OpenForm());

            this.callbacks.ChangeImage(IconIndex.Outstation);
        }

        string ISimulatorNodeCsvConfiguration.CsvConfigurationType => "outstation";

        IReadOnlyDictionary<string, string> ISimulatorNodeCsvConfiguration.CsvConfiguration => csvConfiguration;

        void OpenForm()
        {
            this.instance.ShowForm();
        }
        
        void ISimulatorNode.Remove()
        {
            this.instance.Shutdown();
            this.outstation.Shutdown();            
        }

        IEnumerable<Metric> ISimulatorNode.Metrics
        {
            get
            {
                var list = new List<Metric>();
                var stats = outstation.GetStackStatistics();
                list.Add(new Metric("Num transport rx", stats.NumTransportRx.ToString()));
                list.Add(new Metric("Num transport tx", stats.NumTransportTx.ToString()));
                list.Add(new Metric("Num transport error rx", stats.NumTransportErrorRx.ToString()));
                return list;
            }
        }

        string ISimulatorNode.DisplayName
        {
            get { return instance.DisplayName; }
        }

        IEnumerable<ISimulatorNodeAction> ISimulatorNode.Actions
        {
            get
            {
                yield return openAction;
            }
        }

        IEnumerable<ISimulatorNodeFactory> ISimulatorNode.Children
        {
            get { return Enumerable.Empty<ISimulatorNodeFactory>(); }
        }       
    }
}
