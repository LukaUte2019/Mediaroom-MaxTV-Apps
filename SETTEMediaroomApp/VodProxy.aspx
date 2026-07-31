<%@ Page Language="C#" AutoEventWireup="true" %>

<script runat="server">

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Web;

protected void Page_Load(object sender, EventArgs e)
{
    try
    {
        string path = Request.RawUrl;

        if (!path.StartsWith("/MediaroomV2.5/VodStorefront.Main25"))
        {
            Response.Write("Blocked");
            return;
        }

        string ip = GetEthernet7Ip();

        if (ip == null)
        {
            Response.StatusCode = 500;
            Response.Write("Ethernet7 not found");
            return;
        }

        string targetUrl = "http://" + ip + path;

        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(targetUrl);
        request.Method = Request.HttpMethod;
        request.UserAgent = Request.UserAgent;
        request.Timeout = 15000;

        // forward POST body if exists
        if (Request.InputStream.Length > 0)
        {
            using (Stream reqStream = request.GetRequestStream())
            {
                Request.InputStream.CopyTo(reqStream);
            }
        }

        using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
        using (Stream stream = response.GetResponseStream())
        {
            Response.ContentType = response.ContentType;
            Response.StatusCode = (int)response.StatusCode;

            stream.CopyTo(Response.OutputStream);
        }

        Response.End();
    }
    catch (Exception ex)
    {
        Response.StatusCode = 500;
        Response.Write("Proxy error: " + ex.Message);
    }
}

private string GetEthernet7Ip()
{
    foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
    {
        if (ni.Name == "Ethernet7" &&
            ni.OperationalStatus == OperationalStatus.Up)
        {
            IPInterfaceProperties props = ni.GetIPProperties();

            foreach (UnicastIPAddressInformation ip in props.UnicastAddresses)
            {
                if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.Address.ToString();
                }
            }
        }
    }

    return null;
}

</script>