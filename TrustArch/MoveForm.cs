using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace TrustArch
{
    public partial class MoveForm : Form
    {
        public double nodeRatio = 0;
        public double nodeSpeed = 0;
        public int eventCount = 0;
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
            this.Hide();
        }
    }
}
