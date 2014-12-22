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

using System.Reflection;

using System.IO;

namespace WindowsFormsApplication1
{
    public partial class Form1 : Form
    {
        private spyder spyder_ = null;
        string basedir_ = null;

        System.Data.SQLite.SQLiteConnection sqlite_conn_ = null;

        List<string> keywords_ = new List<string>();
        List<string> addrs_ = new List<string>();

        bool is_meticulous_ = false;
        int depth_ = 2;
        int threads_ = 4;

        public Form1()
        {
            InitializeComponent();

            init_listview();

            init_event_handler();

            //MessageBox.Show(AppDomain.CurrentDomain.BaseDirectory);
            basedir_ = AppDomain.CurrentDomain.BaseDirectory;

            try
            {
                loaddb();
            }
            catch(Exception e)
            {
                MessageBox.Show(e.Message);
            }

            init_keyword_view();
            init_address_view();

            init_status_bar();
        }

        /*
         * //创建一个数据库文件
        string datasource="h:/test.db";
        System.Data.SQLite.SQLiteConnection.CreateFile(datasource);
        //连接数据库
        System.Data.SQLite.SQLiteConnection conn = new System.Data.SQLite.SQLiteConnection();
        System.Data.SQLite.SQLiteConnectionStringBuilder connstr = new System.Data.SQLite.SQLiteConnectionStringBuilder();
        connstr.DataSource = datasource;
        connstr.Password = "admin";//设置密码，SQLite ADO.NET实现了数据库密码保护
        conn.ConnectionString = connstr.ToString();            
        conn.Open();
        //创建表
        System.Data.SQLite.SQLiteCommand cmd = new System.Data.SQLite.SQLiteCommand();
        string sql = "CREATE TABLE test(username varchar(20),password varchar(20))";
        cmd.CommandText=sql;
        cmd.Connection=conn;
        cmd.ExecuteNonQuery();
        //插入数据
        sql = "INSERT INTO test VALUES(’dotnetthink’,'mypassword’)";
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
        //取出数据
        sql = "SELECT * FROM test";
        cmd.CommandText = sql;
        System.Data.SQLite.SQLiteDataReader reader = cmd.ExecuteReader();
        StringBuilder sb = new StringBuilder();
        while (reader.Read())
        {
            sb.Append("username:").Append(reader.GetString(0)).Append("\n")
            .Append("password:").Append(reader.GetString(1));
        }
        MessageBox.Show(sb.ToString());
        */
        private void loaddb()
        {
            string db_path = basedir_ + "spyder.s3db";

            sqlite_conn_ = new System.Data.SQLite.SQLiteConnection();
            System.Data.SQLite.SQLiteConnectionStringBuilder connstr = new System.Data.SQLite.SQLiteConnectionStringBuilder();
            connstr.DataSource = db_path;
            //connstr.Password = "admin";//设置密码，SQLite ADO.NET实现了数据库密码保护
            sqlite_conn_.ConnectionString = connstr.ToString();
            sqlite_conn_.Open();

            System.Data.SQLite.SQLiteCommand cmd = new System.Data.SQLite.SQLiteCommand();
            cmd.Connection = sqlite_conn_;
            string sql = "select * from keyword";
            cmd.CommandText = sql;

            System.Data.SQLite.SQLiteDataReader reader = cmd.ExecuteReader();

            while(reader.Read())
            {
                keywords_.Add(reader.GetString(1));
            }

            reader.Close();

            sql = "select * from address";
            cmd.CommandText = sql;

            reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                addrs_.Add(reader.GetString(1));
            }

            reader.Close();

            sql = "select * from config";
            cmd.CommandText = sql;

            reader = cmd.ExecuteReader();

            if(reader.Read())
            {
                threads_ = reader.GetInt32(0);
                depth_ = reader.GetInt32(1);
                int is_meticulous = reader.GetInt32(2);
                if(is_meticulous == 1)
                {
                    is_meticulous_ = true;
                }
            }
        }

        private void init_status_bar()
        {
            this.toolStripStatusLabel1.Text = "max connections:" + threads_;
            this.toolStripStatusLabel2.Text = "max depth:" + depth_;
            this.toolStripStatusLabel3.Text = "meticulous:" + is_meticulous_;
        }

        void init_keyword_view()
        {
            this.listView2.BeginUpdate();

            foreach(string keyword in keywords_)
            {
                ListViewItem lvi = new ListViewItem();

                lvi.Text = keyword;

                this.listView2.Items.Add(lvi);
            }

            this.listView2.EndUpdate();
        }

        void init_address_view()
        {
            this.listView3.BeginUpdate();

            foreach (string addr in addrs_)
            {
                ListViewItem lvi = new ListViewItem();

                lvi.Text = addr;

                this.listView3.Items.Add(lvi);
            }

            this.listView3.EndUpdate();
        }

        private void init_event_handler()
        {
            spyder_ = new spyder();
            spyder_.PageEncoding = spyder.Encodings.UTF8;
            spyder_.DownloadFinish += new spyder.DownloadFinishHandler(spyder_finish);
            spyder_.SaveContents += new spyder.SaveContentsHandler(spyder_parser);
            spyder_.ShowStatus += new spyder.ShowStatusHandler(ShowStatus);
            spyder_.ErrorMessage += new spyder.ErrorMessageHandler(AddErrorLog);
        }

        private delegate void SetTitleProxy(string url);
        private delegate void AddListViewProxy(string addr, string title);
        private delegate void AddErrorLogProxy(string error_code);

        void AddErrorLog(string error_code)
        {
            if (this.InvokeRequired)
            {
                AddErrorLogProxy fc = new AddErrorLogProxy(AddErrorLog);
                this.Invoke(fc, new object[1] { error_code });
            }
            else
            {
                this.listView4.BeginUpdate();

                ListViewItem lvi = new ListViewItem();

                lvi.Text = error_code;

                this.listView4.Items.Add(lvi);

                this.listView4.EndUpdate();
            }
        }

        void SetTitle(string text)
        {
            if (this.InvokeRequired)
            {
                SetTitleProxy fc = new SetTitleProxy(SetTitle);
                this.Invoke(fc, new object[1] { text });
            }
            else
            {
                this.Text = text;
            }
        }

        void AddListView(string addr, string title)
        {
            if (this.InvokeRequired)
            {
                AddListViewProxy fc = new AddListViewProxy(AddListView);
                this.Invoke(fc, new object[2] { addr, title });
            }
            else
            {
                this.listView1.BeginUpdate();

                ListViewItem lvi = new ListViewItem();

                lvi.Text = addr;

                lvi.SubItems.Add(title);

                this.listView1.Items.Add(lvi);

                this.listView1.EndUpdate();
            }
            
        }

        void ShowStatus(int index, string url)
        {
           SetTitle("spyder" + "(" + index.ToString() + ")" + url);
        }

        void spyder_finish(int count)
        {
            SetTitle("spyder");
            MessageBox.Show("finish:" + count.ToString());
        }

        void spyder_parser(string html, string url)
        {
            string[] keyword = keywords_.ToArray();
            //string t1 = "地球资源";
            //keyword[0] = t1;
            //keyword[1] = "习近平";
            int title_begin = html.IndexOf("<title>");
            int title_end = html.IndexOf("</title>", title_begin);

            string title = html.Substring(title_begin + 7, title_end - title_begin - 7);

            bool matched = false;
            int match = match_keyword(keyword, html);

            if(match <= 0)
            {
                return;
            }

            if(is_meticulous_)
            {  
                int match_title = match_meticulous_for_title(keyword, title);
                if (match_title > 0)
                {
                    matched = true;
                }
                else
                {
                    int match_m = match_meticulous(keyword, html);
                    if (match_m > 0)
                    {
                        matched = true;
                    }
                }  
            }
            else
            {
                matched = true;
            }

            if (matched)
            {
                AddListView(url, title);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string[] url = addrs_.ToArray();

            spyder_.MaxDepth = depth_;
            spyder_.MaxConnection = threads_;

            spyder_.Download(url);
        }

        private void init_listview()
        {
            this.listView1.Columns.Add("title", 300, HorizontalAlignment.Left);
            this.listView1.Columns.Add("address", 400, HorizontalAlignment.Left);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            spyder_.Abort();
        }

        private int match_keyword(string[] keywords, string content)
        {
            int mactchcount = 0;
            string regex = null;
            for (int i = 0; i < keywords.Length; ++i )
            {
                if(i == 0)
                    regex += "(" + keywords[i] + ")";
                else
                    regex += "|(" + keywords[i] + ")";
            }

            //Regex pattern = new Regex("<p.*?[^<a]+?.*?(" + regex + ")+?.*?[^a>]*?.*?/p>");"(?<=<p>).*?((投资)|(习近平))+?.*?(?=</p>)"
            Regex pattern = new Regex("(?<=<p).*?(" + regex + ")+?.*?(?=</p>)");
            //savetofile("", content);
            Match matchMode = pattern.Match(content);

            while (matchMode.Success)
            {
                matchMode = matchMode.NextMatch();
                mactchcount++;
            }

            return mactchcount;
        }

        private int match_meticulous_for_title(string[] keywords, string content)
        {
            int mactchcount = 0;
            string regex = null;
            for (int i = 0; i < keywords.Length; ++i)
            {
                if (i == 0)
                    regex += "(" + keywords[i] + ")";
                else
                    regex += "|(" + keywords[i] + ")";
            }

            //Regex pattern = new Regex("(<meta.*(" + regex + ")+?.*>)+?");"(?<=<p>).*?((投资)|(习近平))+?.*?(?=</p>)"
            Regex pattern = new Regex(regex);
            Match matchMode = pattern.Match(content);

            while (matchMode.Success)
            {
                matchMode = matchMode.NextMatch();
                mactchcount++;
            }

            return mactchcount;
        }

        private int match_meticulous(string[] keywords, string content)
        {
            int mactchcount = 0;
            string regex = null;
            for (int i = 0; i < keywords.Length; ++i)
            {
                if (i == 0)
                    regex += "(" + keywords[i] + ")";
                else
                    regex += "|(" + keywords[i] + ")";
            }

            //Regex pattern = new Regex("(<meta.*(" + regex + ")+?.*>)+?");"(?<=<p>).*?((投资)|(习近平))+?.*?(?=</p>)"
            Regex pattern = new Regex("(?<=<meta).*?(" + regex + ")+?.*?(?=>)");
            Match matchMode = pattern.Match(content);

            while (matchMode.Success)
            {
                matchMode = matchMode.NextMatch();
                mactchcount++;
            }

            return mactchcount;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            Form2 f = new Form2();
            f.View = this.listView2;
            f.Keyword = keywords_;

            f.type = "keyword";

            f.ShowDialog();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            System.Data.SQLite.SQLiteCommand cmd = new System.Data.SQLite.SQLiteCommand();
            cmd.Connection = sqlite_conn_;

            string sql = "delete from keyword";
            cmd.CommandText = sql;

            cmd.ExecuteNonQuery();

            foreach(string keyword in keywords_)
            {
                string s = "insert into keyword(word) values(\"" + keyword + "\")";
                cmd.CommandText = s;
                cmd.ExecuteNonQuery();
            }

            sql = "delete from address";
            cmd.CommandText = sql;

            cmd.ExecuteNonQuery();

            foreach(string addr in addrs_)
            {
                string s = "insert into address(addr) values(\"" + addr + "\")";
                cmd.CommandText = s;
                cmd.ExecuteNonQuery();
            }

            int meticulous = is_meticulous_ ? 1 : 0;

            sql = "update config set is_meticulous=" + meticulous + ", depth=" + depth_ + ", threads=" + threads_;
            cmd.CommandText = sql;

            cmd.ExecuteNonQuery();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            Form2 f = new Form2();
            f.View = this.listView3;
            f.Addresses = addrs_;
            f.type = "address";

            f.ShowDialog();
        }

        private void button7_Click(object sender, EventArgs e)
        {
           ListView.SelectedListViewItemCollection selectitmes = this.listView2.SelectedItems;
           
            foreach(ListViewItem item in selectitmes)
            {
                string text = item.Text;
                this.listView2.Items.Remove(item);
                keywords_.Remove(text);
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            button4_Click(sender, e);
        }

        private void savetofile(string filename, string txt)
        {
            if(filename == string.Empty)
            {
                filename = DateTime.Now.ToString("yyyyMMddHHmmss") + ".txt";
            }

            string path = basedir_ + filename;

            File.WriteAllText(path, txt, Encoding.UTF8);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            string htmlfilename = DateTime.Now.ToString("yyyyMMddHHmmss") + ".html";

            Dictionary<string, string> dic = new Dictionary<string, string>();
            string title = DateTime.Now.ToString("yyyyMMddHHmmss") + "搜索记录";

            string content = "<h2>" + title + "</h2>";
            content += "<table class=\"table table-striped\"><thead><tr><th>ID</th><th>title</th><th>address</th></tr></thead><tbody>$body$</tbody></table>";

            string body = "";
            int index = 1;
            foreach(ListViewItem item in listView1.Items)
            {
                string addr = item.Text;
                string desc = "";
                if(item.SubItems.Count > 1)
                    desc = item.SubItems[1].Text;

                string line = "<td>" + index + "</td><td>" + desc + "</td><td><a href=\"" + addr + "\" target=\"_blank\">" + addr + "</a></td>"; 

                body += "<tr>" + line + "</tr>";

                index++;
            }

            content = content.Replace("$body$", body);

            dic.Add("title", title);
            dic.Add("content", content);

            string error = "";
            bool res = savetohtml("template1", "", htmlfilename, dic, ref error);

            if(!res)
            {
                AddErrorLog(error);
                MessageBox.Show(error);
            }
            else
            {
                MessageBox.Show("save success!");
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            button3_Click(sender, e);
        }

        private void 系统设置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            button8_Click(sender, e);
        }

        void save_config(int max_connection, int max_depth, bool is_meticulous)
        {
            this.threads_ = max_connection;
            this.depth_ = max_depth;
            this.is_meticulous_ = is_meticulous;

            init_status_bar();
        }

        private void button8_Click(object sender, EventArgs e)
        {
            Form3 f = new Form3();
            f.max_connections = this.threads_;
            f.max_depth = this.depth_;
            f.is_meticulous = this.is_meticulous_;

            f.SaveConfig += new Form3.SaveConfigHandler(save_config);

            f.ShowDialog();
        }

        private bool savetohtml(string template, string path, string htmlname, Dictionary<string, string> dic, ref string message)
        {
             bool result = false;
             string templatepath = basedir_ + "template/" + template + ".html";
             string htmlpath = basedir_ + "output/";
             string htmlnamepath = Path.Combine(htmlpath, htmlname);
             Encoding encode = Encoding.UTF8;
             StringBuilder html = new StringBuilder();
 
             try
             {
                 //读取模版
                 html.Append(File.ReadAllText(templatepath, encode));
             }
             catch (FileNotFoundException ex)
             {
                 message = ex.Message;
                 return false;
             }
             catch(Exception e)
             {
                 message = e.Message;
                 return false;
             }
 
             foreach (KeyValuePair<string,string> d in dic)
             {
                 //替换数据
                 html.Replace(
                     string.Format("${0}$", d.Key),
                     d.Value);
             }
 
             try
             {
                 //写入html文件
                 if (!Directory.Exists(htmlpath))
                     Directory.CreateDirectory(htmlpath);

                 File.WriteAllText(htmlnamepath, html.ToString(), encode);
                 result = true;
             }
             catch (IOException ex)
             {
                 message = ex.Message;
                 return false;
             }
 
             return result;
         }

        private void button9_Click(object sender, EventArgs e)
        {
            ListView.SelectedListViewItemCollection selectitmes = this.listView3.SelectedItems;

            foreach (ListViewItem item in selectitmes)
            {
                string text = item.Text;
                this.listView3.Items.Remove(item);
                this.addrs_.Remove(text);
            }
        }
     
   }
}
