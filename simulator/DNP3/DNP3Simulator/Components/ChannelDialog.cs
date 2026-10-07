using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;


using Automatak.DNP3.Interface;
using System.IO;

namespace Automatak.Simulator.DNP3.Components
{
    public partial class ChannelDialog : Form
    {
        public ChannelDialog()
        {
            InitializeComponent();            
            this.comboBoxSerialDeviceName.Items.AddRange(System.IO.Ports.SerialPort.GetPortNames());
            this.comboBoxParity.DataSource = Enum.GetValues(typeof(Parity));
            this.comboBoxStopBits.DataSource = Enum.GetValues(typeof(StopBits));
            this.comboBoxFlowControl.DataSource = Enum.GetValues(typeof(FlowControl));

            this.comboBoxParity.SelectedItem = Parity.None;
            this.comboBoxStopBits.SelectedItem = StopBits.One;
            this.comboBoxFlowControl.SelectedItem = FlowControl.None;           
        }

        private void buttonADD_Click(object sender, EventArgs e)
        {
            try
            {
                create = GetCreateFunctorMaybeNull();
                if (create == null)
                {
                    toolStripStatusLabel1.Text = "Unable to create channel";
                }
                else
                {
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error creating channel");
            }
        }

        private Func<IDNP3Manager, IChannel>? GetCreateFunctorMaybeNull()
        {
            var min = TimeSpan.FromMilliseconds(Decimal.ToDouble(numericUpDownMinRetryMS.Value));
            var max = TimeSpan.FromMilliseconds(Decimal.ToDouble(numericUpDownMaxRetryMS.Value));

            switch (tabControlChannelType.SelectedIndex)
            { 
                case(0):                                        
                    return GetTCPClientFunctor(min, max, clientEnableTls.Checked);
                case(1):                    
                    return GetTCPServerFunctor(min, max, serverEnableTls.Checked);
                case(2):
                    return GetSerialFunctor(min, max);
                default:
                    return null;
            }
        }        

        private Func<IDNP3Manager, IChannel> GetSerialFunctor(TimeSpan min, TimeSpan max)
        {
            var name = this.comboBoxSerialDeviceName.Text;
            var baud = Decimal.ToInt32(this.numericUpDownBaud.Value);
            
            var dataBits = Decimal.ToInt32(this.numericUpDownDataBits.Value);
            var parity = comboBoxParity.SelectedValue is Parity selectedParity ? selectedParity : Parity.None;
            var flow = comboBoxFlowControl.SelectedValue is FlowControl selectedFlow ? selectedFlow : FlowControl.None;
            var stopBits = comboBoxStopBits.SelectedValue is StopBits selectedStopBits ? selectedStopBits : StopBits.One;

            var flags = logLevelControl1.Filters.Flags;
            var retry = new ChannelRetry(min, max, min);
            var ss = new SerialSettings(name, baud, dataBits, stopBits, parity, flow);
            return (IDNP3Manager manager) => manager.AddSerial(this.textBoxID.Text, flags, retry, ss, ChannelListener.None());
        }

        private Func<IDNP3Manager, IChannel> GetTCPClientFunctor(TimeSpan min, TimeSpan max, bool useTLS)
        {
            var flags = logLevelControl1.Filters.Flags;
            var retry = new ChannelRetry(min, max, min);
            var endpoint = new IPEndpoint(textBoxHost.Text, Decimal.ToUInt16(numericUpDownPort.Value));
            if (useTLS)
            {
                var config = this.clientTLSOptionsControl.Configuration;
                return (IDNP3Manager manager) =>
                    manager.AddTLSClient(this.textBoxID.Text, flags, retry, new List<IPEndpoint> { endpoint }, config, ChannelListener.None());
            }
            else
            {
                return (IDNP3Manager manager) => manager.AddTCPClient(this.textBoxID.Text, flags, retry, new List<IPEndpoint> { endpoint }, ChannelListener.None());
            }
        }

        private Func<IDNP3Manager, IChannel> GetTCPServerFunctor(TimeSpan min, TimeSpan max, bool useTLS)
        {
            var flags = logLevelControl1.Filters.Flags;
            var retry = new ChannelRetry(min, max, min);
            var endpoint = new IPEndpoint(textBoxServerHost.Text, Decimal.ToUInt16(numericUpDownServerPort.Value));
            if (useTLS)
            {
                var config = this.serverTLSOptionsControl.Configuration;
                return (IDNP3Manager manager) =>
                    manager.AddTLSServer(this.textBoxID.Text, flags, ServerAcceptMode.CloseExisting, endpoint, config, ChannelListener.None());
            }
            else
            {
                return (IDNP3Manager manager) =>
                    manager.AddTCPServer(this.textBoxID.Text, flags, ServerAcceptMode.CloseExisting, endpoint, ChannelListener.None());
            }
        } 

        public Func<IDNP3Manager, IChannel>? ChannelAction
        {
            get
            {
                return create;
            }
        }

        public String SelectedAlias
        {
            get
            {
                return textBoxID.Text;
            }
        }

        public IReadOnlyDictionary<string, string> CsvConfiguration
        {
            get
            {
                var values = CsvConfigurationSnapshot.Capture(this, "channel");
                values["channel_name"] = SelectedAlias;
                values["channel_type"] = tabControlChannelType.SelectedTab?.Text ?? string.Empty;
                values["channel_retry_min_ms"] = numericUpDownMinRetryMS.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                values["channel_retry_max_ms"] = numericUpDownMaxRetryMS.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

                switch (tabControlChannelType.SelectedIndex)
                {
                    case 0:
                        values["channel_ip"] = textBoxHost.Text;
                        values["channel_port"] = numericUpDownPort.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    case 1:
                        values["channel_ip"] = textBoxServerHost.Text;
                        values["channel_port"] = numericUpDownServerPort.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    case 2:
                        values["channel_serial_device"] = comboBoxSerialDeviceName.Text;
                        values["channel_serial_baud"] = numericUpDownBaud.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        break;
                }

                return values;
            }
        }

        public void RestoreCsvConfiguration(IReadOnlyDictionary<string, string> values)
        {
            CsvConfigurationSnapshot.Restore(this, "channel", values);
            if (values.TryGetValue("channel_ip", out var host))
            {
                if (tabControlChannelType.SelectedIndex == 1)
                {
                    textBoxServerHost.Text = host;
                }
                else if (tabControlChannelType.SelectedIndex == 0)
                {
                    textBoxHost.Text = host;
                }
            }
            if (values.TryGetValue("channel_port", out var portText) && decimal.TryParse(portText, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var port))
            {
                if (tabControlChannelType.SelectedIndex == 1)
                {
                    numericUpDownServerPort.Value = Math.Min(numericUpDownServerPort.Maximum, Math.Max(numericUpDownServerPort.Minimum, port));
                }
                else if (tabControlChannelType.SelectedIndex == 0)
                {
                    numericUpDownPort.Value = Math.Min(numericUpDownPort.Maximum, Math.Max(numericUpDownPort.Minimum, port));
                }
            }
            if (values.TryGetValue("channel_serial_device", out var device))
            {
                comboBoxSerialDeviceName.Text = device;
            }
            if (values.TryGetValue("channel_name", out var alias))
            {
                textBoxID.Text = alias;
            }

            create = GetCreateFunctorMaybeNull();
        }

        private Func<IDNP3Manager, IChannel>? create;

        /*
        private void clientTlsBrowseCert_Click(object sender, EventArgs e)
        {
            DialogResult result = openFileDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                clientTlsCert.Text = openFileDialog.FileName;
            }
        }

        private void clientTlsBrowsePrivateKey_Click(object sender, EventArgs e)
        {
            DialogResult result = openFileDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                clientTlsPrivateKey.Text = openFileDialog.FileName;
            }
        }

        private void clientTlsBrowseTrustedCert_Click(object sender, EventArgs e)
        {
            DialogResult result = openFileDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                clientTlsTrustedCert.Text = openFileDialog.FileName;
            }
        }

        private void serverTlsBrowseCert_Click(object sender, EventArgs e)
        {
            DialogResult result = openFileDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                serverTlsCert.Text = openFileDialog.FileName;
            }
        }

        private void serverTlsBrowsePrivateKey_Click(object sender, EventArgs e)
        {
            DialogResult result = openFileDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                serverTlsPrivateKey.Text = openFileDialog.FileName;
            }
        }

        private void serverTlsBrowseTrustedCert_Click(object sender, EventArgs e)
        {
            DialogResult result = openFileDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                serverTlsTrustedCert.Text = openFileDialog.FileName;
            }
        }
        */
    }
}
