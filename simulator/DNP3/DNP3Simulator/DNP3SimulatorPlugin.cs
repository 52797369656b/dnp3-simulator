using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Automatak.DNP3.Adapter;
using Automatak.DNP3.Interface;

using Automatak.Simulator.Commons;
using Automatak.Simulator.API;
using Automatak.Simulator.DNP3.API;
using Automatak.Simulator.DNP3.Commons;

namespace Automatak.Simulator.DNP3
{
    static class IconIndex
    {
        public const int Channel = 0;                
        public const int Master = 1;
        public const int Outstation = 2;
    };


    class DNP3SimulatorPlugin : ISimulatorPlugin, ISimulatorPluginCsvLoader
    {
        readonly ImageList imgList = new ImageList();
        readonly ILogHandler logHandler;
        readonly IDNP3Manager manager;
        
        readonly DNP3Config config = new DNP3Config(
           new IOutstationModule[]{ 
               Automatak.Simulator.DNP3.DefaultOutstationPlugin.OutstationModule.Instance,
               Automatak.Simulator.DNP3.RelayOutstationPlugin.OutstationModule.Instance
           }
        );

        public DNP3SimulatorPlugin(ILog log)
        {
            this.logHandler = new ForwardingLogHandler(log);

            this.manager = DNP3ManagerFactory.CreateManager(this.logHandler);

            imgList.Images.Add(Properties.Resources.satellite_dish);            
            imgList.Images.Add(Properties.Resources.network_monitor);
            imgList.Images.Add(Properties.Resources.send_container);

            /*
                Load outstation plugins here 
            */
        }

        string ISimulatorPlugin.UniqueId
        {
            get { return "DNP3"; }
        }

        System.Drawing.Image ISimulatorPlugin.PluginImage
        {
            get { return Properties.Resources.satellite_dish_add; }
        }


        string ISimulatorPlugin.RootDisplayName
        {
            get { return "DNP3 Channel"; }
        }


        PluginCsvLoadResult? ISimulatorPluginCsvLoader.LoadFromCsvConfiguration(string alias, string host, ushort port, ushort masterAddress, ushort slaveAddress, ISimulatorNodeCallbacks callbacks)
        {
            var retry = new ChannelRetry(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1));
            var endpoint = new IPEndpoint(host, port);
            var channel = manager.AddTCPClient(alias, 0u, retry, new List<IPEndpoint> { endpoint }, ChannelListener.None());

            if (channel == null)
            {
                return null;
            }

            var channelNode = new ChannelNode(config, channel, callbacks, alias, new Dictionary<string, string>
            {
                ["channel_name"] = alias,
                ["channel_type"] = "TCP Client",
                ["channel_ip"] = host,
                ["channel_port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["channel_retry_min_ms"] = "1000",
                ["channel_retry_max_ms"] = "5000"
            });

            var masterAlias = alias + "-master";
            var masterCache = new MeasurementCache();

            var masterStack = new MasterStackConfig();
            masterStack.link = new LinkConfig(true)
            {
                localAddr = masterAddress,
                remoteAddr = slaveAddress,
                responseTimeout = TimeSpan.FromSeconds(5),
                keepAliveTimeout = TimeSpan.FromSeconds(30)
            };
            masterStack.master = new MasterConfig();

            var master = channel.AddMaster(masterAlias, masterCache, DefaultMasterApplication.Instance, masterStack);
            ISimulatorNode? masterNode = null;
            if (master != null)
            {
                master.Enable();
                masterNode = new MasterNode(masterCache, master, callbacks, masterAlias, new Dictionary<string, string>
                {
                    ["master_name"] = masterAlias,
                    ["master_address"] = masterAddress.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["slave_address"] = slaveAddress.ToString(System.Globalization.CultureInfo.InvariantCulture)
                });
            }

            var outstationModule = config.OutstationModules.FirstOrDefault();
            ISimulatorNode? outstationNode = null;
            if (outstationModule != null)
            {
                var factory = outstationModule.CreateFactory();
                var outstationConfig = outstationModule.DefaultConfig;
                outstationConfig.link = new LinkConfig(false)
                {
                    localAddr = slaveAddress,
                    remoteAddr = masterAddress,
                    responseTimeout = TimeSpan.FromSeconds(5),
                    keepAliveTimeout = TimeSpan.FromSeconds(30)
                };

                var outstation = channel.AddOutstation(alias + "-slave", factory.CommandHandler, factory.Application, outstationConfig);
                if (outstation != null)
                {
                    var instance = factory.CreateInstance(outstation, alias + "-slave", outstationConfig);
                    outstation.Enable();
                    if (instance.ShowFormOnCreation)
                    {
                        instance.ShowForm();
                    }
                    outstationNode = new OutstationNode(outstation, instance, callbacks, new Dictionary<string, string>
                    {
                        ["outstation_name"] = alias + "-slave",
                        ["master_address"] = masterAddress.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["slave_address"] = slaveAddress.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    });
                }
            }

            return new PluginCsvLoadResult(channelNode, masterNode, outstationNode);
        }

        ISimulatorNode? ISimulatorPlugin.Create(ISimulatorNodeCallbacks callbacks)
        {
            using (var dialog = new Components.ChannelDialog())
            {
                dialog.ShowDialog();
                if (dialog.DialogResult == DialogResult.OK)
                {
                    var channel = dialog.ChannelAction!.Invoke(manager);
                    return new ChannelNode(config, channel, callbacks, dialog.SelectedAlias, dialog.CsvConfiguration);
                }
                else
                {
                    return null;    
                }
            }            
       }

       System.Windows.Forms.ImageList ISimulatorPlugin.NodeImageList
       {
           get { return imgList; }
       }
    }
}
