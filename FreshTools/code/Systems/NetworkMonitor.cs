using System;
using System.Data.SqlClient;
using System.Net;
using System.Windows.Forms;

namespace FreshTools
{
    class NetworkMonitor
    {
        public NetworkMonitor()
        {

        }

        public void TestPing()
        {

        }

        public bool TestWebPage(string url)
        {
            url = url.Trim();
            if (url.IndexOf("http://") != 0) url = "http://" + url;

            bool pageExists = false;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = WebRequestMethods.Http.Head;
                request.Timeout = 2000;
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                pageExists = response.StatusCode == HttpStatusCode.OK;
                LogSystem.Log("TestWebPage('" + url + "') - " + response.StatusCode);
            }
            catch (WebException e)
            {
                LogSystem.Log("TestWebPage('" + url + "') - WebException-" + e.Message);
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
    }
}
