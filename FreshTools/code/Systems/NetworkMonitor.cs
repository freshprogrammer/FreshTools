using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace FreshTools
{
    public class NetworkMonitor
    {
        public const string LOG_TAG = "NetworkMonitor";
        
        //to disable logging every single test attampt and fail - non networking errors like bad URIs still reported
        private static bool pingLoggingEnabled = false;
        private static bool pageTestLoggingEnabled = false;

        public static string InternetURL = "http://8.8.8.8";
        public static int PingTimeout = 1000;
        public static int PageTimeout = 1000;
        public static int PingShortTimeout = 50;

        public bool IsRunning { get { return managerRunning; } set {} }

        public NotifyIcon NotifyIcon;
        
        private Thread managerThread;
        private bool managerRunning = false;
        private int managerThreadInterval = 1000;
        private NetworkStatus lastNetworkStatus = NetworkStatus.NoIntranet;
        
        private List<NetworkMonitorThread> monitorThreads;

        public NetworkMonitor()
        {
            monitorThreads = new List<NetworkMonitorThread>();
        }

        public void AddMonitor(string site, bool testPing, bool testWebPage)
        {
            monitorThreads.Add(new NetworkMonitorThread(site, testPing, testWebPage));
        }

        public void ToggleMonitoring(object sender, EventArgs e)
        {
            MenuItem i = (MenuItem)sender;
            if (managerRunning)
                StopMonitoring();
            else
                StartMonitoring();
            i.Checked = managerRunning;
        }

        public void StartMonitoring()
        {
            managerThread = new Thread(new ThreadStart(ManagerRun));
            managerThread.Name = "Networkonitor Manager";
            managerThread.Start();

            foreach (NetworkMonitorThread t in monitorThreads)
            {
                t.Start();
            }
            Log.I("Started Sucsessfully ("+monitorThreads.Count+") monitors", LOG_TAG);
        }

        public void StopMonitoring()
        {
            managerRunning = false;
            managerThread = null;
            foreach (NetworkMonitorThread t in monitorThreads)
            {
                t.Stop();
            }
            Log.I("Stopped Sucsessfully (" + monitorThreads.Count + ") monitors", LOG_TAG);
        }

        private void ManagerRun()
        {
            managerRunning = true;
            while (managerRunning)
            {
                NetworkStatus currentNetStat = GetNetworkstatus();
                if (currentNetStat != lastNetworkStatus)
                {
                    //status chaged
                    Log.W("Network status changed - now " + currentNetStat, LOG_TAG);
                }
                lastNetworkStatus = currentNetStat;

                try
                {
                    Thread.Sleep(managerThreadInterval);
                }
                catch(ThreadInterruptedException)
                {
                    //manager thred stoped
                }
            }
        }

        private static NetworkStatus GetNetworkstatus()
        {
            if (IsTheInternetUp())
                return NetworkStatus.AllGood;
            else if (IsTheGatewayUp())
                return NetworkStatus.NoInternet;
            else
                return NetworkStatus.NoIntranet;
        }


        public void RunIPConfig(object sender, EventArgs e)
        {
            TestCode();
        }

        public static void TestCode()
        {
            //netMan.TestWriteToDB();
            Log.V("NetworkMonitor.IsTheIntranetUp() - " + NetworkMonitor.IsTheGatewayUp(), LOG_TAG);
            Log.V("NetworkMonitor.IsTheInternetUp() - " + NetworkMonitor.IsTheInternetUp(), LOG_TAG);

            //NetworkMonitorThread t1 = new NetworkMonitorThread(NetworkMonitor.GetLocalIPAddress() + "", true, false, this);
            //NetworkMonitorThread t2 = new NetworkMonitorThread(NetworkMonitor.GetDefaultGateway() + "", true, false, this);
            //NetworkMonitorThread t3 = new NetworkMonitorThread("www.google.com", true, true, this);

            //t1.Start();
            //t2.Start();
            //t3.Start();

            /*
            Ping("1.2.3.4");
            Ping(NetworkMonitor.GetLocalIPAddress() + "");
            Ping(NetworkMonitor.GetDefaultGateway() + "");
            Ping("http://www.checkupdown.com/accounts/grpb/B1394343/");
            Ping("www.gooogle.com");
            Ping("8.8.8.8");
            Ping("209.164.2.138");
            Ping("freshprogramming.com");

            PingAsync("1.2.3.4");
            PingAsync(NetworkMonitor.GetLocalIPAddress() + "");
            PingAsync(NetworkMonitor.GetDefaultGateway() + "");
            PingAsync("http://www.checkupdown.com/accounts/grpb/B1394343/");
            PingAsync("www.gooogle.com");
            PingAsync("8.8.8.8");
            PingAsync("209.164.2.138");
            PingAsync("freshprogramming.com");
            PingAsync("freshDistraction.com");
            PingAsync("freshGifs.com");

            TestWebPage("1.2.3.4");
            TestWebPage(NetworkMonitor.GetLocalIPAddress() + "");
            TestWebPage(NetworkMonitor.GetDefaultGateway() + "");
            TestWebPage("http://www.checkupdown.com/accounts/grpb/B1394343/");
            TestWebPage("www.gooogle.com");
            TestWebPage("8.8.8.8");
            TestWebPage("209.164.2.138");
            TestWebPage("freshprogramming.com");
             */
        }

        private static void PingCompleted(object o, PingCompletedEventArgs args)
        {
            if (args.Error != null)
                Log.E("PingCompleted() - Error - " + args.Error.Message, LOG_TAG);//no idea what site failed
            else
                Log.I("PingCompleted() - '" + args.Reply.Address + "' - " + args.Reply.Status + " - " + args.Reply.RoundtripTime, LOG_TAG);
        }

        public static PingReply Ping(string url, int timeout = -1)
        {
            url = FormatURL(url);
            if (url == null || url.Length==0)
            {
                Log.E("Ping('" + url + "') - Failed - invalid url", LOG_TAG);
                return null;
            }

            if (timeout == -1)
                timeout = PingTimeout;

            Uri uri = new Uri(url);
            try
            {
                PingReply pr = new Ping().Send(uri.DnsSafeHost, PingTimeout);
                if (pingLoggingEnabled) Log.V("Ping('" + url + "') - succeeded", LOG_TAG);
                return pr;
            }
            catch (PingException)
            {
                if (pingLoggingEnabled) Log.E("Ping('" + url + "') - Failed - PingException", LOG_TAG);
                return null;
            }
        }

        /*private static void PingAsync(string url, int timeout = -1)
        {
            //LogSystem.Log("PingAsync('" + url + "')");

            url = FormatURL(url);
            if (url == null || url.Length == 0)
            {
                LogSystem.Log("PingAsync('" + url + "') - Failed - invalid url");
                return;
            }

            if (timeout == -1)
                timeout = PingTimeout;

            Uri uri = new Uri(url);
            AutoResetEvent waiter = new AutoResetEvent(false);
            Ping ping = new Ping();
            ping.PingCompleted += PingCompleted;
            ping.SendAsync(uri.DnsSafeHost, PingTimeout, waiter);
        }*/

        public static bool TestWebPage(string url)
        {
            url = FormatURL(url);
            if (url == null || url.Length == 0)
            {
                if (pageTestLoggingEnabled) Log.E("TestWebPage('" + url + "') - Failed - invalid url", LOG_TAG);
                return false;
            }

            bool pageExists = false;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = WebRequestMethods.Http.Head;
                request.Timeout = PageTimeout;

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    pageExists = response.StatusCode == HttpStatusCode.OK;
                    if (pageTestLoggingEnabled) Log.V("TestWebPage('" + url + "') - " + response.StatusCode, LOG_TAG);
                }
            }
            catch (UriFormatException e)
            {
                Log.E("TestWebPage('" + url + "') - UriFormatException-" + e.Message, LOG_TAG);
            }
            catch (WebException e)
            {
                if (e.Message.IndexOf("(403)") != -1)
                {
                    Log.E("TestWebPage('" + url + "') - WebException-Forbidden-" + e.Message, LOG_TAG);
                }
                else
                {
                    //timed out
                    if (pageTestLoggingEnabled) Log.W("TestWebPage('" + url + "') - WebException-" + e.Message, LOG_TAG);
                }
            }

            return pageExists;
        }

        public void TestWriteToDB()
        {
            using (SqlConnection conn = new SqlConnection())
            {
                conn.ConnectionString = "Server=[server_name];Database=[database_name];Trusted_Connection=true";
                conn.Open();

                // Create the command
                SqlCommand command = new SqlCommand("SELECT * FROM TableName WHERE FirstColumn = @firstColumnValue", conn);
                // Add the parameters.
                command.Parameters.Add(new SqlParameter("firstColumnValue", 1));

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    // while there is another record present
                    while (reader.Read())
                    {
                        // write the data on to the screen
                        Console.WriteLine(String.Format("{0} \t | {1} \t | {2} \t | {3}",
                            // call the objects from their index
                        reader[0], reader[1], reader[2], reader[3]));
                    }
                }
            }
        }

        public static bool IsTheInternetUp()
        {
            PingReply pr = Ping(InternetURL);
            if (pr != null)
                return pr.Status == IPStatus.Success;
            else return false;
        }

        public static bool IsTheGatewayUp()
        {
            IPAddress gateway = GetDefaultGateway();
            if (gateway == null)
                return false;
            PingReply pr = Ping(gateway.ToString(), PingShortTimeout);
            if (pr != null)
                return pr.Status == IPStatus.Success;
            else return false;
        }

        public static string FormatURL(string url)
        {
            url = url.Trim();
            if (url.Length == 0)
                return null;
            if (url.IndexOf("http://") != 0)
                url = "http://" + url;
            return url;
        }

        public static IPAddress GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip;
                }
            }
            return null;
        }

        public static IPAddress GetDefaultGateway()
        {
            foreach (NetworkInterface i in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (i != null && i.OperationalStatus == OperationalStatus.Up && i.GetIPProperties().GatewayAddresses.Count > 0)
                {
                    GatewayIPAddressInformation address = i.GetIPProperties().GatewayAddresses[0];
                    return address.Address;
                }
            }
            return null;
        }

        private enum NetworkStatus
        {
            NoIntranet,
            NoInternet,
            AllGood,
        }
    }

    public class NetworkMonitorThread
    {
        private Thread testThread;
        private String site;
        private bool testPing;
        private bool testWebPage;

        //These dont fully switch over untill the minimum limits are reached
        private bool sitePings = false;
        private bool sitePageReturns = false;
        private DateTime lastPingDownTime;
        private DateTime lastPageDownTime;
        private TimeSpan lastTotalPageUpTimeSpan;
        private TimeSpan lastTotalPingUpTimeSpan;
        private TimeSpan lastTotalPageDownTimeSpan;
        private TimeSpan lastTotalPingDownTimeSpan;

        public bool Running;
        public int TestInterval = 5000;
        public int AcceptableDownTime = 10 * 1000;
        public int MinimumUpTime = 0;


        public NetworkMonitorThread(string site, bool testPing, bool testWebPage)
        {
            this.testPing = testPing;
            this.testWebPage = testWebPage;
            this.site = site;
            Running = false;
            lastPingDownTime = DateTime.Now;
            lastPageDownTime = DateTime.Now;
            lastTotalPageUpTimeSpan = TimeSpan.Zero;
            lastTotalPingUpTimeSpan = TimeSpan.Zero;
            lastTotalPageDownTimeSpan = TimeSpan.Zero;
            lastTotalPingDownTimeSpan = TimeSpan.Zero;
        }

        public void Run()
        {
            try
            {
                while (Running)
                {
                    if (testPing)
                    {
                        PingReply pingReply = NetworkMonitor.Ping(site);
                        bool sitePingsNow = pingReply != null && pingReply.Status == IPStatus.Success;
                        if (sitePingsNow != sitePings)
                        {
                            //status changed - report it
                            sitePings = sitePingsNow;
                            ReportPingStatusChange(sitePageReturns);
                        }
                        if(sitePings)
                            lastPingDownTime = DateTime.Now;
                    }
                    if (testWebPage)
                    {
                        //////////////////////////////////////////All this code is prototyped - nothign tested
                        bool sitePageReturnsNow = NetworkMonitor.TestWebPage(site);
                        if (sitePageReturnsNow)
                        {
                            TimeSpan upTime = DateTime.Now - lastPageDownTime;
                            if (sitePageReturnsNow != sitePageReturns && upTime.TotalMilliseconds > MinimumUpTime)
                            {//log site page returns now
                                sitePageReturns = sitePageReturnsNow;
                                lastTotalPageDownTimeSpan = DateTime.Now - lastPageDownTime;
                                lastPageDownTime = DateTime.Now;
                                ReportPageStatusChange(true);
                            }
                        }
                        else
                        {
                            TimeSpan downTime = DateTime.Now - lastPageDownTime;
                            if (downTime.TotalMilliseconds >= AcceptableDownTime)
                            {
                                ReportPageStatusChange(false);
                            }
                        }

                        ///end test
                    }

                    //LogSystem.Log("("+sitePings+","+sitePageReturns+") from "+site);
                    Thread.Sleep(TestInterval);
                }
            }
            catch (ThreadAbortException)
            {

            }
            finally
            {

            }
        }

        private void ReportPingStatusChange(bool upNow)
        {
            Log.V(site + " ping now = " + upNow, NetworkMonitor.LOG_TAG);
        }

        private void ReportPageStatusChange(bool upNow)
        {
            Log.V(site + " page up now = " + upNow, NetworkMonitor.LOG_TAG);
        }

        public void Start()
        {
            Running = true;
            testThread = new Thread(Run);
            testThread.Start();
            testThread.Name = "NetworkMonitorThread(" + site + ")";
            testThread.Priority = ThreadPriority.BelowNormal;
        }

        public void Stop()
        {
            Running = false;
            testThread.Abort();
            testThread = null;
        }
    }
}
