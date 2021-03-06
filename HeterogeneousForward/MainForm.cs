﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using AdHocBaseApp;
using System.IO;

namespace HeterogeneousForward
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
            HFOrganization.GenerateNodes();
            HFOrganization.GenerateOrganizations();
            HFOrganization.GenerateNodePositionsAllRandom();
            HFEventManager handler = new HFEventManager();
            handler.LoadEvents(false);
            //HFReader.AddDefaultForwardStrategy();

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
            HFGlobal global = (HFGlobal)Global.getInstance();
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
                HFReader reader = (HFReader)global.readers[i];
                //brush = new SolidBrush(Organization.colors[reader.OrgId]);

                float r1 = r;
                if (reader.IsHub())
                    r1 = r * 1.5f;

                if (reader.IsAllowedTags(global.currentSendingTags) && global.currentSendingTags != 0)
                    brush = new SolidBrush(Color.Blue);
                else
                    brush = new SolidBrush(Color.Black);

                gc.DrawString("R" + reader.Id.ToString(), new Font("arial", 10), brush,
                    (float)reader.X + (float)offsetX, (float)reader.Y + (float)offsetY);

                gc.FillEllipse(brush, (float)reader.X - r1 / 2 + offsetX,
                    (float)reader.Y - r1 / 2 + offsetY, r1, r1);


                pen = new Pen(brush);
                if (global.currentSendingTags>0 && reader.IsAllowedTags(global.currentSendingTags))
                {
                    lock (reader.Neighbors)
                    {
                        foreach (Neighbor nb in new List<Neighbor>(reader.Neighbors.Values))
                        {
                            HFReader node = (HFReader)nb.node;
                            if (global.drawLine && node.IsAllowedTags(global.currentSendingTags))
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
            HFGlobal global = (HFGlobal)Global.getInstance();
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
            HFEventManager manager = new HFEventManager();
            manager.LoadEvents(clear);
            if (f.outputAsDefault)
                global.eventsFileName = f.filename;
            MessageBox.Show("Done");
        }

        private void generateObjectMotionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            HFGlobal global = (HFGlobal)Global.getInstance();
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
            SendForm f = new SendForm();
            f.ShowDialog();
            if (f.ok != true)
                return;

            HFGlobal global = (HFGlobal)Global.getInstance();
            HFEventGenerator generator = new HFEventGenerator();
            Node[] froms = null;
            Node[] tos = null;
            if(f.fromType == NodeType.READER)
                froms = global.readers;
            else
                throw new Exception("Wrong node type");
            
            if(f.toType == NodeType.READER)
                tos = global.readers;
            else
                throw new Exception("Wrong node type");
            
            generator.GenerateSendEvents(f.clear, false, froms, tos, "SND_DATA", f.minDist, 2, f.generateMode);
            MessageBox.Show("Done");
        }

        private void reloadEventsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Event.LoadEvents(true);
        }


    }
}
