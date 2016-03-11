using System;
using System.Data.SqlClient;
using System.Net;
using System.Windows.Forms;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace FreshTools
{
    class NetworkMonitor
    {
        public static int PingTimeout = 1000;
        public static int PageTimeout = 1000;

        public NetworkMonitor()
        {

        }

        public static long TestPing(string url, int timeout=-1)
        {
            url = url.Trim();
            if (url.IndexOf("http://") != 0) url = "http://" + url;

            if (timeout == -1)
                timeout = PingTimeout;

            Ping ping = new Ping();
            Uri uri = new Uri(url);
            PingReply reply = ping.Send(uri.DnsSafeHost, PingTimeout);
            if (reply.Status == IPStatus.Success)
                return reply.RoundtripTime;
            else
                return -1;
        }

        public static bool TestWebPage(string url)
        {
            url = url.Trim();
            if (url.IndexOf("http://") != 0) url = "http://" + url;

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

        public static long PingInternet()
        {
            return TestPing("http://8.8.8.8");
        }

        public static bool IsTheInternetUp()
        {
            return 0 <= TestPing("http://8.8.8.8");
        }

        public static bool IsTheIntranetUp()
        {
            return 0 <= TestPing(GetDefaultGateway()+"",50);
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
                if (i != null && i.OperationalStatus == OperationalStatus.Up)
                {
                    var address = i.GetIPProperties().GatewayAddresses[0];
                    return address.Address;
                }
            }
            return null;
        }
    }
}
