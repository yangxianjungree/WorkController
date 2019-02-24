using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Controller
{
    public partial class MainTest : Form
    {
        private yWorkThreadHandle TemplateThreadPrint = null;



        public MainTest()
        {
            InitializeComponent();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {

            if (TemplateThreadPrint == null)
            {
                TemplateThreadPrint = new yWorkThreadHandle(PrintMethodDelegate, 
                    CheckMethod, lastThingToDo, (int)nudSum.Value, 1, 100, RtbShowMessage);
            }
            if (!TemplateThreadPrint.Work_Thread())
            {
                if (true)
                {
                    //RtbShowMessage.Text = string.Empty;

                    TemplateThreadPrint = new yWorkThreadHandle(PrintMethodDelegate, 
                        CheckMethod, lastThingToDo, (int)nudSum.Value, 1, 100, RtbShowMessage);

                    TemplateThreadPrint.Work_Thread();
                }
            }

        }

        private void lastThingToDo(int index)
        {
            if (index > (int)nudSum.Value)
            {
                return;
            }

            if (index == (int)nudSum.Value)
            {
                pbBratio.Value = 100;
                return;
            }


        }

        private bool CheckMethod(int arg)
        {
            return true;
        }

        private void PrintMethodDelegate(int index)
        {

            if (lblNum.InvokeRequired || pbBratio.InvokeRequired)
            {
                Action<int> action = new Action<int>(setValue);
                //if (lblNum.Parent == null || lblNum.Parent.IsDisposed == true)
                //{
                //    return;
                //}
                lblNum.Parent.BeginInvoke(action, index);
            }
            else
            {
                setValue(index);
            }
            
        }

        private void setValue(int val)
        {
            double ratio = val / (double)nudSum.Value;
            pbBratio.Value = (int)(ratio * 100);
            lblNum.Text = val.ToString();
        }


        private void btnPause_Click(object sender, EventArgs e)
        {
            if (TemplateThreadPrint != null)
            {
                TemplateThreadPrint.Pauser_Thread();
            }

        }

        private void btnContinue_Click(object sender, EventArgs e)
        {
            if (TemplateThreadPrint != null)
            {
                TemplateThreadPrint.Continue_Thread();
            }

        }

        private void btnEnd_Click(object sender, EventArgs e)
        {
            if (TemplateThreadPrint != null)
            {
                TemplateThreadPrint.Stop_Thread();
            }

            MessageBox.Show("exiting now !!");
            this.Close();

        }

        public RichTextBox RtbShowMessage { get; set; }

        private void MainTest_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (TemplateThreadPrint != null)
            {
                TemplateThreadPrint.Stop_Thread();
            }

            System.Threading.Thread.Sleep(1000);
        }
    }
}
