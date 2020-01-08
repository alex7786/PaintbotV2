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
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Settings.Default.Save();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Program.GenerateGCode();
        }

        private void textBox12_TextChanged(object sender, EventArgs e)
        {
            Program.DisplayPictureSize();
            Program.RefreshPreview();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            DialogResult result = openFileDialog1.ShowDialog();
            if (result == DialogResult.OK) // Test result.
            {
                String res = openFileDialog1.FileName;
                Settings.Default.imagePath = res;
                Program.LoadImage();
                Program.RefreshPreview();
                Program.DisplayPictureSize();
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            Program.RotatePicture();
            Program.DisplayPictureSize();
            Program.RefreshPreview();
        }

        private void button5_Click(object sender, EventArgs e)
        {
            Program.MirrorPictureX();
            Program.RefreshPreview();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            Program.MirrorPictureY();
            Program.RefreshPreview();
        }

        private void button7_Click(object sender, EventArgs e)
        {
            Program.ResizePicture();
            Program.RefreshPreview();
            Program.DisplayPictureSize();
        }

        private void button8_Click(object sender, EventArgs e)
        {
            Program.ParseColors();
        }

        private void button9_Click(object sender, EventArgs e)
        {
            Program.RecolorImage();
            Program.RefreshPreview();
        }
    }
}
