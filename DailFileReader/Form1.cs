using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Windows.Forms.DataVisualization.Charting;


//  E. Yamamoto 07/2016, Rapiscan Systems
//  Drag and drop a daily file and plot it on two charts (one for gamma, one for neutron). 
//  7/13/2016 Added ability to hide graphs from detectors, added sum counts charts.
namespace DailFileReader
{
    public partial class DailyFileReader : Form
    {
        List<Int32[]> gb;
        List<Int32[]> nb;
        List<Double[]> nb1;
        ScanBuffer gscan;

        Point? prevPosition = null;
        ToolTip tooltip = new ToolTip();


        int nGX;
        int nGA;
        int nNA;
        int gammaAlarms;
        int neutronAlarms;

        String gammaSigma;
        String neutronFAP;

        public DailyFileReader()
        {
            InitializeComponent();

            this.chart1.MouseClick += new System.Windows.Forms.MouseEventHandler(this.chart1_Click);
            this.chart2.MouseClick += new System.Windows.Forms.MouseEventHandler(this.chart2_Click);
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            this.Text = System.String.Format("DailyFileReader Version {0}", version);

            this.AllowDrop = true;
            this.DragEnter += new DragEventHandler(Form1_DragEnter);
            this.DragDrop += new DragEventHandler(Form1_DragDrop);
            gb = new List<Int32[]>();
            nb = new List<Int32[]>();
            nb1 = new List<Double[]>();
            gscan = new ScanBuffer(5);


            //initialize all the charting stuff...
            chart1.ChartAreas[0].AxisX.ScaleView.Zoomable = true;
            chart2.ChartAreas[0].AxisX.ScaleView.Zoomable = true;

            chart1.Series.Clear();
            chart2.Series.Clear();

            for (int i = 0; i < 4; i++)
            {
                var dataSeries = new Series();
                dataSeries.Name = String.Format("Gamma{0}", i + 1);
                dataSeries.ChartType = SeriesChartType.Line;
                chart1.Series.Add(dataSeries);
            }
            for (int i = 0; i < 4; i++)
            {
                var dataSeries = new Series();
                dataSeries.Name = String.Format("Neutron{0}", i + 1);
                dataSeries.ChartType = SeriesChartType.Line;
                chart2.Series.Add(dataSeries);
            }

            for (int i = 0; i < 4; i++)
            {
                var dataSeries = new Series();
                dataSeries.Name = String.Format("GammaOccupancy{0}", i + 1);
                dataSeries.ChartType = SeriesChartType.Point;
                chart1.Series.Add(dataSeries);
            }
            for (int i = 0; i < 4; i++)
            {
                var dataSeries = new Series();
                dataSeries.Name = String.Format("NeutronOccupancy{0}", i + 1);
                dataSeries.ChartType = SeriesChartType.Point;
                chart2.Series.Add(dataSeries);
            }


            var dataSeriesSum = new Series();
            dataSeriesSum.Name = String.Format("Sum");
            dataSeriesSum.ChartType = SeriesChartType.Line;
            dataSeriesSum.Enabled = false;
            chart1.Series.Add(dataSeriesSum);

            dataSeriesSum = new Series();
            dataSeriesSum.Name = String.Format("SumOccupancy");
            dataSeriesSum.ChartType = SeriesChartType.Point;
            dataSeriesSum.Enabled = false; 
            chart1.Series.Add(dataSeriesSum);

            dataSeriesSum = new Series();
            dataSeriesSum.Name = String.Format("Sum");
            dataSeriesSum.ChartType = SeriesChartType.Line;
            dataSeriesSum.Enabled = false; 
            chart2.Series.Add(dataSeriesSum);

            dataSeriesSum = new Series();
            dataSeriesSum.Name = String.Format("SumOccupancy");
            dataSeriesSum.ChartType = SeriesChartType.Point;
            dataSeriesSum.Enabled = false; 
            chart2.Series.Add(dataSeriesSum);


            //mouse stuff...
            this.chart1.MouseMove += new MouseEventHandler(chart1_MouseMove);
            this.tooltip.AutomaticDelay = 10;

            chart1.ChartAreas[0].AxisX.LabelStyle.Format = "{0:0}";
            chart1.ChartAreas[0].AxisY.LabelStyle.Format = "{0:0}";
            chart1.ChartAreas[0].AxisY.Title = "CPS";

            this.chart2.MouseMove += new MouseEventHandler(chart2_MouseMove);
            this.tooltip.AutomaticDelay = 10;

            this.MouseWheel += new MouseEventHandler(chData_MouseWheel);

            chart2.ChartAreas[0].AxisX.LabelStyle.Format = "{0:0}";
            chart2.ChartAreas[0].AxisY.LabelStyle.Format = "{0:0}";
            chart2.ChartAreas[0].AxisY.Title = "CPS";


        }
        void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }
        //this is where all the important stuff happens
        void Form1_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string file in files) Console.WriteLine(file);
            //open and read file
            gb.Clear();
            nb.Clear();
            nb1.Clear();

            nGX = nNA = nGA = gammaAlarms = neutronAlarms = 0;

            foreach (var series in chart1.Series)
            {
                series.Points.Clear();
            }
            foreach (var series in chart2.Series)
            {
                series.Points.Clear();
            }

            int entriesFound = 0;
            int gbs = 0;
            int nbs = 0;
            using (var textReader = new StreamReader(files[0]))
            {
                string line = textReader.ReadLine();
                int skipCount = 0;
                while (line != null && skipCount < 1)
                {
                    line = textReader.ReadLine();

                    skipCount++;
                }
                //parse the info (only the gamma and neutron stuff).
                while (line != null)
                {
                    string[] columns = line.Split(',');
                    //perform your logic
                    entriesFound++;
                    switch (columns[0])
                    {
                        case "GX":
                            nGX++;
                            if (nGA > 0) gammaAlarms++;
                            if (nNA > 0) neutronAlarms++;
                            nGA = nNA = 0;
                            break;
                        case "SG1":
                            gammaSigma = String.Format("Gamma NSigma: {0}", columns[5]);
                            break;
                        case "SN1":
                            neutronFAP = String.Format("Neutron FAP: {0}", columns[3]);
                            break;
                        case "GS": //gamma occupancy stuff
                        case "GA":
                            if(columns[0] == "GA") nGA++;
                            List<float> GMessage = new List<float>();
                            for (int i = 1; i <= 4; i++)
                            {
                                GMessage.Add(Convert.ToSingle(columns[i]));
                            }
                            gscan.AddData(GMessage);
                            float[] gammasum = gscan.SumData();
                            if (gscan.Count() == 5)
                            {
                                gbs++;
                                chart1.Series["GammaOccupancy1"].Points.AddXY(gbs, gammasum[0]);
                                chart1.Series["GammaOccupancy2"].Points.AddXY(gbs, gammasum[1]);
                                chart1.Series["GammaOccupancy3"].Points.AddXY(gbs, gammasum[2]);
                                chart1.Series["GammaOccupancy4"].Points.AddXY(gbs, gammasum[3]);
                                chart1.Series["SumOccupancy"].Points.AddXY(gbs, gammasum[0] + gammasum[1] + gammasum[2] + gammasum[3]);
                            }
                            break;
                        case "GB": //gamma background (once every 5 seconds)
                            gbs += 5;
                            chart1.Series["Gamma1"].Points.AddXY(gbs, Convert.ToDouble(columns[1]));
                            chart1.Series["Gamma2"].Points.AddXY(gbs, Convert.ToDouble(columns[2]));
                            chart1.Series["Gamma3"].Points.AddXY(gbs, Convert.ToDouble(columns[3]));
                            chart1.Series["Gamma4"].Points.AddXY(gbs, Convert.ToDouble(columns[4]));
                            chart1.Series["Sum"].Points.AddXY(gbs, Convert.ToDouble(columns[1]) + Convert.ToDouble(columns[2]) + Convert.ToDouble(columns[3]) + Convert.ToDouble(columns[4]));
                            break;
                        case "NB": //neutron background stuff (once every 5 seconds)
                            nbs += 5;
                            chart2.Series["Neutron1"].Points.AddXY(nbs, Convert.ToDouble(columns[1]));
                            chart2.Series["Neutron2"].Points.AddXY(nbs, Convert.ToDouble(columns[2]));
                            chart2.Series["Neutron3"].Points.AddXY(nbs, Convert.ToDouble(columns[3]));
                            chart2.Series["Neutron4"].Points.AddXY(nbs, Convert.ToDouble(columns[4]));
                            chart2.Series["Sum"].Points.AddXY(nbs, Convert.ToDouble(columns[1]) + Convert.ToDouble(columns[2]) + Convert.ToDouble(columns[3]) + Convert.ToDouble(columns[4]));
                            break;
                        case "NS": //neutron stuff during occupancy
                        case "NA":
                            if(columns[0] == "NA") nNA++;
                            nbs ++;
                            chart2.Series["NeutronOccupancy1"].Points.AddXY(nbs, Convert.ToDouble(columns[1]));
                            chart2.Series["NeutronOccupancy2"].Points.AddXY(nbs, Convert.ToDouble(columns[2]));
                            chart2.Series["NeutronOccupancy3"].Points.AddXY(nbs, Convert.ToDouble(columns[3]));
                            chart2.Series["NeutronOccupancy4"].Points.AddXY(nbs, Convert.ToDouble(columns[4]));
                            chart2.Series["SumOccupancy"].Points.AddXY(nbs, Convert.ToDouble(columns[1]) + Convert.ToDouble(columns[2]) + Convert.ToDouble(columns[3]) + Convert.ToDouble(columns[4]));
                            break;
                        default:
                            break;
                    }

                    line = textReader.ReadLine();
                }
                Console.WriteLine(String.Format("Number of GBs: {0}", gbs));
                label1.Text = String.Format("Occupancies: {0}, Gamma Alarms: {1}, Neutron Alarms: {2}",nGX, gammaAlarms, neutronAlarms);
                label2.Text = String.Format("{0}\n{1}", gammaSigma, neutronFAP);
            }
        }


        void chart1_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.Location;
            if (prevPosition.HasValue && pos == prevPosition.Value)
                return;
            tooltip.RemoveAll();
            prevPosition = pos;
            var results = chart1.HitTest(pos.X, pos.Y, false,
                                         ChartElementType.PlottingArea);
            foreach (var result in results)
            {
                if (result.ChartElementType == ChartElementType.PlottingArea)
                {
                    var xVal = result.ChartArea.AxisX.PixelPositionToValue(pos.X);
                    var yVal = result.ChartArea.AxisY.PixelPositionToValue(pos.Y);

                    var valstring = String.Format("X={0:0}, Y={1:f2} CPS", xVal, yVal);
                    tooltip.Show(valstring, this.chart1,
                                 pos.X + 20, pos.Y - 15);
                }
            }
        }
        void chart2_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.Location;
            if (prevPosition.HasValue && pos == prevPosition.Value)
                return;
            tooltip.RemoveAll();
            prevPosition = pos;
            var results = chart2.HitTest(pos.X, pos.Y, false,
                                         ChartElementType.PlottingArea);
            foreach (var result in results)
            {
                if (result.ChartElementType == ChartElementType.PlottingArea)
                {
                    var xVal = result.ChartArea.AxisX.PixelPositionToValue(pos.X);
                    var yVal = result.ChartArea.AxisY.PixelPositionToValue(pos.Y);

                    var valstring = String.Format("X={0:0}, Y={1:f2} CPS", xVal, yVal);
                    tooltip.Show(valstring, this.chart2,
                                 pos.X+20, pos.Y - 15);
                }
            }
        }

        private void chData_MouseWheel(object sender, MouseEventArgs e)
        {
            try
            {
                if (e.Delta < 0)
                {
                    chart1.ChartAreas[0].AxisX.ScaleView.ZoomReset();
                    chart1.ChartAreas[0].AxisY.ScaleView.ZoomReset();
                    chart2.ChartAreas[0].AxisX.ScaleView.ZoomReset();
                    chart2.ChartAreas[0].AxisY.ScaleView.ZoomReset();
                }

                if (e.Delta > 0)
                {
                    double xMin = chart1.ChartAreas[0].AxisX.ScaleView.ViewMinimum;
                    double xMax = chart1.ChartAreas[0].AxisX.ScaleView.ViewMaximum;
                    double yMin = chart1.ChartAreas[0].AxisY.ScaleView.ViewMinimum;
                    double yMax = chart1.ChartAreas[0].AxisY.ScaleView.ViewMaximum;

                    double posXStart = chart1.ChartAreas[0].AxisX.PixelPositionToValue(e.Location.X) - (xMax - xMin) / 4;
                    double posXFinish = chart1.ChartAreas[0].AxisX.PixelPositionToValue(e.Location.X) + (xMax - xMin) / 4;
                    double posYStart = chart1.ChartAreas[0].AxisY.PixelPositionToValue(e.Location.Y) - (yMax - yMin) / 4;
                    double posYFinish = chart1.ChartAreas[0].AxisY.PixelPositionToValue(e.Location.Y) + (yMax - yMin) / 4;

                    chart1.ChartAreas[0].AxisX.ScaleView.Zoom(posXStart, posXFinish);
                   // chart1.ChartAreas[0].AxisY.ScaleView.Zoom(posYStart, posYFinish);

                    double xMin2 = chart2.ChartAreas[0].AxisX.ScaleView.ViewMinimum;
                    double xMax2 = chart2.ChartAreas[0].AxisX.ScaleView.ViewMaximum;
                    double yMin2 = chart2.ChartAreas[0].AxisY.ScaleView.ViewMinimum;
                    double yMax2 = chart2.ChartAreas[0].AxisY.ScaleView.ViewMaximum;

                    double posXStart2 = chart2.ChartAreas[0].AxisX.PixelPositionToValue(e.Location.X) - (xMax2 - xMin2) / 4;
                    double posXFinish2 = chart2.ChartAreas[0].AxisX.PixelPositionToValue(e.Location.X) + (xMax2 - xMin2) / 4;
                    double posYStart2 = chart2.ChartAreas[0].AxisY.PixelPositionToValue(e.Location.Y) - (yMax2 - yMin2) / 4;
                    double posYFinish2 = chart2.ChartAreas[0].AxisY.PixelPositionToValue(e.Location.Y) + (yMax2 - yMin2) / 4;

                    chart2.ChartAreas[0].AxisX.ScaleView.Zoom(posXStart2, posXFinish2);
                    //chart2.ChartAreas[0].AxisY.ScaleView.Zoom(posYStart2, posYFinish2);

                }
            }
            catch { }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {

                chart1.Series["Gamma1"].Enabled = checkBox1.Checked;
                chart1.Series["GammaOccupancy1"].Enabled = checkBox1.Checked;
                chart1.ChartAreas[0].RecalculateAxesScale();
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
                chart1.Series["Gamma2"].Enabled = checkBox2.Checked;
                chart1.Series["GammaOccupancy2"].Enabled = checkBox2.Checked;
                chart1.ChartAreas[0].RecalculateAxesScale();

        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
 
                chart1.Series["Gamma3"].Enabled = checkBox3.Checked;
                chart1.Series["GammaOccupancy3"].Enabled = checkBox3.Checked;
                chart1.ChartAreas[0].RecalculateAxesScale();
        }

        private void checkBox4_CheckedChanged(object sender, EventArgs e)
        {
                chart1.Series["Gamma4"].Enabled = checkBox4.Checked;
                chart1.Series["GammaOccupancy4"].Enabled = checkBox4.Checked;
                chart1.ChartAreas[0].RecalculateAxesScale();

        }

        private void checkBox5_CheckedChanged(object sender, EventArgs e)
        {
            chart1.Series["Sum"].Enabled = checkBox5.Checked;
            chart1.Series["SumOccupancy"].Enabled = checkBox5.Checked;
            chart1.ChartAreas[0].RecalculateAxesScale();
        }

        private void checkBox6_CheckedChanged(object sender, EventArgs e)
        {
            chart2.Series["Sum"].Enabled = checkBox6.Checked;
            chart2.Series["SumOccupancy"].Enabled = checkBox6.Checked;
            chart2.ChartAreas[0].RecalculateAxesScale();
        }

        private void checkBox10_CheckedChanged(object sender, EventArgs e)
        {
            chart2.Series["Neutron1"].Enabled = checkBox10.Checked;
            chart2.Series["NeutronOccupancy1"].Enabled = checkBox10.Checked;
            chart2.ChartAreas[0].RecalculateAxesScale();
        }

        private void checkBox9_CheckedChanged(object sender, EventArgs e)
        {
            chart2.Series["Neutron2"].Enabled = checkBox9.Checked;
            chart2.Series["NeutronOccupancy2"].Enabled = checkBox9.Checked;
            chart2.ChartAreas[0].RecalculateAxesScale();
        }

        private void checkBox8_CheckedChanged(object sender, EventArgs e)
        {
            chart2.Series["Neutron3"].Enabled = checkBox8.Checked;
            chart2.Series["NeutronOccupancy3"].Enabled = checkBox8.Checked;
            chart2.ChartAreas[0].RecalculateAxesScale();
        }

        private void checkBox7_CheckedChanged(object sender, EventArgs e)
        {
            chart2.Series["Neutron4"].Enabled = checkBox7.Checked;
            chart2.Series["NeutronOccupancy4"].Enabled = checkBox7.Checked;
            chart2.ChartAreas[0].RecalculateAxesScale();
        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void chart1_Click(object sender, MouseEventArgs e)
        {
            double xMin = chart1.ChartAreas[0].AxisX.ScaleView.ViewMinimum;
            double xMax = chart1.ChartAreas[0].AxisX.ScaleView.ViewMaximum;
            double yMin = chart1.ChartAreas[0].AxisY.ScaleView.ViewMinimum;
            double yMax = chart1.ChartAreas[0].AxisY.ScaleView.ViewMaximum;

            double posXStart = chart1.ChartAreas[0].AxisX.PixelPositionToValue(e.Location.X) - (xMax - xMin) / 4;
            double posXFinish = chart1.ChartAreas[0].AxisX.PixelPositionToValue(e.Location.X) + (xMax - xMin) / 4;
            double posYStart = chart1.ChartAreas[0].AxisY.PixelPositionToValue(e.Location.Y) - (yMax - yMin) / 4;
            double posYFinish = chart1.ChartAreas[0].AxisY.PixelPositionToValue(e.Location.Y) + (yMax - yMin) / 4;

            chart1.ChartAreas[0].AxisX.ScaleView.Zoom(posXStart, posXFinish);
            // chart1.ChartAreas[0].AxisY.ScaleView.Zoom(posYStart, posYFinish);

            double xMin2 = chart2.ChartAreas[0].AxisX.ScaleView.ViewMinimum;
            double xMax2 = chart2.ChartAreas[0].AxisX.ScaleView.ViewMaximum;
            double yMin2 = chart2.ChartAreas[0].AxisY.ScaleView.ViewMinimum;
            double yMax2 = chart2.ChartAreas[0].AxisY.ScaleView.ViewMaximum;

            double posXStart2 = chart2.ChartAreas[0].AxisX.PixelPositionToValue(e.Location.X) - (xMax2 - xMin2) / 4;
            double posXFinish2 = chart2.ChartAreas[0].AxisX.PixelPositionToValue(e.Location.X) + (xMax2 - xMin2) / 4;
            double posYStart2 = chart2.ChartAreas[0].AxisY.PixelPositionToValue(e.Location.Y) - (yMax2 - yMin2) / 4;
            double posYFinish2 = chart2.ChartAreas[0].AxisY.PixelPositionToValue(e.Location.Y) + (yMax2 - yMin2) / 4;

            chart2.ChartAreas[0].AxisX.ScaleView.Zoom(posXStart2, posXFinish2);
            //chart2.ChartAreas[0].AxisY.ScaleView.Zoom(posYStart2, posYFinish2);
        }

        private void chart2_Click(object sender, MouseEventArgs e)
        {
            double xMin = chart1.ChartAreas[0].AxisX.ScaleView.ViewMinimum;
            double xMax = chart1.ChartAreas[0].AxisX.ScaleView.ViewMaximum;
            double yMin = chart1.ChartAreas[0].AxisY.ScaleView.ViewMinimum;
            double yMax = chart1.ChartAreas[0].AxisY.ScaleView.ViewMaximum;

            double posXStart = chart1.ChartAreas[0].AxisX.PixelPositionToValue(e.Location.X) - (xMax - xMin) / 4;
            double posXFinish = chart1.ChartAreas[0].AxisX.PixelPositionToValue(e.Location.X) + (xMax - xMin) / 4;
            double posYStart = chart1.ChartAreas[0].AxisY.PixelPositionToValue(e.Location.Y) - (yMax - yMin) / 4;
            double posYFinish = chart1.ChartAreas[0].AxisY.PixelPositionToValue(e.Location.Y) + (yMax - yMin) / 4;

            chart1.ChartAreas[0].AxisX.ScaleView.Zoom(posXStart, posXFinish);
            // chart1.ChartAreas[0].AxisY.ScaleView.Zoom(posYStart, posYFinish);

            double xMin2 = chart2.ChartAreas[0].AxisX.ScaleView.ViewMinimum;
            double xMax2 = chart2.ChartAreas[0].AxisX.ScaleView.ViewMaximum;
            double yMin2 = chart2.ChartAreas[0].AxisY.ScaleView.ViewMinimum;
            double yMax2 = chart2.ChartAreas[0].AxisY.ScaleView.ViewMaximum;

            double posXStart2 = chart2.ChartAreas[0].AxisX.PixelPositionToValue(e.Location.X) - (xMax2 - xMin2) / 4;
            double posXFinish2 = chart2.ChartAreas[0].AxisX.PixelPositionToValue(e.Location.X) + (xMax2 - xMin2) / 4;
            double posYStart2 = chart2.ChartAreas[0].AxisY.PixelPositionToValue(e.Location.Y) - (yMax2 - yMin2) / 4;
            double posYFinish2 = chart2.ChartAreas[0].AxisY.PixelPositionToValue(e.Location.Y) + (yMax2 - yMin2) / 4;

            chart2.ChartAreas[0].AxisX.ScaleView.Zoom(posXStart2, posXFinish2);
            //chart2.ChartAreas[0].AxisY.ScaleView.Zoom(posYStart2, posYFinish2);
        }


    }


    public class ScanBuffer //hold chunks of data.
    {
        private int maxArrayLength;
        private const int nbins = 4;
        private LinkedList<float> countR0;
        private LinkedList<float> countR1;
        private LinkedList<float> countR2;
        private LinkedList<float> countR3;

        public int getMaxArrayLength() { return maxArrayLength; }
        public int getNbins() { return nbins; }
        public int Count()
        {
            return countR0.Count;
        }
        public ScanBuffer(int a)
        {
            maxArrayLength = a;
            countR0 = new LinkedList<float>();
            countR1 = new LinkedList<float>();
            countR2 = new LinkedList<float>();
            countR3 = new LinkedList<float>();
        }

        public void AddData(List<float> message)
        {
            countR0.AddLast(message[0]);
            countR1.AddLast(message[1]);
            countR2.AddLast(message[2]);
            countR3.AddLast(message[3]);

            if (Count() > getMaxArrayLength()) RemoveData(); //drops the oldest data if the array length is > max
        }

        public void RemoveData()
        {
            countR0.RemoveFirst();
            countR1.RemoveFirst();
            countR2.RemoveFirst();
            countR3.RemoveFirst();
        }

        public void ClearData()
        {
            countR0.Clear();
            countR1.Clear();
            countR2.Clear();
            countR3.Clear();
        }
        public float[] SumData()
        {
            float[] sumCounts = new float[nbins];
            for (int i = 0; i < nbins; i++) sumCounts[i] = 0;

            foreach (float value in countR0) sumCounts[0] += value;
            foreach (float value in countR1) sumCounts[1] += value;
            foreach (float value in countR2) sumCounts[2] += value;
            foreach (float value in countR3) sumCounts[3] += value;

            return sumCounts;
        }
    }
}
