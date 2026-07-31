using System;
using System.Text;
using System.Web;
using System.Web.UI;

public partial class _VideoTeka : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        Response.Clear();
        Response.Buffer = true;
        Response.Cache.SetCacheability(HttpCacheability.NoCache);
        Response.Cache.SetNoStore();

        StringBuilder sb = new StringBuilder();

        try
        {
            // =========================
            // BASE URL
            // =========================
            string baseBackend =
                "http://p2pfsf10.prod.iptv.mt/MediaroomV2.5/VodStorefront.Main25/home/index/featured?";

            // =========================
            // ORIGINAL QUERY STRING
            // =========================
            string qs = Request.Url.Query; // includes ?DeviceGuid=...

            // =========================
            // FORCE REQUIRED MPF PARAMS
            // =========================
            string forcedParams =
                "&__MPFMDSTKNAPPID=ericsson.mediaroom.storefront/2.5/Main25";

            // =========================
            // BUILD FINAL URL
            // =========================
            string fullBackendUrl = baseBackend + qs + forcedParams;

            // =========================
            // PROXY URL (NO URL ENCODING)
            // =========================
            string proxyUrl =
                "http://172.16.40.100/vod_proxy.php?url=" + fullBackendUrl;

            // =========================
            // XML SAFE ONLY
            // =========================
            string safeProxyUrl = proxyUrl.Replace("&", "&amp;");

            // =========================
            // MRML OUTPUT
            // =========================
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<uidescription version=\"3.0\">");

            sb.AppendLine("  <MrmlPage id=\"ctl00\" pagelayout=\"widescreen\" excludefromhistory=\"true\" landingpageuri=\"http://172.16.40.100/vod_proxy.php?url=http://p2pfsf10.prod.iptv.mt/MediaroomV2.5/VodStorefront.Main25/home/Index/featured\" background=\"argb(191,17,17,17)\" height=\"720\" width=\"1280\" showanimations=\"GeneralFadeIn\" inanimations=\"GeneralFadeIn\" appid=\"ericsson.mediaroom.storefront/2.5/Main25\">");

            // =========================
            // HEADER
            // =========================
            sb.AppendLine("    <Header>");
            sb.AppendLine("      <AppManifest>");
            sb.AppendLine("        <AppUrls>");
            sb.AppendLine("          <Url>http://172.16.40.100/vod_proxy.php?url=http://p2pfsf10.prod.iptv.mt/MediaroomV2.5/VodStorefront.Main25/home/Index/featured</Url>");
            sb.AppendLine("        </AppUrls>");
            sb.AppendLine("      </AppManifest>");
            sb.AppendLine("    </Header>");

            sb.AppendLine("");

            // =========================
            // LOADING TEXT
            // =========================
            sb.AppendLine("    <Text id=\"PleaseWaitMessage\" fontstyle=\"Reg30\" top=\"154\" left=\"490\" height=\"38\" width=\"600\" showanimations=\"GeneralFadeIn\">");
            sb.AppendLine("      Connecting to IPTV Platform for the MaxTV Videoteka. Please wait...");
            sb.AppendLine("    </Text>");

            // =========================
            // ACTIONS
            // =========================
            sb.AppendLine("    <Actions>");

            sb.AppendLine(
                "      <Action name=\"OnEnterAction\" type=\"navigate\" data=\"" +
                safeProxyUrl +
                "\" />"
            );

            sb.AppendLine("      <Event type=\"onenter\" action=\"OnEnterAction\" />");

            sb.AppendLine("      <Event type=\"onkey:vod\" action=\"CustomKeyActionvod\" />");
            sb.AppendLine("      <Action name=\"CustomKeyActionvod\" type=\"navigate\" data=\"tv\" />");

            sb.AppendLine("    </Actions>");

            sb.AppendLine("  </MrmlPage>");
            sb.AppendLine("</uidescription>");
        }
        catch (Exception ex)
        {
            sb.Clear();

            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<uidescription>");
            sb.AppendLine("  <MrmlPage>");
            sb.AppendLine("    <Text>Error: " + HttpUtility.HtmlEncode(ex.Message) + "</Text>");
            sb.AppendLine("  </MrmlPage>");
            sb.AppendLine("</uidescription>");
        }

        Response.ContentType = "application/vnd.microsoft-tvui+xml";
        Response.Write(sb.ToString());

        Response.Flush();
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }
}