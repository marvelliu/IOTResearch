using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using AdHocBaseApp;
using System.Diagnostics;


namespace VANETs
{
    public partial class MainForm : BaseForm
    {
        float offsetX = 50;
        float offsetY = 50;
        float r = 7;
        bool inited = false;
        int showed = 0;


        void Init()
        {
            //Console.Out.WriteLine("Program Starts...");
            Global global = Global.getInstance();
            VANETOrganization.GenerateNodes();
            VANETOrganization.GenerateNetworks();
            VANETOrganization.GenerateNodePositionsAllRandom();
            EventManager handler = new EventManager();
            handler.LoadEvents();

            global.mainForm = (MainForm)this;
            inited = true;
        }

        public override void Quit()
        {
            if (showed == 0)
                Application.Exit();
        }

        void DrawNodes(Graphics gc)
        {
            Global global = Global.getInstance();
            if (global.nodraw)
                return;
            if (inited == false)
                return;
            showed = 1;

            if (global.orgNum <= 0 || global.readerNum <= 0)
                return;

            gc.Clear(Color.White);

            Brush brush = new SolidBrush(Color.Black);
            Pen pen = new Pen(brush);
            gc.DrawLine(pen, 0 + offsetX, 0 + offsetY,
                           (float)global.layoutX + offsetX, 0 + offsetY);
            gc.DrawLine(pen, (float)global.layoutX + offsetX, 0 + offsetY,
                           (float)global.layoutX + offsetX, (float)global.layoutY + offsetY);
            gc.DrawLine(pen, (float)global.layoutX + offsetX, (float)global.layoutY + offsetY,
                            0 + offsetX, (float)global.layoutY + offsetY);
            gc.DrawLine(pen, 0 + offsetX, (float)global.layoutY + offsetY,
                           0 + offsetX, 0 + offsetY);

            for (int i = 0; i < global.objectNum; i++)
            {
                brush = new SolidBrush(Color.Aquamarine);
                pen = new Pen(brush);

                gc.FillEllipse(brush, (float)global.objects[i].X - r / 2 + offsetX,
                        (float)global.objects[i].Y - r / 2 + offsetY, r, r);
                gc.DrawString("O" + global.objects[i].Id.ToString(), new Font("arial", 10), brush,
                    (float)global.objects[i].X + offsetX, (float)global.objects[i].Y + offsetY);
            }

            VANETServer server = VANETServer.getInstance();
            for (int i = 0; i < global.readers.Length; i++)
            {
                VANETReader reader = (VANETReader)global.readers[i];
                GatewayEntity g = null;
                int count = 0;
                count = reader.gatewayEntities.Count;
                if(count >0)
                    g = reader.gatewayEntities[-1];
                if (count == 0)//no backbone yet? just use the last color as default
                    brush = new SolidBrush(Organization.colors[Organization.colors.Length - 1]);
                else
                {
                    if (server.BackboneNodeMapping.ContainsKey(g.gateway))
                    {
                        brush = new SolidBrush(Organization.colors[server.BackboneNodeMapping[g.gateway]]);
                    }
                    else
                        Debug.Assert(false, "Mapping not exist, error.....");
                }
                pen = new Pen(brush);

                gc.DrawString(reader.Id.ToString(), new Font("arial", 10), brush,
                    (float)reader.X + offsetX, (float)reader.Y + offsetY);
                gc.FillEllipse(brush, (float)reader.X - r / 2 + offsetX,
                    (float)reader.Y - r / 2 + offsetY, r, r);
                if (count > 0 && reader.Id == g.gateway)//is a backbone
                    gc.FillEllipse(brush, (float)reader.X - r + offsetX,
                        (float)reader.Y - r + offsetY, 2 * r, 2 * r);


                lock (reader.Neighbors)
                {
                    foreach (Neighbor nb in new List<Neighbor>(reader.Neighbors.Values))
                    {
                        Reader node = nb.node;
                        gc.DrawLine(pen, (float)reader.X + offsetX,
                            (float)reader.Y + offsetY,
                            (float)node.X + offsetX, (float)node.Y + offsetY);
                    }
                }
            }
            showed = 0;

        }

        void ShowNodeInfo()
        {
            StringBuilder sb = new StringBuilder(4096);
            Global global = Global.getInstance();
            for (int i = 0; i < global.orgNum; i++)
            {
                sb.AppendLine((global.orgs[i].Id + " " + global.orgs[i].Name + " " + global.orgs[i].Nodes.Count));
                Organization org = global.orgs[i];
                for (int j = 0; j < org.Nodes.Count; j++)
                {
                    sb.Append("\t" + org.Nodes[j].Id + ":" + org.Nodes[j].OrgId
                        + " (" + org.Nodes[j].X + "," + org.Nodes[j].Y + ")");
                    if (j > 0)
                        sb.AppendLine("\t"+Utility.Distance(org.Nodes[j], org.Nodes[j - 1]).ToString());
                    else
                        sb.AppendLine();
                }
            }
            InfoForm info = new InfoForm(sb.ToString());
            info.ShowDialog();
        }
        public MainForm()
        {
            InitializeComponent();
            Global global = Global.getInstance();
            if (global.automatic)
            {
                Init();
                Scheduler scheduler = Scheduler.getInstance();
                scheduler.Start();
                this.lblThreadInfo.Text = "Thread started";
            }
            if (global.nodraw)
            {
                this.ShowInTaskbar = false;
                this.WindowState = FormWindowState.Minimized;
            }
            CheckForIllegalCrossThreadCalls = false;
        }

        private void initToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Init();
            this.Invalidate();            
        }

        private void nodeInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowNodeInfo();
        }

        private void MainForm_MouseMove(object sender, MouseEventArgs e)
        {
            this.lblNodeInfo.Text = "(" + (e.X-offsetX).ToString() + "," + (e.Y-offsetY).ToString() + "）";
        }

        private void startToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Scheduler scheduler = Scheduler.getInstance();
            scheduler.Start();
            this.lblThreadInfo.Text = "Thread started";
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Scheduler scheduler = Scheduler.getInstance();
            if(scheduler.Started())
                scheduler.Stop();
            this.lblThreadInfo.Text = "Thread stopped";
        }

        private void stopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Scheduler scheduler = Scheduler.getInstance();
            scheduler.Stop();
            this.lblThreadInfo.Text = "Thread stopped";
        }



        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics gc = e.Graphics;
            gc.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;
            gc.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
            gc.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixel;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            this.SetStyle(ControlStyles.UserPaint, true);
            this.SetStyle(ControlStyles.ResizeRedraw, true);

            base.OnPaint(e);

            DrawNodes(e.Graphics);
            
            Scheduler scheduler = Scheduler.getInstance();
            if (scheduler.Started())
                this.lblThreadInfo.Text = scheduler.currentTime.ToString();            
            
        }

        private void generateEventToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EventGenerator generator = new EventGenerator();
            //generator.GenerateEvents(true);
        }

        private void generateObjectMotionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Global global = Global.getInstance();
            EventGenerator generator = new EventGenerator();
            generator.GenerateRandomObjectMotionEvents(true, true, 30, 5, global.objectNum, NodeType.OBJECT, global.eventsFileName);
        }

        private void reloadEventsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Event.LoadEvents();
        }        
    }
}
