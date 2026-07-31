using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Web;
using System.Web.UI;

public partial class _PackageSelection : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        Response.Clear();
        Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
        Response.Cache.SetNoStore();

        string backendHost = "172.16.40.101";       // IPTV / Mediaroom backend
        string backendPath = "/24ti/PackageSelection.aspx"; // MRML source
        int backendPort = 80;

        string eth4IP = GetEthernet4IP();

        // Preserve query string from client
        string queryString = Request.QueryString.ToString();
        if (!string.IsNullOrEmpty(queryString))
        {
            backendPath += "?" + queryString;
        }

        try
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(
                "http://" + backendHost + ":" + backendPort + backendPath);
            req.Method = "GET";
            req.UserAgent = Request.UserAgent ?? "Mediaroom-STB";

            if (!string.IsNullOrEmpty(eth4IP))
            {
                req.ServicePoint.BindIPEndPointDelegate = delegate(ServicePoint sp, IPEndPoint remoteEndPoint, int retryCount)
                {
                    return new IPEndPoint(IPAddress.Parse(eth4IP), 0);
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

    private string GetEthernet4IP()
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
