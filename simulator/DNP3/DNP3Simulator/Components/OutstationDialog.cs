using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using Automatak.DNP3.Interface;
using Automatak.Simulator.DNP3.API;

namespace Automatak.Simulator.DNP3.Components
{
    partial class OutstationDialog : Form
    {
        readonly IDNP3Config config;
        readonly bool allowTemplateEdit;
        readonly OutstationStackConfig initialConfig;

        public OutstationDialog(IDNP3Config config, IOutstationModule module)
        {
            InitializeComponent();

            this.config = config;
            this.initialConfig = module.DefaultConfig;
            this.allowTemplateEdit = module.AllowTemplateEditing;
            this.textBoxID.Text = module.DefaultLogName;

            this.linkConfigControl.Configuration = initialConfig.link;            

            if (allowTemplateEdit)
            {
                comboBoxTemplate.DataSource = config.Templates.Select(kv => kv.Key).ToList();
            }
            else
            {
                groupBoxDatabase.Enabled = false;   
            }
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

        OutstationParams OutstationParameters
        {
            get
            {
                OutstationParams ret = new OutstationParams();
                ret.allowUnsolicited = this.checkBoxEnableUnsol.Checked;
                ret.maxControlsPerRequest = Decimal.ToByte(this.numericUpDownMaxControls.Value);
                ret.maxTxFragSize = Decimal.ToUInt16(this.numericUpDownMaxTxFrag.Value);
                ret.selectTimeout = TimeSpan.FromMilliseconds(Decimal.ToDouble(this.numericUpDownSelectTimeout.Value));
                ret.unsolConfirmTimeout = TimeSpan.FromMilliseconds(Decimal.ToDouble(this.numericUpDownUnsolConfirmTimeout.Value));
                ret.solicitedConfirmTimeout = TimeSpan.FromMilliseconds(Decimal.ToDouble(this.numericUpDownSolConfirmTimeout.Value));
                ret.numUnsolRetries = NumRetries.Fixed(Decimal.ToInt32(this.numericUpDownUnsolicitedRetry.Value));
                return ret;
            }
        }

        public OutstationStackConfig Configuration
        {
            get
            {
                var oc = new OutstationStackConfig();
                oc.link = this.linkConfigControl.Configuration;
                
                oc.outstation.config = this.OutstationParameters;
                oc.outstation.buffer = this.eventBufferConfigControl1.Configuration;                
                            
                if (this.allowTemplateEdit)
                {
                    if (this.comboBoxTemplate.SelectedItem is string templateId)
                    {
                        var template = config.GetTemplateMaybeNull(templateId);
                        oc.databaseTemplate = (template == null) ? initialConfig.databaseTemplate : template;
                    }
                    else
                    {
                        oc.databaseTemplate = initialConfig.databaseTemplate;
                    }
                }
                else
                {
                    oc.databaseTemplate = initialConfig.databaseTemplate;
                }
                

                return oc;
            }
        }

        public IReadOnlyDictionary<string, string> CsvConfiguration
        {
            get
            {
                var link = linkConfigControl.Configuration;
                var values = CsvConfigurationSnapshot.Capture(this, "outstation");
                values["outstation_name"] = SelectedAlias;
                values["outstation_template"] = comboBoxTemplate.SelectedItem?.ToString() ?? string.Empty;
                values["master_address"] = link.remoteAddr.ToString();
                values["slave_address"] = link.localAddr.ToString();
                if (comboBoxTemplate.SelectedItem is string templateName && config.GetTemplateMaybeNull(templateName) is { } template)
                {
                    CsvConfigurationSnapshot.AddObjectValues(values, "outstation_template", template);
                }
                return values;
            }
        }

        void CheckState()
        {
            if (this.allowTemplateEdit && comboBoxTemplate.SelectedValue == null)
            {
                this.buttonEdit.Enabled = false;
                this.buttonAdd.Enabled = false;
                this.toolStripStatusLabel1.Text = "You must select a device template";
            }
            else
            {
                this.buttonEdit.Enabled = true;
                this.buttonAdd.Enabled = true;
                this.toolStripStatusLabel1.Text = "";
            }
        }

        void OutstationDialog_Load(object sender, EventArgs e)
        {
            CheckState();
        }

        private void buttonNew_Click(object sender, EventArgs e)
        {
            using (var dialog = new TemplateDialog("template1", new DatabaseTemplate()))
            {                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    var template = dialog.ConfiguredTemplate;

                    // TODO - let the dialog config these internally
                    this.staticResponseTypeControl1.Configure(template);
                    this.eventResponseTypeControl1.Configure(template);


                    config.AddTemplate(dialog.SelectedAlias, template);
                    this.comboBoxTemplate.DataSource = config.Templates.Select(kvp => kvp.Key).ToList();                    
                }
            }
        }       

        private void comboBoxTemplate_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.CheckState();
        }

        private void buttonEdit_Click(object sender, EventArgs e)
        {
            if (this.comboBoxTemplate.SelectedItem is not string alias)
            {
                return;
            }

            var edited = config.GetTemplateMaybeNull(alias);

            if (edited != null)
            {
                using (var dialog = new TemplateDialog(alias, edited))
                {
                    dialog.ShowDialog();
                    if (dialog.DialogResult == DialogResult.OK)
                    {
                        var template = dialog.ConfiguredTemplate;
                        config.AddTemplate(dialog.SelectedAlias, template);
                        this.comboBoxTemplate.DataSource = config.Templates.Select(kvp => kvp.Key).ToList();
                    }
                }
            }
           
        }
    }
}
