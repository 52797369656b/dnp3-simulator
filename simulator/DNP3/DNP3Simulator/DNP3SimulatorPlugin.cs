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


        PluginCsvLoadResult? ISimulatorPluginCsvLoader.LoadFromCsvConfiguration(IReadOnlyList<IReadOnlyDictionary<string, string>> configurations, ISimulatorNodeCallbacks callbacks)
        {
            if (configurations.Count == 0)
            {
                return null;
            }

            var firstConfiguration = configurations[0];
            using var channelDialog = new Components.ChannelDialog();
            channelDialog.RestoreCsvConfiguration(firstConfiguration);
            var channelFactory = channelDialog.ChannelAction;
            if (channelFactory == null)
            {
                return null;
            }

            var alias = channelDialog.SelectedAlias;
            var channel = channelFactory(manager);

            if (channel == null)
            {
                return null;
            }

            var channelNode = new ChannelNode(config, channel, callbacks, alias, channelDialog.CsvConfiguration);
            var children = new List<ISimulatorNode>();
            var legacyFormat = HasValue(firstConfiguration, "legacy_csv_format");

            var masterRows = configurations
                .Where(row => legacyFormat || HasValue(row, "master_name"))
                .GroupBy(row => GetValue(row, "master_name") ?? "legacy-master", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First());

            foreach (var row in masterRows)
            {
                using var masterDialog = new Components.MasterDialog();
                masterDialog.RestoreCsvConfiguration(row);
                var masterAlias = string.IsNullOrWhiteSpace(masterDialog.SelectedAlias) ? alias + "-master" : masterDialog.SelectedAlias;
                var masterCache = new MeasurementCache();
                var master = channel.AddMaster(masterAlias, masterCache, DefaultMasterApplication.Instance, masterDialog.Configuration);
                if (master != null)
                {
                    master.Enable();
                    children.Add(new MasterNode(masterCache, master, callbacks, masterAlias, masterDialog.CsvConfiguration));
                }
            }

            var outstationRows = configurations
                .Where(row => legacyFormat || HasValue(row, "outstation_name"))
                .GroupBy(row => GetValue(row, "outstation_name") ?? "legacy-outstation", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First());

            foreach (var row in outstationRows)
            {
                var moduleName = GetValue(row, "outstation_module");
                var outstationModule = config.OutstationModules.FirstOrDefault(module =>
                    string.IsNullOrWhiteSpace(moduleName) || string.Equals(module.Name, moduleName, StringComparison.OrdinalIgnoreCase));
                if (outstationModule == null)
                {
                    continue;
                }

                var templateName = GetValue(row, "outstation_template");
                var databaseTemplate = Components.CsvConfigurationSnapshot.RestoreObject<DatabaseTemplate>("outstation_database_template", row);
                if (!string.IsNullOrWhiteSpace(templateName))
                {
                    ((IDNP3Config)config).AddTemplate(templateName, databaseTemplate);
                }

                using var outstationDialog = new Components.OutstationDialog(config, outstationModule);
                outstationDialog.RestoreCsvConfiguration(row);
                var outstationAlias = string.IsNullOrWhiteSpace(outstationDialog.SelectedAlias) ? alias + "-slave" : outstationDialog.SelectedAlias;
                var outstationConfig = outstationDialog.Configuration;
                if (row.Keys.Any(key => key.StartsWith("outstation_database_template_", StringComparison.OrdinalIgnoreCase)))
                {
                    outstationConfig.databaseTemplate = databaseTemplate;
                }
                var factory = outstationModule.CreateFactory();
                var outstation = channel.AddOutstation(outstationAlias, factory.CommandHandler, factory.Application, outstationConfig);
                if (outstation != null)
                {
                    var instance = factory.CreateInstance(outstation, outstationAlias, outstationConfig);
                    outstation.Enable();
                    if (instance.ShowFormOnCreation)
                    {
                        instance.ShowForm();
                    }
                    children.Add(new OutstationNode(outstation, instance, callbacks, outstationDialog.CsvConfiguration));
                }
            }

            return new PluginCsvLoadResult(channelNode, children);
        }

        private static bool HasValue(IReadOnlyDictionary<string, string> row, string key)
        {
            return row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value);
        }

        private static string? GetValue(IReadOnlyDictionary<string, string> row, string key)
        {
            return row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
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
