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

                    foreach (var channelGroup in configs.GroupBy(config => config.ChannelKey, StringComparer.OrdinalIgnoreCase))
                    {
                        var channelConfigurations = channelGroup
                            .Select(config => (IReadOnlyDictionary<string, string>)config.Values)
                            .ToList();
                        var config = channelGroup.First();
                        var callbacks = new TreeNodeCallbacks(this);
                        var created = dnpPlugin.LoadFromCsvConfiguration(channelConfigurations, callbacks);

                        if (created == null)
                        {
                            MessageBox.Show(this, $"Nie udało się utworzyć kanału {config.ChannelName} ({config.ChannelDescription}).", "Błąd konfiguracji", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            continue;
                        }

                        BindNode(created.Channel, callbacks.node, dnpTreeView.Nodes);
                        foreach (var child in created.Children)
                        {
                            var childCallbacks = new TreeNodeCallbacks(this);
                            BindNode(child, childCallbacks.node, callbacks.node.Nodes);
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

                    var rows = new List<Dictionary<string, string>>();
                    foreach (TreeNode node in treeView.Nodes)
                    {
                        if (node.Tag is not ISimulatorNodeCsvConfiguration channelConfiguration)
                        {
                            continue;
                        }

                        var masters = node.Nodes.Cast<TreeNode>()
                            .Select(child => child.Tag)
                            .OfType<ISimulatorNodeCsvConfiguration>()
                            .Where(configuration => configuration.CsvConfigurationType == "master")
                            .ToList();
                        var outstations = node.Nodes.Cast<TreeNode>()
                            .Select(child => child.Tag)
                            .OfType<ISimulatorNodeCsvConfiguration>()
                            .Where(configuration => configuration.CsvConfigurationType == "outstation")
                            .ToList();

                        var masterConfigurations = masters.Count == 0
                            ? new IReadOnlyDictionary<string, string>[] { new Dictionary<string, string>() }
                            : masters.Select(configuration => configuration.CsvConfiguration).ToArray();
                        var outstationConfigurations = outstations.Count == 0
                            ? new IReadOnlyDictionary<string, string>[] { new Dictionary<string, string>() }
                            : outstations.Select(configuration => configuration.CsvConfiguration).ToArray();

                        foreach (var master in masterConfigurations)
                        {
                            foreach (var outstation in outstationConfigurations)
                            {
                                var row = new Dictionary<string, string>(channelConfiguration.CsvConfiguration, StringComparer.OrdinalIgnoreCase);
                                foreach (var value in master)
                                {
                                    row[value.Key] = value.Value;
                                }
                                foreach (var value in outstation)
                                {
                                    row[value.Key] = value.Value;
                                }
                                rows.Add(row);
                            }
                        }
                    }

                    if (rows.Count == 0)
                    {
                        MessageBox.Show(this, "Nie znaleziono konfiguracji DNP3 do zapisania.", "Brak danych", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    var headers = rows.SelectMany(row => row.Keys)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(GetCsvColumnGroup)
                        .ThenBy(header => header, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    var data = new List<string> { string.Join(",", headers.Select(EscapeCsvField)) };
                    data.AddRange(rows.Select(row => string.Join(",", headers.Select(header => EscapeCsvField(row.TryGetValue(header, out var value) ? value : string.Empty)))));

                    File.WriteAllLines(dialog.FileName, data, Encoding.UTF8);
                    MessageBox.Show(this, "Konfiguracja zapisana do pliku CSV.", "Zapisano", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Błąd zapisu CSV", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static int GetCsvColumnGroup(string header)
        {
            if (header.StartsWith("channel_", StringComparison.OrdinalIgnoreCase)) return 0;
            if (header.StartsWith("master_", StringComparison.OrdinalIgnoreCase)) return 1;
            if (header.StartsWith("outstation_", StringComparison.OrdinalIgnoreCase)) return 2;
            if (header.Equals("slave_address", StringComparison.OrdinalIgnoreCase)) return 2;
            return 3;
        }

        private static string EscapeCsvField(string value)
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
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
            public string ChannelDescription { get; set; } = string.Empty;
            public Dictionary<string, string> Values { get; private set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public string ChannelKey => string.Join("|", Values
                .Where(value => value.Key.StartsWith("channel_", StringComparison.OrdinalIgnoreCase))
                .OrderBy(value => value.Key, StringComparer.OrdinalIgnoreCase)
                .Select(value => $"{value.Key}={value.Value}"));

            public static CsvChannelConfiguration? FromDictionary(Dictionary<string, string> row)
            {
                var host = GetValue(row, "channel_ip", "ip", "host", "server_ip", "channel_host", "remote_ip");
                var channelType = GetValue(row, "channel_type") ?? "TCP Client";
                var isSerial = channelType.Contains("serial", StringComparison.OrdinalIgnoreCase);
                if (!isSerial && string.IsNullOrWhiteSpace(host))
                {
                    return null;
                }

                var portText = GetValue(row, "channel_port", "port", "server_port", "tcp_port");
                if (!isSerial && !ushort.TryParse(portText, out _))
                {
                    return null;
                }

                var channelName = GetValue(row, "channel_name", "channel", "id", "alias", "name") ?? "channel";
                var isLegacy = !row.ContainsKey("channel_type");
                row["channel_name"] = channelName;
                row["channel_type"] = channelType;
                if (isLegacy)
                {
                    row["legacy_csv_format"] = "true";
                }

                return new CsvChannelConfiguration
                {
                    ChannelName = channelName,
                    ChannelDescription = isSerial ? GetValue(row, "channel_serial_device") ?? "Serial" : $"{host}:{portText}",
                    Values = row
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
