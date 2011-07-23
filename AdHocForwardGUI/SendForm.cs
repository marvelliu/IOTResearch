using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace AdHocBaseApp
{
    public partial class SendForm : Form
    {
        public bool ok = false;

        public int generateMode = 0;
        public bool clear = true;
        public NodeType fromType = NodeType.READER;
        public NodeType toType = NodeType.READER;
        public double minDist = 0;


        public SendForm()
        {
            InitializeComponent();
        }

        private void cmbType_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.cmbType.SelectedIndex == 1)
            {
                this.cmbFrom.Enabled = true;
            }
            else
            {
                this.cmbFrom.Enabled = false;
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            this.clear = this.chkClear.Checked;
            this.generateMode = this.cmbType.SelectedIndex;
            this.fromType = (NodeType)Enum.Parse(typeof(NodeType), this.cmbFrom.Text, false);
            this.toType = (NodeType)Enum.Parse(typeof(NodeType), this.cmbTo.Text, false);
            this.minDist = (double)this.numMinDist.Value;

            this.ok = true;
            this.Hide();
        }
    }
}
