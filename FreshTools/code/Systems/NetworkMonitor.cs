using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;

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
        private string broadcastSSID = Environment.MachineName;
        private string broadcastPassword = "not_secure";

        private enum NetworkStatus
        {
            NoIntranet,
            NoInternet,
            AllGood,
        }

        public NetworkMonitor()
        {
            monitorThreads = new List<NetworkMonitorThread>();
        }

        public void LoadConfig(VariableLibrary vars)
        {
            vars.GetVariable("WifiBroadcastSSID", ref broadcastSSID, true);
            vars.GetVariable("WifiBroadcastPassword", ref broadcastPassword, true);
        }

        public void UpdateConfigVariables(VariableLibrary vars)
        {
            vars.GetVariable("WifiBroadcastSSID", broadcastSSID).SetValue(broadcastSSID);
            vars.GetVariable("WifiBroadcastPassword", broadcastPassword).SetValue(broadcastPassword);
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

        public void CreateWifiReport(object sender, EventArgs e)
        {
            Process p = new Process();
            p.StartInfo.FileName = "netsh";
            p.StartInfo.Arguments = "wlan show wlanReport";
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.Start();
            string err = p.StandardError.ReadToEnd();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            if (output == "Generating report ... failed, error is 0x5\r\nYou must run this command from a command prompt with administrator privilege. \r\n\r\n")
            {
                Microsoft.VisualBasic.Interaction.MsgBox("Report creation failed. Admin access is required.", Microsoft.VisualBasic.MsgBoxStyle.OkOnly, Application.ProductName + "Create Wifi Report");
            }
            else
            {
                Process.Start(new ProcessStartInfo(@"C:\ProgramData\Microsoft\Windows\WlanReport\wlan-report-latest.html"));
            }
        }

        public void StartHostedWifi(object sender, EventArgs e)
        {
            bool cont = true;
            bool valid = false;
            const string promptTitle = "Fresh Tools - Start Wifi";
            const string executionNote = "\n\nNote: this will execute: wlan set hostednetwork mode=allow ssid=SSID key=PASSWORD\nFollowed by a stop and start of the hosted network";

            //get SSID
            string input = "";
            while (!valid && cont)
            {
                input = Microsoft.VisualBasic.Interaction.InputBox("Enter an SSID for your wifi network" + executionNote, promptTitle, broadcastSSID).Trim();
                input = input.Trim();
                cont = input.Length > 0;
                valid = input.Length >= 3 && input.Length <= 32;
                valid = valid && input.IndexOf(" ") == -1;
                valid = valid && input.IndexOf("\t") == -1;
                valid = valid && input.IndexOf("\n") == -1;
                valid = valid && input.IndexOf("\"") == -1;
                valid = valid && input.IndexOf("\'") == -1;

                if(!valid && cont)
                    Microsoft.VisualBasic.Interaction.MsgBox("Invalid SSID", Microsoft.VisualBasic.MsgBoxStyle.OkOnly, promptTitle);
            }
            if (cont) broadcastSSID = input;
            valid = false;
            while (!valid && cont)
            {
                input = Microsoft.VisualBasic.Interaction.InputBox("Enter an password to secure your network" + executionNote, promptTitle, broadcastPassword).Trim();
                valid = input.Length >= 3 && input.Length <= 32;
                cont = input.Length > 0;
                valid = valid && input.IndexOf(" ") == -1;
                valid = valid && input.IndexOf("\t") == -1;
                valid = valid && input.IndexOf("\n") == -1;
                valid = valid && input.IndexOf("\"") == -1;
                valid = valid && input.IndexOf("\'") == -1;

                if (!valid && cont)
                    Microsoft.VisualBasic.Interaction.MsgBox("Invalid Password", Microsoft.VisualBasic.MsgBoxStyle.OkOnly, promptTitle);
            }
            if (cont) broadcastPassword = input;

            string output, err;
            FreshArchives.ExecuteCmdCommand("netsh", "wlan set hostednetwork mode=allow ssid=" + broadcastSSID + " key=" + broadcastPassword, out output, out err);
            FreshArchives.ExecuteCmdCommand("netsh", "wlan stop hostednetwork", out output, out err);
            FreshArchives.ExecuteCmdCommand("netsh", "wlan start hostednetwork", out output, out err);

            if (cont)
            {
                Microsoft.VisualBasic.Interaction.MsgBox("Started wifi. (SSID:'" + broadcastSSID + "' Password:'" + broadcastPassword + "').", Microsoft.VisualBasic.MsgBoxStyle.OkOnly, promptTitle);
                Log.I("Started Wifi");
            }
            else
            {
                Microsoft.VisualBasic.Interaction.MsgBox("Wifi not started.", Microsoft.VisualBasic.MsgBoxStyle.OkOnly, promptTitle);
            }
        }

        public void StopHostedWifi(object sender, EventArgs e)
        {
            string output, err;
            FreshArchives.ExecuteCmdCommand("netsh", "wlan stop hostednetwork", out output, out err);
            Log.I("Stopped Wifi");
        }

        /// <summary>
        /// Returns the status, ssid and password setup ti broadcast wifi from this machine.
        /// status(-1:not available, 0:Off, 1:Broadcasting).
        /// </summary>
        /// <param name="ssid"></param>
        /// <param name="pass"></param>
        /// <param name="status"></param>
        public void GetHostedWifiInfo(out string ssid, out string pass, out int status)
        {
            ssid = "";
            pass = "";
            string output, err;
            FreshArchives.ExecuteCmdCommand("netsh", "wlan show hostednetwork", out output, out err);
            //output = output.Replace(" ", "");

            if (output.IndexOf("Service (wlansvc) is not running.") != -1)
                status = -1;//not supported?
            else
            {
                if (output.IndexOf("\r\n    Status                 : Not started") != -1)
                    status = 0; //not running
                else
                    status = 1; //actively running

                int start = output.IndexOf("SSID name");
                start = output.IndexOf('"', start) + 1;
                ssid = output.Substring(start, output.IndexOf('"', start) - start);

                FreshArchives.ExecuteCmdCommand("netsh", "wlan show hostednetwork setting=security", out output, out err);

                start = output.IndexOf("User security key");
                start = output.IndexOf(':', start) + 2;
                pass = output.Substring(start, output.IndexOf('\r', start) - start);
            }
        }
        

        public void RunIPConfig(object sender, EventArgs e)
        {
            const string line_Break = "-------------------------------------------------------------------";
            List<string> commands = new List<string>();
            commands.Add("ipconfig");
            //commands.Add("ping 8.8.8.8");
            //commands.Add("tracert 8.8.8.8");
            commands.Add("ping -a -t 8.8.8.8");
            string args = "";
            foreach (string c in commands)
            {
                args += "&& echo " + line_Break;
                args += "&& echo " + c;
                args += "&& echo " + line_Break;
                args += "&& " + c;
            }
            args = args.Substring(3);//trim first &&s

            Process p = new Process();
            p.StartInfo.WorkingDirectory = @"C:\";
            p.StartInfo.FileName = "cmd";
            p.StartInfo.Arguments = "/K " + args;
            p.StartInfo.RedirectStandardInput = false;
            p.StartInfo.RedirectStandardOutput = false;
            p.StartInfo.UseShellExecute = true;
            p.StartInfo.CreateNoWindow = false;
            p.Start();

            Log.I("Started ping and IPconfig");
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
