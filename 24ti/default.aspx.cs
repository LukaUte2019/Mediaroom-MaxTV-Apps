using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Web;
using System.Web.UI;

public partial class _Default1 : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        Response.Clear();
        Response.Cache.SetCacheability(HttpCacheability.NoCache);
        Response.Cache.SetNoStore();

        string backendHost = "172.16.40.101";        // IPTV / Mediaroom backend
        string backendPath = "/24ti/default.aspx";   // MRML source
        int backendPort = 80;

        string eth7IP = GetEthernet7IP();           // Get IP of Ethernet 7

        try
        {
            // Use concatenation instead of $"..." (string interpolation)
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(
                "http://" + backendHost + ":" + backendPort + backendPath);
            req.Method = "GET";
            req.UserAgent = Request.UserAgent ?? "Mediaroom-STB";

            // Bind to Ethernet 7 IP if found
            if (!string.IsNullOrEmpty(eth7IP))
            {
                req.ServicePoint.BindIPEndPointDelegate = delegate(ServicePoint sp, IPEndPoint remoteEndPoint, int retryCount)
                {
                    return new IPEndPoint(IPAddress.Parse(eth7IP), 0);
                };
            }

            using (HttpWebResponse backendRes = (HttpWebResponse)req.GetResponse())
            using (Stream backendStream = backendRes.GetResponseStream())
            {
                Response.StatusCode = (int)backendRes.StatusCode;
                Response.ContentType = backendRes.ContentType ?? "application/vnd.microsoft-tvui+xml";
                backendStream.CopyTo(Response.OutputStream);
            }
        }
        catch (Exception ex)
        {
            Response.ContentType = "application/vnd.microsoft-tvui+xml";

            string fallback =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<uidescription>\n" +
                "  <MrmlPage>\n" +
                "    <Text>Backend error: " + HttpUtility.HtmlEncode(ex.Message) + "</Text>\n" +
                "  </MrmlPage>\n" +
                "</uidescription>";

            Response.Write(fallback);
        }

        Response.Flush();
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }

    private string GetEthernet7IP()
    {
        try
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                string lname = nic.Name.ToLowerInvariant();
                if (lname.Contains("ethernet 7") || lname.Contains("eth7") || lname.Contains("ethernet7"))
                {
                    foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                            return addr.Address.ToString();
                    }
                }
            }
        }
        catch { }
        return null;
    }
}