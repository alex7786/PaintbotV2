using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Paintbot
{
    public partial class Form2 : Form
    {
        public Form2(Form1 form1)
        {
            form1.progressBar1.Value = 0;
            form1.progressBar1.Update();
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
