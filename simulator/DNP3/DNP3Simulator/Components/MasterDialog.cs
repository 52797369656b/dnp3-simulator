using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using Automatak.DNP3.Interface;

namespace Automatak.Simulator.DNP3.Components
{
    public partial class MasterDialog : Form
    {
        public MasterDialog()
        {
            InitializeComponent();

            this.linkConfigControl.Configuration = new LinkConfig(true);
        }

        private void buttonADD_Click(object sender, EventArgs e)
        {            
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        public String SelectedAlias
        {
            get
            {
                return textBoxID.Text;
            }
        }

        public MasterStackConfig Configuration
        {
            get
            {
                var config = new MasterStackConfig();
                config.link = this.linkConfigControl.Configuration;
                config.master = this.masterConfigControl.Configuration;
                return config;
            }
        }

        public IReadOnlyDictionary<string, string> CsvConfiguration
        {
            get
            {
                var values = CsvConfigurationSnapshot.Capture(this, "master");
                values["master_name"] = SelectedAlias;
                values["master_address"] = linkConfigControl.Configuration.localAddr.ToString();
                values["slave_address"] = linkConfigControl.Configuration.remoteAddr.ToString();
                return values;
            }
        }
    }
}
