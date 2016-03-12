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
        public static string InternetURL = "http://8.8.8.8";
        public static int PingTimeout = 1000;
        public static int PageTimeout = 1000;
        public static int PingShortTimeout = 50;

        public NetworkMonitor()
        {

        }

        public static PingReply Ping(string url, int timeout = -1)
        {
            url = FormatURL(url);

            if (timeout == -1)
                timeout = PingTimeout;

            Ping ping = new Ping();
            Uri uri = new Uri(url);
            return new Ping().Send(uri.DnsSafeHost, PingTimeout);
        }

        public static bool TestWebPage(string url)
        {
            url = FormatURL(url);

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
            return 0 <= Ping(GetDefaultGateway() + "", PingShortTimeout).RoundtripTime;
        }

        public static string FormatURL(string url)
        {
            url = url.Trim();
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
