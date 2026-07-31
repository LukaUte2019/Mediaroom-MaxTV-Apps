using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.Script.Serialization;

public partial class ShowTVConnected : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string deviceGuid = Request.QueryString["DeviceGuid"];
        if (string.IsNullOrEmpty(deviceGuid))
            deviceGuid = "UNKNOWN";

        LukifyResponse linked = GetLinkedUser(deviceGuid);

        // Handle disconnect action
        string action = Request.QueryString["action"];
        if (!string.IsNullOrEmpty(action) && action.ToLower() == "disconnect")
        {
            UnpairDevice(deviceGuid);
            linked = null; // refresh UI to show disconnected
        }

        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.ContentEncoding = Encoding.UTF8;
        Response.Cache.SetNoStore();

        StringBuilder sb = new StringBuilder();

        sb.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
        sb.AppendLine(@"<uidescription version=""3.0"">");
        sb.AppendLine(@"<MrmlPage id=""TVConnected"" appid=""lukify.tv/1.0"" width=""1280"" height=""720"">");
        sb.AppendLine(@"<Panel>");

        if (linked != null && linked.status == "success")
        {
            RenderConnected(sb, linked, deviceGuid);
        }
        else
        {
            RenderError(sb, deviceGuid);
        }

        sb.AppendLine(@"</Panel>");
        sb.AppendLine(@"</MrmlPage>");
        sb.AppendLine(@"</uidescription>");

        Response.Write(sb.ToString());
        Response.End();
    }

    // ================= CONNECTED UI =================

    private void RenderConnected(StringBuilder sb, LukifyResponse r, string deviceGuid)
    {
        sb.AppendLine(@"
<Text top=""40"" left=""40"" width=""900"" height=""80""
      foreground=""argb(255,0,200,0)"">
  Lukify TV Connected
</Text>");

        // Show big profile picture to the left of the username
        if (!string.IsNullOrEmpty(r.profile_picture_url))
        {
            string safeUrl = HttpUtility.HtmlAttributeEncode(r.profile_picture_url);
            sb.AppendLine(@"<Image id=""ProfilePicBig"" top=""130"" left=""40"" width=""128"" height=""128"" url=""" + safeUrl + @""" />");
        }

        sb.AppendLine(string.Format(@"
<Text top=""160"" left=""180"" width=""1000"" height=""60""
      fontstyle=""Reg42""
      foreground=""argb(255,255,255,255)"">
  Logged in as: {0}
</Text>",
        HttpUtility.HtmlEncode(r.username)));

        sb.AppendLine(string.Format(@"
<Text top=""230"" left=""180"" width=""1000"" height=""50""
      fontstyle=""Reg32""
      foreground=""argb(255,200,200,200)"">
  {0}
</Text>",
        HttpUtility.HtmlEncode(r.full_name)));

        sb.AppendLine(string.Format(@"
<Text top=""300"" left=""180"" width=""1000"" height=""40""
      fontstyle=""Reg28""
      foreground=""argb(255,180,180,180)"">
  Device: Name: {0}, Device Brand: {2}, Device Model: {1}
</Text>",
        HttpUtility.HtmlEncode(r.device_info.device_name),
        HttpUtility.HtmlEncode(r.device_info.device_model),
        HttpUtility.HtmlEncode(r.device_info.brand)));

        // ---------------- Messages button ----------------
        string dmThreadsUrl = "http://172.16.40.101/SETTEMediaroomApp/DMThreads.aspx";
        var dmParams = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrEmpty(r.userid))
            dmParams.Add("userid=" + HttpUtility.UrlEncode(r.userid));
        if (!string.IsNullOrEmpty(deviceGuid) && deviceGuid != "UNKNOWN")
            dmParams.Add("DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid));
        if (dmParams.Count > 0)
            dmThreadsUrl += "?" + string.Join("&", dmParams);

        sb.AppendLine(@"<Button id=""MessagesButton"" top=""400"" left=""40"" width=""320"" height=""50"" justification=""center"" href=""page:" + HttpUtility.HtmlAttributeEncode(dmThreadsUrl) + @""" focusScale=""1.05"">");
        sb.AppendLine(@"  <Text>Messages</Text>");
        sb.AppendLine(@"</Button>");

// ================= VOD STOREFRONT (RIGHT SIDE) =================
string vodUrl = "http://172.16.40.101/SETTEMediaroomApp/VideoTeka.aspx"
                + "?DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);

sb.AppendLine(@"<Button id=""VODStorefrontButton"" top=""460"" left=""900"" width=""320"" height=""50"" justification=""center"" href=""page:" + HttpUtility.HtmlAttributeEncode(vodUrl) + @""" focusScale=""1.05"">");
sb.AppendLine(@"  <Text>MaxTV Videoteka</Text>");
sb.AppendLine(@"</Button>");
        // ---------------- Instagram Login button ----------------
        string instagramSendUrl = BuildSendToPhoneUrl(
            userId: r.userid,
            deviceGuid: deviceGuid,
            deepLinkUrl: BuildInstagramDeepLink(deviceGuid, r.userid)
        );

        sb.AppendLine(@"<Button id=""InstagramLoginButton"" top=""460"" left=""40"" width=""320"" height=""50"" justification=""center"" href=""page:" + HttpUtility.HtmlAttributeEncode(instagramSendUrl) + @""" focusScale=""1.05"">");
        sb.AppendLine(@"  <Text>Log in to Instagram</Text>");
        sb.AppendLine(@"</Button>");

        // ---------------- Search Users button ----------------
        string searchUsersUrl = "http://172.16.40.101/SETTEMediaroomApp/SearchUsers.aspx";
        var suParams = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrEmpty(r.userid))
            suParams.Add("me_id=" + HttpUtility.UrlEncode(r.userid));
        if (!string.IsNullOrEmpty(deviceGuid) && deviceGuid != "UNKNOWN")
            suParams.Add("DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid));
        if (suParams.Count > 0)
            searchUsersUrl += "?" + string.Join("&", suParams);

        sb.AppendLine(@"<Button id=""SearchUsersButton"" top=""520"" left=""40"" width=""320"" height=""50"" justification=""center"" href=""page:" + HttpUtility.HtmlAttributeEncode(searchUsersUrl) + @""" focusScale=""1.05"">");
        sb.AppendLine(@"  <Text>Search Users</Text>");
        sb.AppendLine(@"</Button>");

        // ---------------- Open Profile button ----------------
        string openProfileUrl = "http://172.16.40.101/SETTEMediaroomApp/ViewProfile.aspx"
            + "?username=" + HttpUtility.UrlEncode(r.username)
            + "&user_id=" + HttpUtility.UrlEncode(r.userid)
            + "&userid=" + HttpUtility.UrlEncode(r.userid)
            + "&selected_user_id=" + HttpUtility.UrlEncode(r.userid);

        sb.AppendLine(@"<Button id=""OpenProfileButton"" top=""580"" left=""40"" width=""320"" height=""50"" justification=""center"" href=""page:" + HttpUtility.HtmlAttributeEncode(openProfileUrl) + @""" focusScale=""1.05"">");
        sb.AppendLine(@"  <Text>Open Profile</Text>");
        sb.AppendLine(@"</Button>");

        // ---------------- Lukify Videos button ----------------
        string lukifyVideosUrl = "http://172.16.40.101/SETTEMediaroomApp/LukifyVideos.aspx";
        if (!string.IsNullOrEmpty(deviceGuid) && deviceGuid != "UNKNOWN")
            lukifyVideosUrl += "?DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid) + "&mode=following";

        sb.AppendLine(@"<Button id=""LukifyVideosButton"" top=""640"" left=""40"" width=""320"" height=""50"" justification=""center"" href=""page:" + HttpUtility.HtmlAttributeEncode(lukifyVideosUrl) + @""" focusScale=""1.05"">");
        sb.AppendLine(@"  <Text>View Lukify Videos</Text>");
        sb.AppendLine(@"</Button>");

        // ---------------- Gley button ----------------
        var gleyParams = new System.Collections.Generic.List<string>();
        string deviceGuidParam = (deviceGuid == "UNKNOWN") ? String.Empty : deviceGuid;
        gleyParams.Add("DeviceGuid=" + HttpUtility.UrlEncode(deviceGuidParam));
        if (!string.IsNullOrEmpty(r.userid))
            gleyParams.Add("userid=" + HttpUtility.UrlEncode(r.userid));
        string gleyUrl = "http://172.16.40.101/SETTEMediaroomApp/Gley.aspx";
        if (gleyParams.Count > 0)
            gleyUrl += "?" + string.Join("&", gleyParams);

        sb.AppendLine(@"<Button id=""GleyButton"" top=""700"" left=""40"" width=""320"" height=""50"" justification=""center"" href=""page:" + HttpUtility.HtmlAttributeEncode(gleyUrl) + @""" focusScale=""1.05"">");
        sb.AppendLine(@"  <Text>Gley</Text>");
        sb.AppendLine(@"</Button>");

        // ---------------- Disconnect button ----------------
        string disconnectUrl = "http://172.16.40.101/SETTEMediaroomApp/ShowTVConnected.aspx"
                               + "?DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid)
                               + "&action=disconnect";

        sb.AppendLine(@"<Button id=""DisconnectButton"" top=""400"" left=""900"" width=""320"" height=""50"" justification=""center"" href=""page:" + HttpUtility.HtmlAttributeEncode(disconnectUrl) + @""" focusScale=""1.05"">");
        sb.AppendLine(@"  <Text>Disconnect TV</Text>");
        sb.AppendLine(@"</Button>");
    }

    private string BuildInstagramDeepLink(string deviceGuid, string userId)
    {
        string stbDeviceGuid = (deviceGuid == "UNKNOWN") ? "" : deviceGuid;
        string stbUserId = string.IsNullOrEmpty(userId) ? "" : userId;

        return "lukify://log_in_to_instagram"
             + "?stbdeviceguid=" + HttpUtility.UrlEncode(stbDeviceGuid)
             + "&stbuserid=" + HttpUtility.UrlEncode(stbUserId);
    }

    private string BuildSendToPhoneUrl(string userId, string deviceGuid, string deepLinkUrl)
    {
        string url = "http://172.16.40.101/SETTEMediaroomApp/SendLinkToPhone.aspx";
        var qs = new System.Collections.Generic.List<string>();

        if (!string.IsNullOrEmpty(userId))
            qs.Add("user_id=" + HttpUtility.UrlEncode(userId));

        if (!string.IsNullOrEmpty(deviceGuid) && deviceGuid != "UNKNOWN")
            qs.Add("deviceguid=" + HttpUtility.UrlEncode(deviceGuid));

        if (!string.IsNullOrEmpty(deepLinkUrl))
            qs.Add("url=" + HttpUtility.UrlEncode(deepLinkUrl));

        if (qs.Count > 0)
            url += "?" + string.Join("&", qs);

        return url;
    }

    // ================= ERROR UI =================

    private void RenderError(StringBuilder sb, string deviceGuid)
    {
        sb.AppendLine(@"
<Text top=""200"" left=""200"" width=""900"" height=""80""
      fontstyle=""Reg48""
      foreground=""argb(255,255,60,60)"">
  TV is not linked
</Text>");

        sb.AppendLine(@"
<Text top=""300"" left=""200"" width=""900"" height=""60""
      fontstyle=""Reg32""
      foreground=""argb(255,200,200,200)"">
  Please reconnect this TV from the Lukify Music app
</Text>");

        string connectUrl = "http://172.16.40.101/SETTEMediaroomApp/ConnectSTBToLukify.aspx";
        if (!string.IsNullOrEmpty(deviceGuid))
            connectUrl += "?DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);

        sb.AppendLine(@"<Button id=""ConnectButton"" top=""400"" left=""200"" width=""320"" height=""50"" justification=""center"" href=""page:" + HttpUtility.HtmlAttributeEncode(connectUrl) + @""">");
        sb.AppendLine(@"  <Text>Connect TV</Text>");
        sb.AppendLine(@"</Button>");
    }

    // ================= API CALLS =================

    private LukifyResponse GetLinkedUser(string deviceGuid)
    {
        try
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(
                "http://172.16.40.100/get_lukify_clientidforuserid.php?deviceguid=" +
                HttpUtility.UrlEncode(deviceGuid));

            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
            {
                string json = sr.ReadToEnd();
                JavaScriptSerializer js = new JavaScriptSerializer();
                return js.Deserialize<LukifyResponse>(json);
            }
        }
        catch
        {
            return null;
        }
    }

    private void UnpairDevice(string deviceGuid)
    {
        try
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(
                "http://172.16.40.100/get_lukify_clientidforuserid.php");

            req.Method = "POST";
            req.ContentType = "application/json; charset=utf-8";

            string jsonData = "{\"action\":\"unpair\",\"deviceguid\":\"" + deviceGuid + "\"}";
            byte[] data = Encoding.UTF8.GetBytes(jsonData);
            req.ContentLength = data.Length;

            using (var stream = req.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }

            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream()))
            {
                string response = sr.ReadToEnd();
            }
        }
        catch
        {
        }
    }
}

// ================= MODELS =================

public class LukifyResponse
{
    public string status { get; set; }
    public string userid { get; set; }
    public string deviceguid { get; set; }
    public string username { get; set; }
    public string full_name { get; set; }
    public DeviceInfo device_info { get; set; }
    public string profile_picture_url { get; set; }
}

public class DeviceInfo
{
    public string device_name { get; set; }
    public string device_model { get; set; }
    public string brand { get; set; }
}