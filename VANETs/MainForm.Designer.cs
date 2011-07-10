namespace VANETs
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.preSetToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.initToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.generateEventToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.generateObjectMotionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.reloadEventsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.mainToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.startToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.stopToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.infomationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.nodeInfoToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.lblNodeInfo = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblThreadInfo = new System.Windows.Forms.ToolStripStatusLabel();
            this.menuStrip1.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.preSetToolStripMenuItem,
            this.mainToolStripMenuItem,
            this.infomationToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(884, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // preSetToolStripMenuItem
            // 
            this.preSetToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.initToolStripMenuItem,
            this.generateEventToolStripMenuItem,
            this.generateObjectMotionToolStripMenuItem,
            this.reloadEventsToolStripMenuItem});
            this.preSetToolStripMenuItem.Name = "preSetToolStripMenuItem";
            this.preSetToolStripMenuItem.Size = new System.Drawing.Size(52, 20);
            this.preSetToolStripMenuItem.Text = "PreSet";
            // 
            // initToolStripMenuItem
            // 
            this.initToolStripMenuItem.Name = "initToolStripMenuItem";
            this.initToolStripMenuItem.Size = new System.Drawing.Size(201, 22);
            this.initToolStripMenuItem.Text = "Init";
            this.initToolStripMenuItem.Click += new System.EventHandler(this.initToolStripMenuItem_Click);
            // 
            // generateEventToolStripMenuItem
            // 
            this.generateEventToolStripMenuItem.Name = "generateEventToolStripMenuItem";
            this.generateEventToolStripMenuItem.Size = new System.Drawing.Size(201, 22);
            this.generateEventToolStripMenuItem.Text = "Generate Event";
            this.generateEventToolStripMenuItem.Click += new System.EventHandler(this.generateEventToolStripMenuItem_Click);
            // 
            // generateObjectMotionToolStripMenuItem
            // 
            this.generateObjectMotionToolStripMenuItem.Name = "generateObjectMotionToolStripMenuItem";
            this.generateObjectMotionToolStripMenuItem.Size = new System.Drawing.Size(201, 22);
            this.generateObjectMotionToolStripMenuItem.Text = "Generate Object Motion";
            this.generateObjectMotionToolStripMenuItem.Click += new System.EventHandler(this.generateObjectMotionToolStripMenuItem_Click);
            // 
            // reloadEventsToolStripMenuItem
            // 
            this.reloadEventsToolStripMenuItem.Name = "reloadEventsToolStripMenuItem";
            this.reloadEventsToolStripMenuItem.Size = new System.Drawing.Size(201, 22);
            this.reloadEventsToolStripMenuItem.Text = "Reload Events";
            this.reloadEventsToolStripMenuItem.Click += new System.EventHandler(this.reloadEventsToolStripMenuItem_Click);
            // 
            // mainToolStripMenuItem
            // 
            this.mainToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.startToolStripMenuItem,
            this.stopToolStripMenuItem});
            this.mainToolStripMenuItem.Name = "mainToolStripMenuItem";
            this.mainToolStripMenuItem.Size = new System.Drawing.Size(46, 20);
            this.mainToolStripMenuItem.Text = "Main";
            // 
            // startToolStripMenuItem
            // 
            this.startToolStripMenuItem.Name = "startToolStripMenuItem";
            this.startToolStripMenuItem.Size = new System.Drawing.Size(98, 22);
            this.startToolStripMenuItem.Text = "Start";
            this.startToolStripMenuItem.Click += new System.EventHandler(this.startToolStripMenuItem_Click);
            // 
            // stopToolStripMenuItem
            // 
            this.stopToolStripMenuItem.Name = "stopToolStripMenuItem";
            this.stopToolStripMenuItem.Size = new System.Drawing.Size(98, 22);
            this.stopToolStripMenuItem.Text = "Stop";
            this.stopToolStripMenuItem.Click += new System.EventHandler(this.stopToolStripMenuItem_Click);
            // 
            // infomationToolStripMenuItem
            // 
            this.infomationToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.nodeInfoToolStripMenuItem1});
            this.infomationToolStripMenuItem.Name = "infomationToolStripMenuItem";
            this.infomationToolStripMenuItem.Size = new System.Drawing.Size(78, 20);
            this.infomationToolStripMenuItem.Text = "Infomation";
            // 
            // nodeInfoToolStripMenuItem1
            // 
            this.nodeInfoToolStripMenuItem1.Name = "nodeInfoToolStripMenuItem1";
            this.nodeInfoToolStripMenuItem1.Size = new System.Drawing.Size(127, 22);
            this.nodeInfoToolStripMenuItem1.Text = "Node Info";
            this.nodeInfoToolStripMenuItem1.Click += new System.EventHandler(this.nodeInfoToolStripMenuItem_Click);
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblNodeInfo,
            this.lblThreadInfo});
            this.statusStrip1.Location = new System.Drawing.Point(0, 728);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(884, 22);
            this.statusStrip1.TabIndex = 1;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // lblNodeInfo
            // 
            this.lblNodeInfo.Name = "lblNodeInfo";
            this.lblNodeInfo.Size = new System.Drawing.Size(41, 17);
            this.lblNodeInfo.Text = "Nodes";
            // 
            // lblThreadInfo
            // 
            this.lblThreadInfo.Name = "lblThreadInfo";
            this.lblThreadInfo.Size = new System.Drawing.Size(104, 17);
            this.lblThreadInfo.Text = "Thread not started";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.ClientSize = new System.Drawing.Size(884, 750);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.MaximumSize = new System.Drawing.Size(900, 788);
            this.MinimumSize = new System.Drawing.Size(900, 788);
            this.Name = "MainForm";
            this.Text = "VANETs";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.MainForm_MouseMove);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem mainToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem initToolStripMenuItem;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel lblNodeInfo;
        private System.Windows.Forms.ToolStripMenuItem startToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem infomationToolStripMenuItem;
        private System.Windows.Forms.ToolStripStatusLabel lblThreadInfo;
        private System.Windows.Forms.ToolStripMenuItem stopToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem nodeInfoToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem preSetToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem generateEventToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem generateObjectMotionToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem reloadEventsToolStripMenuItem;
    }
}

