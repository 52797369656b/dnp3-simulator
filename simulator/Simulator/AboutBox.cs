using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace Automatak.Simulator
{
    partial class AboutBox : Form
    {
        public AboutBox()
        {
            InitializeComponent();
            var licenseFilePath = Path.Combine(AppContext.BaseDirectory, "THIRD_PARTY_LICENSES.txt");
            try
            {
                this.richTextBox1.Text = File.ReadAllText(licenseFilePath);
            }
            catch (IOException exception)
            {
                this.richTextBox1.Text = $"Unable to load THIRD_PARTY_LICENSES.txt: {exception.Message}";
            }
            catch (UnauthorizedAccessException exception)
            {
                this.richTextBox1.Text = $"Unable to load THIRD_PARTY_LICENSES.txt: {exception.Message}";
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

    }
}
