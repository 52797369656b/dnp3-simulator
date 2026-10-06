using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualBasic.FileIO;

using Automatak.Simulator.UI;
using Automatak.Simulator.Commons;
using Automatak.Simulator.API;

namespace Automatak.Simulator
{
    public partial class SimulatorForm : Form
    {
        readonly IEnumerable<ISimulatorPluginFactory> plugins;
        readonly LogToFile fileLogger;
        readonly ILog log;
        readonly List<ISimulatorPlugin> pluginInstances = new List<ISimulatorPlugin>();
        readonly Dictionary<ISimulatorPlugin, TreeView> pluginTrees = new Dictionary<ISimulatorPlugin, TreeView>();


        public SimulatorForm(IEnumerable<ISimulatorPluginFactory> plugins)
        {                 
            InitializeComponent();    
            
            this.plugins = plugins;
            this.fileLogger = new LogToFile();
            this.log = new LogMultiplexer(this.logWindow1, fileLogger);

            this.logFileControl.FileLogger = fileLogger;

            var toolTipUserNote = new ToolTip();
            toolTipUserNote.SetToolTip(this.buttonMakeNote, "Make a user-defined note in the log");
        }
           
        void ShowAboutBox()
        {
            using (var about = new AboutBox())
            {                
                about.ShowDialog();
            }            
        }

        void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowAboutBox();  
        }

        private void helpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var helpFilePath = Path.Combine(AppContext.BaseDirectory, "Pomoc.txt");
            if (!File.Exists(helpFilePath))
            {
                MessageBox.Show(this, "Nie znaleziono pliku Pomoc.txt.", "Pomoc", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = helpFilePath,
                    UseShellExecute = true
                });
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, $"Nie można otworzyć pliku pomocy: {exception.Message}", "Pomoc", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BindNode(ISimulatorNode simNode, TreeNode node, TreeNodeCollection parent)
        {
            node.Text = simNode.DisplayName;
            node.Tag = simNode;

            var menu = new ContextMenuStrip();

            foreach (var nodeAction in simNode.Actions)
            {
                var action = new ToolStripMenuItem(nodeAction.DisplayName);
                action.Click += new EventHandler(
                    delegate(Object? o, EventArgs a)
                    {
                        nodeAction.Invoke();
                    }
                );
                menu.Items.Add(action);
            }

            if (simNode.Actions.Any())
            {
                menu.Items.Add(new ToolStripSeparator());
            }
            
            foreach (var factory in simNode.Children)
            {
                var action = new ToolStripMenuItem(factory.DisplayName);
                action.Click += new EventHandler(
                    delegate(Object? o, EventArgs a)
                    {
                        var callbacks = new TreeNodeCallbacks(this);
                        var child = factory.Create(callbacks);
                        if (child != null)
                        {
                            this.BindNode(child, callbacks.node, node.Nodes);
                        }
                    }
                );
                menu.Items.Add(action);
            }

            if (simNode.Children.Any())
            {
                menu.Items.Add(new ToolStripSeparator());
            }

            var item = new ToolStripMenuItem("Remove");
            item.Click += new EventHandler(
                delegate(Object? o, EventArgs a)
                {
                    ShutdownFrom(node);
                    parent.Remove(node);
                }
            );
           
            menu.Items.Add(item);
            node.ContextMenuStrip = menu;
            parent.Add(node);
        }

        private static void ShutdownFrom(TreeNode node)
        {
            if (node.Tag is not ISimulatorNode simNode)
            {
                return;
            }

            foreach(TreeNode subnode in node.Nodes)
            {
                ShutdownFrom(subnode);
            }
            simNode.Remove();
        }
        
        private void SimulatorForm_Load(object sender, EventArgs e)
        {                        
            foreach (var factory in plugins)
            {                
                var instance = factory.Create(this.log);
                this.pluginInstances.Add(instance);
                var item = new ToolStripMenuItem(instance.RootDisplayName);
                item.Image = instance.PluginImage;
                this.addToolStripMenuItem.DropDownItems.Add(item);
                var page = new TabPage(instance.UniqueId);
                var treeView = new TreeView();
                page.Tag = treeView;
                treeView.Dock = DockStyle.Fill;
                treeView.ImageList = instance.NodeImageList;
                page.Controls.Add(treeView);
                this.tabControlPlugins.TabPages.Add(page);
                this.pluginTrees[instance] = treeView;
                
                item.Click += new EventHandler(
                    delegate(Object? o, EventArgs a)
                    {                        
                        var callbacks = new TreeNodeCallbacks(this);
                        var node = instance.Create(callbacks);
                        if (node != null)
                        {
                            BindNode(node, callbacks.node, treeView.Nodes);
                        }
                    }
                );

                this.log.LogFull(DisplayHint.INFO, "INFO", "system", "Initialized " + instance.UniqueId + " plugin");
            }
        }

        private IEnumerable<Metric> GetMetrics()
        {
            var tab = this.tabControlPlugins.SelectedTab;
            if (tab == null)
            {
                return Enumerable.Empty<Metric>();
            }
            else
            {
                if (tab.Tag is not TreeView view)
                {
                    return Enumerable.Empty<Metric>();
                }

                var node = view.SelectedNode;
                if (node == null)
                {
                    return Enumerable.Empty<Metric>();
                }
                else
                {
                    return node.Tag is ISimulatorNode simNode
                        ? simNode.Metrics
                        : Enumerable.Empty<Metric>();
                }
            }
        }

        private void timerMetrics_Tick(object sender, EventArgs e)
        {
            var metrics = GetMetrics();
            this.listViewMetrics.SuspendLayout();
            this.listViewMetrics.Items.Clear();
            foreach (Metric m in metrics)
            {
                var values = new String[] { m.Id, m.Value };
                var item = new ListViewItem(values);
                this.listViewMetrics.Items.Add(item);
            }
            this.listViewMetrics.ResumeLayout();
        }

        private void SimulatorForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            fileLogger.Shutdown();
        }

        private void buttonMakeNote_Click(object sender, EventArgs e)
        {
            using (var dialog = new MakeNoteDialog())
            {
                dialog.ShowDialog();
                if (dialog.DialogResult == DialogResult.OK)
                {
                    var lines = dialog.SelectedLines;
                    var first = lines.Take(1);
                    var remainder = lines.Skip(1);
                    
                    foreach (var line in first)
                    {
                        log.LogFull(DisplayHint.ALT2, "USER", "system", line);
                    }

                    foreach (var line in remainder)
                    {
                        log.Log(DisplayHint.ALT2, line);
                    }
                }
            }
        }

        private void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    var configs = LoadCsvConfigurations(dialog.FileName);
                    if (configs.Count == 0)
                    {
                        MessageBox.Show(this, "Brak prawidłowych wpisów w pliku CSV.", "Błąd ładowania", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    var dnpPlugin = this.pluginInstances
                        .OfType<ISimulatorPluginCsvLoader>()
                        .FirstOrDefault();

                    if (dnpPlugin == null)
                    {
                        MessageBox.Show(this, "Nie znaleziono wtyczki DNP3 z obsługą CSV.", "Błąd ładowania", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var dnpTreeView = this.pluginTrees
                        .FirstOrDefault(kvp => kvp.Key is ISimulatorPluginCsvLoader)
                        .Value;

                    if (dnpTreeView == null)
                    {
                        MessageBox.Show(this, "Nie znaleziono widoku drzewa DNP3.", "Błąd ładowania", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    foreach (var config in configs)
                    {
                        var callbacks = new TreeNodeCallbacks(this);
                        var created = dnpPlugin.LoadFromCsvConfiguration(
                            string.IsNullOrWhiteSpace(config.ChannelName) ? "channel" : config.ChannelName,
                            config.Host,
                            config.Port,
                            config.MasterAddress,
                            config.SlaveAddress,
                            callbacks);

                        if (created == null)
                        {
                            MessageBox.Show(this, $"Nie udało się utworzyć kanału dla {config.Host}:{config.Port}", "Błąd konfiguacji", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            continue;
                        }

                        BindNode(created.Channel, callbacks.node, dnpTreeView.Nodes);

                        if (created.Master != null)
                        {
                            var masterCallbacks = new TreeNodeCallbacks(this);
                            BindNode(created.Master, masterCallbacks.node, callbacks.node.Nodes);
                        }

                        if (created.Outstation != null)
                        {
                            var outstationCallbacks = new TreeNodeCallbacks(this);
                            BindNode(created.Outstation, outstationCallbacks.node, callbacks.node.Nodes);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Błąd ładowania CSV", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                dialog.DefaultExt = "csv";
                dialog.AddExtension = true;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    var treeView = this.tabControlPlugins.SelectedTab?.Tag as TreeView;
                    if (treeView == null)
                    {
                        MessageBox.Show(this, "Najpierw wybierz zakładkę z kanałem DNP3.", "Brak danych", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    var data = new List<string>();
                    data.Add("channel_name,channel_ip,channel_port,master_address,slave_address");

                    foreach (TreeNode node in treeView.Nodes)
                    {
                        if (node.Tag is not ISimulatorNode simNode)
                        {
                            continue;
                        }

                        data.Add($"{simNode.DisplayName},127.0.0.1,20000,10,20");
                    }

                    File.WriteAllLines(dialog.FileName, data);
                    MessageBox.Show(this, "Konfiguracja zapisana do pliku CSV.", "Zapisano", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Błąd zapisu CSV", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static List<CsvChannelConfiguration> LoadCsvConfigurations(string path)
        {
            var results = new List<CsvChannelConfiguration>();

            using (var parser = new TextFieldParser(path))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                parser.HasFieldsEnclosedInQuotes = true;

                string[]? headers = null;
                while (!parser.EndOfData)
                {
                    var fields = parser.ReadFields();
                    if (fields == null || fields.Length == 0 || fields.All(string.IsNullOrWhiteSpace))
                    {
                        continue;
                    }

                    var cleaned = fields.Select(f => f.Trim()).ToArray();
                    if (headers == null)
                    {
                        headers = cleaned;
                        continue;
                    }

                    var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < Math.Min(headers.Length, cleaned.Length); i++)
                    {
                        row[headers[i]] = cleaned[i];
                    }

                    var config = CsvChannelConfiguration.FromDictionary(row);
                    if (config != null)
                    {
                        results.Add(config);
                    }
                }
            }

            return results;
        }

        private sealed class CsvChannelConfiguration
        {
            public string ChannelName { get; set; } = "channel";
            public string Host { get; set; } = "127.0.0.1";
            public ushort Port { get; set; } = 20000;
            public ushort MasterAddress { get; set; } = 10;
            public ushort SlaveAddress { get; set; } = 20;

            public static CsvChannelConfiguration? FromDictionary(Dictionary<string, string> row)
            {
                var host = GetValue(row, "channel_ip", "ip", "host", "server_ip", "channel_host", "remote_ip");
                if (string.IsNullOrWhiteSpace(host))
                {
                    return null;
                }

                var portText = GetValue(row, "channel_port", "port", "server_port", "tcp_port");
                if (!ushort.TryParse(portText, out var port))
                {
                    return null;
                }

                var masterText = GetValue(row, "master_address", "master_addr", "master", "dnp3_master_address");
                var slaveText = GetValue(row, "slave_address", "slave_addr", "slave", "dnp3_slave_address");

                if (!ushort.TryParse(masterText, out var masterAddress) || !ushort.TryParse(slaveText, out var slaveAddress))
                {
                    return null;
                }

                return new CsvChannelConfiguration
                {
                    ChannelName = GetValue(row, "channel_name", "channel", "id", "alias", "name") ?? "channel",
                    Host = host,
                    Port = port,
                    MasterAddress = masterAddress,
                    SlaveAddress = slaveAddress
                };
            }

            private static string? GetValue(Dictionary<string, string> row, params string[] keys)
            {
                foreach (var key in keys)
                {
                    if (row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }

                return null;
            }
        }

    }
}
