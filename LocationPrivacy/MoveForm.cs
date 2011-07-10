using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace LocationPrivacy
{
    public partial class MoveForm : Form
    {
        public double nodeRatio = 0;
        public double nodeSpeed = 0;
        public int eventCount = 0;
        public int nodeNum = 0;
        public string filename = "";
        public bool clear = false;

        public bool ok = false;
        public MoveForm()
        {
            InitializeComponent();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            this.nodeRatio = double.Parse(this.txtNodeRatio.Text);
            this.nodeSpeed = double.Parse(this.txtNodeSpeed.Text);
            this.eventCount = int.Parse(this.txtEventCount.Text);
            this.clear = this.chkClear.Checked;
            this.ok = true;
            this.filename = "events-r" + this.txtNodeRatio.Text + "-s" + this.txtNodeSpeed.Text + "-n" + nodeNum + ".txt";
            this.Hide();
        }

        private void MoveForm_Load(object sender, EventArgs e)
        {
            ChangeText();
        }

        private void txtNodeRatio_TextChanged(object sender, EventArgs e)
        {
            ChangeText();
        }

        private void ChangeText()
        {
            this.txtOutfile.Text = "events-r" + this.txtNodeRatio.Text + "-s" + this.txtNodeSpeed.Text + "-n" + nodeNum + ".txt"; 
        }

        private void txtNodeSpeed_TextChanged(object sender, EventArgs e)
        {
            ChangeText();
        }

        private void txtEventCount_TextChanged(object sender, EventArgs e)
        {
            ChangeText();
        }
    }
}
