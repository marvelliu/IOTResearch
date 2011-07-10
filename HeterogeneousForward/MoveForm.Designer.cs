namespace HeterogeneousForward
{
    partial class MoveForm
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
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.txtNodeRatio = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.chkClear = new System.Windows.Forms.CheckBox();
            this.label3 = new System.Windows.Forms.Label();
            this.txtNodeSpeed = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.txtEventCount = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.txtOutfile = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // btnOK
            // 
            this.btnOK.Location = new System.Drawing.Point(62, 275);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 0;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(235, 275);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // txtNodeRatio
            // 
            this.txtNodeRatio.Location = new System.Drawing.Point(176, 34);
            this.txtNodeRatio.Name = "txtNodeRatio";
            this.txtNodeRatio.Size = new System.Drawing.Size(100, 21);
            this.txtNodeRatio.TabIndex = 2;
            this.txtNodeRatio.Text = "0.1";
            this.txtNodeRatio.TextChanged += new System.EventHandler(this.txtNodeRatio_TextChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(38, 37);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(119, 12);
            this.label1.TabIndex = 3;
            this.label1.Text = "Moving Node Ration:";
            // 
            // chkClear
            // 
            this.chkClear.AutoSize = true;
            this.chkClear.Checked = true;
            this.chkClear.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkClear.Location = new System.Drawing.Point(40, 168);
            this.chkClear.Name = "chkClear";
            this.chkClear.Size = new System.Drawing.Size(198, 16);
            this.chkClear.TabIndex = 4;
            this.chkClear.Text = "Clear existing moving events:";
            this.chkClear.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(38, 81);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(113, 12);
            this.label3.TabIndex = 7;
            this.label3.Text = "Moving Node Speed:";
            // 
            // txtNodeSpeed
            // 
            this.txtNodeSpeed.Location = new System.Drawing.Point(176, 78);
            this.txtNodeSpeed.Name = "txtNodeSpeed";
            this.txtNodeSpeed.Size = new System.Drawing.Size(100, 21);
            this.txtNodeSpeed.TabIndex = 6;
            this.txtNodeSpeed.Text = "10";
            this.txtNodeSpeed.TextChanged += new System.EventHandler(this.txtNodeSpeed_TextChanged);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(38, 131);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(119, 12);
            this.label2.TabIndex = 9;
            this.label2.Text = "Moving Event Count:";
            // 
            // txtEventCount
            // 
            this.txtEventCount.Location = new System.Drawing.Point(176, 128);
            this.txtEventCount.Name = "txtEventCount";
            this.txtEventCount.Size = new System.Drawing.Size(100, 21);
            this.txtEventCount.TabIndex = 8;
            this.txtEventCount.Text = "15";
            this.txtEventCount.TextChanged += new System.EventHandler(this.txtEventCount_TextChanged);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(38, 211);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(77, 12);
            this.label4.TabIndex = 11;
            this.label4.Text = "Output file:";
            // 
            // txtOutfile
            // 
            this.txtOutfile.Location = new System.Drawing.Point(146, 208);
            this.txtOutfile.Name = "txtOutfile";
            this.txtOutfile.Size = new System.Drawing.Size(260, 21);
            this.txtOutfile.TabIndex = 10;
            // 
            // MoveForm
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(418, 326);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.txtOutfile);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.txtEventCount);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.txtNodeSpeed);
            this.Controls.Add(this.chkClear);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtNodeRatio);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Name = "MoveForm";
            this.Text = "MoveForm";
            this.Load += new System.EventHandler(this.MoveForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.TextBox txtNodeRatio;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox chkClear;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox txtNodeSpeed;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox txtEventCount;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox txtOutfile;
    }
}