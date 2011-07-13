using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using AdHocBaseApp;
using System.IO;

namespace LocationPrivacy
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
            PrivacyOrganization.GenerateNodes();
            PrivacyOrganization.GenerateOrganizations();
            PrivacyOrganization.GenerateNodePositionsAllRandom();
            PrivacyOrganization.GenerateObjectPositionsAllRandom();
            PrivacyEventManager handler = new PrivacyEventManager();
            handler.LoadEvents(false);
            //IOTReader.SetReaderTypes();

            global.mainForm = (MainForm)this;
            inited = true;
        }

        public override void Quit()
        {
            if (showed == 0)
                Application.Exit();
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
            this.Focus();
        }

        private void initToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Init();
            this.Invalidate(); 
        }

        private void startToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Scheduler scheduler = Scheduler.getInstance();
            scheduler.Start();
            this.lblThreadInfo.Text = "Thread started";
        }

        private void stopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Scheduler scheduler = Scheduler.getInstance();
            scheduler.Stop();
            this.lblThreadInfo.Text = "Thread stopped";
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


            for (int i = 0; i < global.readers.Length; i++)
            {
                PrivacyReader reader = (PrivacyReader)global.readers[i];
                //brush = new SolidBrush(Organization.colors[reader.OrgId]);
                brush = new SolidBrush(Color.Black);

                gc.DrawString("R"+reader.Id.ToString(), new Font("arial", 10), brush,
                    (float)reader.X + offsetX, (float)reader.Y + offsetY);
                if (reader.IsGateway)
                    brush = new SolidBrush(Color.Black);

                gc.FillEllipse(brush, (float)reader.X - r / 2 + offsetX,
                    (float)reader.Y - r / 2 + offsetY, r, r);



                brush = new SolidBrush(Color.Blue);
                pen = new Pen(brush);
                foreach (AnonyTreeEntry subTreeInfo in ((PrivacyReader)reader).CachedTreeEntries.Values)
                {
                    if (subTreeInfo.parent != null && subTreeInfo.status!= SubNodeStatus.OUTSIDE)
                    {
                        lock (subTreeInfo.parent)
                        {
                            Reader node = subTreeInfo.parent;
                            gc.DrawLine(pen, (float)reader.X + offsetX,
                                (float)reader.Y + offsetY,
                                (float)node.X + offsetX, (float)node.Y + offsetY);
                        }
                    }
                }
            }
            showed = 0;
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

        private void generateReaderMotionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PrivacyGlobal global = (PrivacyGlobal)Global.getInstance();
            MoveForm f = new MoveForm();
            f.nodeNum = global.readerNum;
            DialogResult r = f.ShowDialog();
            if (f.ok != true)
                return;
            int nodeCount = (int)(global.readerNum * f.nodeRatio);
            double nodeSpeed = f.nodeSpeed;
            int eventCount = f.eventCount;
            bool clear = f.clear;
            string filename = f.filename;
            EventGenerator generator = new EventGenerator();

            File.Copy(global.eventsFileName, filename, true);
            if (clear)
                generator.ClearEvents(filename, "MOV");
            generator.GenerateRandomObjectMotionEvents(true, nodeSpeed, eventCount, nodeCount, NodeType.READER, filename);
            PrivacyEventManager manager = new PrivacyEventManager();
            manager.LoadEvents(true);
            MessageBox.Show("Done");
        }

        private void generateObjectMotionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PrivacyGlobal global = (PrivacyGlobal)Global.getInstance();
            MoveForm f = new MoveForm();
            f.ShowDialog();
            if (f.ok != true)
                return;
            //if (f.DialogResult != System.Windows.Forms.DialogResult.OK)
            //    return;
            int nodeCount = (int)(global.objectNum * f.nodeRatio);
            double nodeSpeed = f.nodeSpeed;
            int eventCount = f.eventCount;
            bool clear = f.clear;
            EventGenerator generator = new EventGenerator();
            if (clear)
                generator.ClearEvents(global.eventsFileName, "MOV");
            generator.GenerateRandomObjectMotionEvents(true, nodeSpeed, eventCount, nodeCount, NodeType.OBJECT, global.eventsFileName);
            MessageBox.Show("Done");
        }

        private void generateSendDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PrivacyGlobal global = (PrivacyGlobal)Global.getInstance();
            EventGenerator generator = new EventGenerator();
            generator.ClearEvents(global.eventsFileName, "SND_DATA");
            generator.GenerateSendEvents(true, false, global.objects, global.orgs, "SND_DATA");
            MessageBox.Show("Done");
        }

        private void reloadEventsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Event.LoadEvents(true);
        }


    }
}
