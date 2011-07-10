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
    public partial class InfoForm : Form
    {
        public InfoForm(string info)
        {
            InitializeComponent();
            this.txtInfo.Text = info;
        }
    }
}
