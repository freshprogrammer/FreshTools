using System;
using System.Data.SqlClient;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace FreshTools
{
    class NetworkMonitor
    {
        public static string InternetURL = "http://8.8.8.8";
        public static int PingTimeout = 1000;
        public static int PageTimeout = 1000;
        public static int PingShortTimeout = 50;

        static NetworkMonitor()
        {

        }

        public NetworkMonitor()
        {

        }

        public static void TestCode()
        {
            //netMan.TestWriteToDB();
            LogSystem.Log("NetworkMonitor.IsTheIntranetUp() - " + NetworkMonitor.IsTheGatewayUp());
            LogSystem.Log("NetworkMonitor.IsTheInternetUp() - " + NetworkMonitor.IsTheInternetUp());

            new WebTestThread("www.google.com", true, true);

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
        }

        private static void PingCompleted(object o, PingCompletedEventArgs args)
        {
            if (args.Error != null)
                LogSystem.Log("PingCompleted() - Error - " + args.Error.Message);//no idea what site failed
            else
                LogSystem.Log("PingCompleted() - '" + args.Reply.Address + "' - " + args.Reply.Status + " - " + args.Reply.RoundtripTime);
        }

        public static PingReply Ping(string url, int timeout = -1)
        {
            LogSystem.Log("Ping('" + url + "')");

            url = FormatURL(url);
            if (url == null || url.Length==0)
            {
                LogSystem.Log("Ping('" + url + "') - Failed - invalid url");
                return null;
            }

            if (timeout == -1)
                timeout = PingTimeout;

            Uri uri = new Uri(url);
            try
            {
                return new Ping().Send(uri.DnsSafeHost, PingTimeout);
            }
            catch (PingException e)
            {
                return null;
            }
        }

        private static void PingAsync(string url, int timeout = -1)
        {
            LogSystem.Log("PingAsync('" + url + "')");

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
        }

        public static bool TestWebPage(string url)
        {
            url = FormatURL(url);
            if (url == null || url.Length == 0)
            {
                LogSystem.Log("TestWebPage('" + url + "') - Failed - invalid url");
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
                    LogSystem.Log("TestWebPage('" + url + "') - " + response.StatusCode);
                }
            }
            catch (UriFormatException e)
            {
                LogSystem.Log("TestWebPage('" + url + "') - UriFormatException-" + e.Message);
            }
            catch (WebException e)
            {
                if (e.Message.IndexOf("(403)") != -1)
                {
                    LogSystem.Log("TestWebPage('" + url + "') - WebException-Forbidden-" + e.Message);
                }
                else
                {
                    //timed out
                    LogSystem.Log("TestWebPage('" + url + "') - WebException-" + e.Message);
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

        public static PingReply PingInternet()
        {
            return Ping(InternetURL);
        }

        public static bool IsTheInternetUp()
        {
            return 0 <= Ping(InternetURL).RoundtripTime;
        }

        public static bool IsTheGatewayUp()
        {
            IPAddress gateway = GetDefaultGateway();
            if (gateway == null)
                return false;
            return 0 <= Ping(gateway + "", PingShortTimeout).RoundtripTime;
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

    public class WebTestThread
    {
        private Thread checkThread;
        private String testSite;
        private bool testPing;
        private bool testWebPage;
        public bool Running;
        public int testInterval = 2000;


        public WebTestThread(string site, bool testPing, bool testWebPage)
        {
            this.testPing = testPing;
            this.testWebPage = testWebPage;
            testSite = site;
            Running = false;

            checkThread = new Thread(Run);
            checkThread.Start();
        }

        public void Run()
        {
            Thread.CurrentThread.Name = "WebTestThread("+testSite+")";
            Running = true;

            try
            {
                while (Running)
                {
                    LogSystem.Log("running");
                    NetworkMonitor.TestWebPage(testSite);

                    Thread.Sleep(testInterval);
                }
            }
            catch (ThreadInterruptedException)
            {

            }
            finally
            {

            }
        }
    }
}
