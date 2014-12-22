using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApplication1
{
    public partial class Form2 : Form
    {
        string type_ = null;
        System.Windows.Forms.ListView listview_ = null;
        List<string> keywords_ = null;
        List<string> addrs_ = null;

        public System.Windows.Forms.ListView View
        {
            get
            {
                return listview_;
            }
            set
            {
                listview_ = value;
            }
        }

        public string type
        {
            get
            {
                return type_;
            }
            set
            {
                type_ = value;
            }
        }

        public List<string> Keyword
        {
            get
            {
                return keywords_;
            }
            set
            {
                keywords_ = value;
            }
        }

        public List<string> Addresses
        {
            get
            {
                return addrs_;
            }
            set
            {
                addrs_ = value;
            }
        }

        public Form2()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (this.listview_ == null || (keywords_ == null && addrs_ == null))
            {
                MessageBox.Show("null view");
                return;
            }

            string text = this.textBox1.Text;

            if (text.Trim() == "")
            {
                MessageBox.Show("empty text");
                return;
            }

            this.listview_.BeginUpdate();

            ListViewItem lvi = new ListViewItem();

            if (this.type_ == "keyword")
            {
                lvi.Text = text;
                this.listview_.Items.Add(lvi);
                
                this.keywords_.Add(text);
            }
            else if (this.type_ == "address")
            {
                if (!text.Contains("http://"))
                {
                    text = "http://" + text;
                }

                const string pattern = @"http://([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?";
                Regex r = new Regex(pattern, RegexOptions.IgnoreCase);
                MatchCollection m = r.Matches(text);

                if (m.Count > 0)
                {
                    lvi.Text = text;
                    this.listview_.Items.Add(lvi);

                    this.addrs_.Add(text);
                }
            }

            this.listview_.EndUpdate();

            this.Close();
        }
    }
}
