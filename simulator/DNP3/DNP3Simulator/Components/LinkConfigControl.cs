using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using Automatak.DNP3.Interface;

namespace Automatak.Simulator.DNP3.Components
{
    public partial class LinkConfigControl : UserControl
    {
        public LinkConfigControl()
        {
            InitializeComponent();
        }        

        public LinkConfig Configuration
        {
            get
            {
                var config = new LinkConfig(isMaster);
                config.isMaster = isMaster;
                config.localAddr = Decimal.ToUInt16(this.numericUpDownSource.Value);
                config.remoteAddr = Decimal.ToUInt16(this.numericUpDownDest.Value);
                config.responseTimeout = TimeSpan.FromMilliseconds(Decimal.ToUInt32(this.numericUpDownTimeout.Value));
                config.keepAliveTimeout = TimeSpan.FromMilliseconds(Decimal.ToUInt32(this.numericUpDownKeepAliveTimeout.Value * 1000));
                return config;
            }
            set
            {
                this.Configure(value);
            
            }
        }

        public void SetAddresses(ushort localAddress, ushort remoteAddress)
        {
            this.numericUpDownSource.Value = localAddress;
            this.numericUpDownDest.Value = remoteAddress;
        }

        private void SetState()
        {
            this.groupBoxConfirmed.Enabled = this.checkBoxConfirmed.Checked;            
        }

        private void Configure(LinkConfig config)
        {
            this.isMaster = config.isMaster;
            this.numericUpDownSource.Value = config.localAddr;
            this.numericUpDownDest.Value = config.remoteAddr;
            this.numericUpDownTimeout.Value = Convert.ToDecimal(config.responseTimeout.TotalMilliseconds);
            this.numericUpDownRetries.Value = 0;
            this.checkBoxConfirmed.Checked = false;
            this.numericUpDownKeepAliveTimeout.Value = Convert.ToDecimal(config.keepAliveTimeout.TotalMilliseconds);

            this.SetState();
        }

        private bool isMaster = false;

        private void checkBoxConfirmed_CheckedChanged(object sender, EventArgs e)
        {
            this.SetState();
        }     
    }
}
