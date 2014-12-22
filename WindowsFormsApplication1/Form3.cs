using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApplication1
{
    public partial class Form3 : Form
    {
        int max_connections_ = 0;
        int max_depth_ = 0;
        bool is_meticulous_ = false;

        public delegate void SaveConfigHandler(int max_connections, int max_depth, bool is_meticulous);
        public event SaveConfigHandler SaveConfig= null;

        public int max_connections
        {
            get
            {
                return this.max_connections_;
            }
            set
            {
                this.max_connections_ = value;
            }
        }

        public int max_depth
        {
            get
            {
                return this.max_depth_;
            }
            set
            {
                this.max_depth_ = value;
            }
        }

        public bool is_meticulous
        {
            get
            {
                return this.is_meticulous_;
            }
            set
            {
                this.is_meticulous_ = value;
            }
        }

        private void set_meticulous(bool is_meticulous)
        {
            if(is_meticulous_)
            {
                this.radioButton1.Checked = true;
            }
            else
            {
                this.radioButton2.Checked = true;
            }
        }

        public Form3()
        {
            InitializeComponent();
        }

        private void radioButton1_Click(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if(radioButton1.Checked)
            {
                is_meticulous_ = true;
            }

            else
            {
                is_meticulous_ = false;
            }

            bool res = int.TryParse(this.textBox1.Text, out max_connections_);
            if(!res)
            {
                MessageBox.Show("invalid connections value!");
                return;
            }

            res = int.TryParse(this.textBox2.Text, out max_depth_);
            if(!res)
            {
                MessageBox.Show("invalid depth value!");
                return;
            }

            if(SaveConfig != null)
            {
                SaveConfig(max_connections_, max_depth_, is_meticulous_);
            }
            else
            {
                MessageBox.Show("error init save handler");
            }

            this.Close();
        }

        private void Form3_Shown(object sender, EventArgs e)
        {
            this.textBox1.Text = max_connections_.ToString();
            this.textBox2.Text = max_depth_.ToString();

            set_meticulous(is_meticulous_);
        }
    }
}
