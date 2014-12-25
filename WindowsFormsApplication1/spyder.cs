using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsFormsApplication1
{
    class spyder
    {
        private class RequestState
        {
            private const int BUFFER_SIZE = 131072;
            private byte[] _data = new byte[BUFFER_SIZE];
            private StringBuilder _sb = new StringBuilder();

            public HttpWebRequest Req { get; private set; }
            public string Url { get; private set; }
            public int Depth { get; private set; }
            public int Index { get; private set; }
            public Stream ResStream { get; set; }
            public StringBuilder Html
            {
                get
                {
                    return _sb;
                }
            }

            public byte[] Data
            {
                get
                {
                    return _data;
                }
            }

            public int BufferSize
            {
                get
                {
                    return BUFFER_SIZE;
                }
            }

            public RequestState(HttpWebRequest req, string url, int depth, int index)
            {
                Req = req;
                Url = url;
                Depth = depth;
                Index = index;
            }

            public ManualResetEvent EvtHandle { get; set; }
        }

        private class WorkingUnitCollection
        {
            private int _count;
            //private AutoResetEvent[] _works;
            private bool[] _busy;

            public WorkingUnitCollection(int count)
            {
                _count = count;
                //_works = new AutoResetEvent[count];
                _busy = new bool[count];

                for (int i = 0; i < count; i++)
                {
                    //_works[i] = new AutoResetEvent(true);
                    _busy[i] = true;
                }
            }

            public void StartWorking(int index)
            {
                if (!_busy[index])
                {
                    _busy[index] = true;
                    //_works[index].Reset();
                }
            }

            public void FinishWorking(int index)
            {
                if (_busy[index])
                {
                    _busy[index] = false;
                    //_works[index].Set();
                }
            }

            public bool IsFinished()
            {
                bool notEnd = false;
                foreach (var b in _busy)
                {
                    notEnd |= b;
                }
                return !notEnd;
            }

            public void WaitAllFinished()
            {
                while (true)
                {
                    if (IsFinished())
                    {
                        break;
                    }
                    Thread.Sleep(1000);
                }
                //WaitHandle.WaitAll(_works);
            }

            public void AbortAllWork()
            {
                for (int i = 0; i < _count; i++)
                {
                    _busy[i] = false;
                }
            }
        }

        private readonly object _locker = new object();
        private Dictionary<string, int> _urlsLoaded = new Dictionary<string, int>();
        private Dictionary<string, int> _urlsUnload = new Dictionary<string, int>();

        private List<string> _rootUrl = null;
        private List<string> _baseUrl = null;

        private static Encoding GB18030 = Encoding.GetEncoding("GB18030");   // GB18030兼容GBK和GB2312
        private static Encoding UTF8 = Encoding.UTF8;
        private string _userAgent = "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; Trident/4.0)";
        private string _accept = "text/html";
        private string _method = "GET";
        private Encoding _encoding = GB18030;
        private Encodings _enc = Encodings.GB;
        private int _maxTime = 1 * 60 * 1000;

        private int _maxDepth = 2;
        //private int _maxExternalDepth = 0;

        private string[] _urlreadys = null;
        private bool _stop = true;
        private Timer _checkTimer = null;
        private bool[] _reqsBusy = null;
        private int _reqCount = 4;
        private WorkingUnitCollection _workingSignals;

        private int _index;

        public delegate void DownloadFinishHandler(int count);
        public event DownloadFinishHandler DownloadFinish = null;

        public delegate void ContentsSavedHandler(string path, string url);
        public event ContentsSavedHandler ContentsSaved = null;

        public delegate void ErrorMessageHandler(string error);
        public event ErrorMessageHandler ErrorMessage = null;

        public delegate void SaveContentsHandler(string html, string url);
        public event SaveContentsHandler SaveContents = null;

        public delegate void ShowStatusHandler(int index, string url);
        public event ShowStatusHandler ShowStatus = null;

        //connection delegate
        public delegate void dispatch_fix_handler(int index);

        public enum Encodings
        {
            UTF8,
            GB
        }

        public Encodings PageEncoding
        {
            get
            {
                return _enc;
            }
            set
            {
                _enc = value;
                switch (value)
                {
                    case Encodings.GB:
                        _encoding = GB18030;
                        break;
                    case Encodings.UTF8:
                        _encoding = UTF8;
                        break;
                }
            }
        }

        public int MaxDepth
        {
            get
            {
                return _maxDepth;
            }
            set
            {
                _maxDepth = Math.Max(value, 1);
            }
        }

        public int MaxConnection
        {
            get
            {
                return _reqCount;
            }
            set
            {
                _reqCount = value;
            }
        }

        public void Download(string[] paths)
        {
            if(paths == null || paths.Length == 0)
            {
                return;
            }
            _urlreadys = paths;
            Init();
            StartDownload();
        }

        public void Abort()
        {
            _stop = true;
            if (_workingSignals != null)
            {
                _workingSignals.AbortAllWork();
            }
        }

        private void Init()
        {
            _urlsLoaded.Clear();
            _urlsUnload.Clear();

            _rootUrl = new List<string>();
            _baseUrl = new List<string>();

            AddUrls(_urlreadys, 0);
            _index = 0;
            _reqsBusy = new bool[_reqCount];
            _workingSignals = new WorkingUnitCollection(_reqCount);
            _stop = false;
        }

        private bool UrlExists(string url)
        {
            lock (_locker)
            {
                bool result = _urlsUnload.ContainsKey(url);
                result |= _urlsLoaded.ContainsKey(url);
                return result;
            }
        }

        private bool UrlAvailable(string url)
        {
            if (UrlExists(url))
            {
                return false;
            }
            if (url.Contains(".jpg") || url.Contains(".gif")
                || url.Contains(".png") || url.Contains(".css")
                || url.Contains(".js"))
            {
                return false;
            }
            return true;
        }

        private void AddUrls(string[] urls, int depth)
        {
            if (urls == null)
                return;

            if (depth >= _maxDepth)
            {
                return;
            }

            if(depth == 0)
            {
                foreach(string u in urls)
                {
                    string temp = u;
                    if (!u.Contains("http://"))
                    {
                        temp = "http://" + u;
                    }

                    _rootUrl.Add(temp);

                    string baseurl = temp.Replace("www.", "");
                    baseurl = baseurl.Replace("http://", "");
                    baseurl = baseurl.TrimEnd('/');

                    _baseUrl.Add(baseurl);
                }
            }

            foreach (string url in urls)
            {
                string cleanUrl = url.Trim();
                int end = cleanUrl.IndexOf(' ');
                if (end > 0)
                {
                    cleanUrl = cleanUrl.Substring(0, end);
                }
                cleanUrl = cleanUrl.TrimEnd('/');
                if (UrlAvailable(cleanUrl))
                {
                    if (match_baseurl(cleanUrl))
                    {
                        lock (_locker)
                        {
                            _urlsUnload.Add(cleanUrl, depth);
                        }
                    }
                    else
                    {
                        // 外链
                    }
                }
            }
        }

        private bool match_baseurl(string url)
        {
            foreach(string baseurl in _baseUrl)
            {
                if (url.Contains(baseurl))
                    return true;
            }

            return false;
        }

        private void CheckFinish(object param)
        {
            if (_workingSignals.IsFinished())
            {
                if (_urlsUnload.Count > 0 && !_stop)
                {
                    DispatchWork();
                    return;
                }

                if (_checkTimer != null)
                    _checkTimer.Dispose();

                _checkTimer = null;
                if (DownloadFinish != null)
                {
                    DownloadFinish(_index);
                }
            }
        }

        private void StartDownload()
        {
            _checkTimer = new Timer(new TimerCallback(CheckFinish), null, 0, 500);
            DispatchWork();
        }

        private void DispatchWork()
        {
            if (_stop)
            {
                return;
            }
                        
            for (int i = 0; i < _reqCount; i++)
            {
                bool busy = true;
                lock (_locker)
                {                
                    busy = _reqsBusy[i];

                    if(!busy)
                        _reqsBusy[i] = true;
                }

                if (!busy)
                {
                    RequestResource(i);
                }
                
            }
        }

        private void DispatchWorkFix(int index)
        {
            if(index >= _reqCount)
            {
                return;
            }

            bool busy = true;

            lock (_locker)
            {
                busy = _reqsBusy[index];

                if (!busy)
                    _reqsBusy[index] = true;
            }

            if(!busy)
            {
                RequestResource(index);
            }
        }

        private void DispatchWorkWait(IAsyncResult ar)
        {
            dispatch_fix_handler dn = (dispatch_fix_handler)ar.AsyncState;

            System.Threading.Thread.Sleep(1000);

            dn.EndInvoke(ar);
        }

        private void ErrorMessageOccur(string error)
        {
            if (ErrorMessage != null)
            {
                ErrorMessage(error);
            }
        }

        private void RequestResource(int index)
        {
            int depth;
            string url = "";
            try
            {
                lock (_locker)
                {
                    if (_urlsUnload.Count <= 0 || _stop)
                    {
                        _workingSignals.FinishWorking(index);

                        if (!_workingSignals.IsFinished())
                        {
                            dispatch_fix_handler fix_handler = new dispatch_fix_handler(DispatchWorkFix);
                            AsyncCallback dispatch_handler = new AsyncCallback(DispatchWorkWait);

                            _reqsBusy[index] = false;

                            fix_handler.BeginInvoke(index, dispatch_handler, fix_handler);
                        }

                        return;
                    }
                    _reqsBusy[index] = true;
                    _workingSignals.StartWorking(index);
                    depth = _urlsUnload.First().Value;
                    url = _urlsUnload.First().Key;
                    _urlsUnload.Remove(url);

                    if(!_urlsLoaded.ContainsKey(url))
                        _urlsLoaded.Add(url, depth);
                    else
                    {
                        ErrorMessageOccur("RequestResource exsit key:" + url);
                    }
                }

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = _method; //请求方法
                req.Accept = _accept; //接受的内容
                req.UserAgent = _userAgent; //用户代理
                req.KeepAlive = false;
                req.Timeout = System.Threading.Timeout.Infinite;
                req.ProtocolVersion = HttpVersion.Version10;

                RequestState rs = new RequestState(req, url, depth, index);
                rs.EvtHandle = new ManualResetEvent(false);

                //var result = req.BeginGetResponse(new AsyncCallback(ReceivedResource), rs);
                /*
                 * ThreadPool.RegisterWaitForSingleObject(result.AsyncWaitHandle,
                        TimeoutCallback, rs, _maxTime, true);
                */

                ThreadPool.QueueUserWorkItem(new WaitCallback(ReceivedResource), rs);

                //rs.EvtHandle.WaitOne();

                return;
            }
            catch (WebException we)
            {
                //MessageBox.Show("RequestResource " + we.Message + url + we.Status);
                ErrorMessageOccur("RequestResource " + we.Message + url + we.Status);
            }
            catch (Exception e)
            {
                //MessageBox.Show(e.Message);
                ErrorMessageOccur("RequestResource " + e.Message + url);
            }

            lock (_locker)
            {
                _reqsBusy[index] = false;
                ErrorMessageOccur("stop index:" + index);
            }

            DispatchWorkFix(index);
        }

        //private void ReceivedResource(IAsyncResult ar)
        private void ReceivedResource(object ar)
        {
            //RequestState rs = (RequestState)ar.AsyncState;
            RequestState rs = (RequestState)ar;
            HttpWebRequest req = rs.Req;
            string url = rs.Url;
            HttpWebResponse res = null;
            int depth = rs.Depth;

            string[] links = null;

            try
            {
                //res = (HttpWebResponse)req.EndGetResponse(ar);
                res = (HttpWebResponse)req.GetResponse();

                if (_stop)
                {
                    res.Close();
                    req.Abort();
                    lock (_locker)
                    {
                        _reqsBusy[rs.Index] = false;
                    }
                    ErrorMessageOccur("ReceivedResource stop index:" + rs.Index);
                    //rs.EvtHandle.Set();
                    return;
                }

                if (res != null && res.StatusCode == HttpStatusCode.OK)
                {
                    Stream resStream = res.GetResponseStream();
                    /*
                    rs.ResStream = resStream;
                    var result = resStream.BeginRead(rs.Data, 0, rs.BufferSize,
                        new AsyncCallback(ReceivedData), rs);

                    rs.EvtHandle.WaitOne();
                    return;
                    */

                    StreamReader streamReader = new StreamReader(resStream);
                    string html = streamReader.ReadToEnd();

                    if (SaveContents != null)
                        SaveContents(html, url);

                    ShowStatus(++_index, url);

                    if (html != null)
                    {
                        links = GetLinks(html, url);

                        if (links.Length > 0)
                        {
                            AddUrls(links, depth + 1);
                        }
                    }
                }
            }
            catch (WebException we)
            {
                ErrorMessageOccur("ReceivedResource " + we.Message + url + we.Status);
            }
            catch (Exception e)
            {
                ErrorMessageOccur("ReceivedResource " + e.Message);
            }

            lock (_locker)
            {
                _reqsBusy[rs.Index] = false;
                //ErrorMessageOccur("stop index:" + rs.Index);
                //_workingSignals.FinishWorking(rs.Index);
            }

            if (res != null)
                res.Close();

            rs.Req.Abort();

            //rs.EvtHandle.Set();

            DispatchWorkFix(rs.Index);
        }

        private void ReceivedData(IAsyncResult ar)
        {
            RequestState rs = (RequestState)ar.AsyncState;
            HttpWebRequest req = rs.Req;
            Stream resStream = rs.ResStream;
            string url = rs.Url;
            int depth = rs.Depth;
            string html = null;
            int index = rs.Index;
            int read = 0;
            //Task<int> task_read = null;

            try
            {
                read = resStream.EndRead(ar);
                //task_read = resStream.ReadAsync(rs.Data, 0, rs.BufferSize);

                if (_stop)
                {
                    rs.ResStream.Close();
                    req.Abort();
                    lock (_locker)
                    {
                        _reqsBusy[rs.Index] = false;
                    }
                    ErrorMessageOccur("ReceivedData stop index:" + rs.Index);
                    return;
                }

                if (read > 0)
                {
                    MemoryStream ms = new MemoryStream(rs.Data, 0, read);
                    StreamReader reader = new StreamReader(ms, _encoding);
                    
                    string str = reader.ReadToEnd();
                    rs.Html.Append(str);

                    Array.Clear(rs.Data, 0, rs.BufferSize);

                    var result = resStream.BeginRead(rs.Data, 0, rs.BufferSize,
                        new AsyncCallback(ReceivedData), rs);

                    return;
                }


                html = rs.Html.ToString();

                if(SaveContents != null)
                    SaveContents(html, url);

                ShowStatus(++_index,url);

                if (html != null)
                {
                    string[] links = GetLinks(html, url);
                    AddUrls(links, depth + 1);
                }
            }
            catch (WebException we)
            {
                //MessageBox.Show("ReceivedData Web " + we.Message + url + we.Status);
                ErrorMessageOccur("ReceivedData Web " + we.Message + url + we.Status);
            }
            catch (Exception e)
            {
                //MessageBox.Show(e.GetType().ToString() + e.Message);
                ErrorMessageOccur("ReceivedData Web " + e.GetType().ToString() + e.Message);
            }

            lock (_locker)
            {
                _reqsBusy[rs.Index] = false;
                //_workingSignals.FinishWorking(index);
            }

            //rs.EvtHandle.Set();

            DispatchWorkFix(rs.Index);
        }

        private string[] GetLinks(string html, string url)
        {
            /*
            const string pattern = @"http://([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?";
            Regex r = new Regex(pattern, RegexOptions.IgnoreCase);
            MatchCollection m = r.Matches(html);
            string[] links = new string[m.Count];

            for (int i = 0; i < m.Count; i++)
            {
                links[i] = m[i].ToString();
            }
            */

            Uri u = new Uri(url);
            string sHost = u.Host;
           
            /*string[] host_splits = sHost.Split('.');
            if (host_splits == null)
            {
                return null;
            }

            if (host_splits.Length > 2)
            {
                domain = host_splits[1] + "." + host_splits[2];
            }
            else if (host_splits.Length > 1)
            {
                domain = host_splits[1];
            }
            */
            string domain = "";
            domain = sHost.Replace("www.", "");
            int domain_end = domain.IndexOf("/");
            
            if(domain_end > 0)
                domain = domain.Substring(0, domain_end);

            domain = domain.Trim();
            
            Regex reg = new Regex(@"(?is)<a[^>]*?href=(['""\s]?)(?<href>[^'""\s]*)\1[^>]*?>");
            MatchCollection match = reg.Matches(html);
            string[] links = null; 
            
            if(match.Count > 0)
                links = new string[match.Count];

            for(int i = 0; i < match.Count; i++)
            {
                //System.Console.WriteLine(m.Groups["href"].Value);
                string href = match[i].Groups["href"].Value;

                if( !href.Contains(domain) && !href.Contains("http://") )
                {
                    href = href.TrimStart('/');
                    href = "http://" + sHost + "/" + href;
                }

                links[i] = href;
            }

            return links;
        }

        private void TimeoutCallback(object state, bool timedOut)
        {
            RequestState rs = state as RequestState;
            //ErrorMessageOccur("timeoutcallback:" + timedOut + " " + rs.Index);
            if (timedOut)
            {
                if (rs != null)
                {
                    rs.Req.Abort();
                }
                lock (_locker)
                {
                    _reqsBusy[rs.Index] = false;
                }
                //rs.EvtHandle.Set();
                DispatchWorkFix(rs.Index);
            }
        }
    }
}
