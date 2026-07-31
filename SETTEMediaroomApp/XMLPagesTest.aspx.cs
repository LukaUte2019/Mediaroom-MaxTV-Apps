using System;
using System.IO;
using System.Text;
using System.Web;
using System.Web.UI;
using Newtonsoft.Json.Linq;

public partial class XMLPagesTest : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.Cache.SetNoStore();

        string jsonFile = Server.MapPath("~/SETTEMediaroomApp/xml_files.json");
        if (!File.Exists(jsonFile))
        {
            Response.Write("JSON file not found: " + HttpUtility.HtmlEncode(jsonFile));
            HttpContext.Current.ApplicationInstance.CompleteRequest();
            return;
        }

        string jsonText;
        try
        {
            jsonText = File.ReadAllText(jsonFile);
        }
        catch (Exception ex)
        {
            Response.Write("Failed to read JSON file: " + HttpUtility.HtmlEncode(ex.Message));
            HttpContext.Current.ApplicationInstance.CompleteRequest();
            return;
        }

        JArray xmlList;
        try
        {
            xmlList = JArray.Parse(jsonText);
        }
        catch (Exception ex)
        {
            Response.Write("Failed to parse JSON: " + HttpUtility.HtmlEncode(ex.Message));
            HttpContext.Current.ApplicationInstance.CompleteRequest();
            return;
        }

        int pageSize = 9;
        int offset = 0;

        int tmp;
        if (int.TryParse(Request.QueryString["pageSize"], out tmp) && tmp > 0)
            pageSize = tmp;

        if (int.TryParse(Request.QueryString["offset"], out tmp) && tmp >= 0)
            offset = tmp;

        int total = xmlList.Count;
        int endIndex = Math.Min(offset + pageSize, total);

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<uidescription version=\"3.0\">");
        sb.AppendLine("  <MrmlPage id=\"XMLPagesTest\" appid=\"xmlpages.app/1.0\" width=\"1280\" height=\"720\">");
        sb.AppendLine("    <Panel id=\"MainPanel\" left=\"0\" top=\"0\" width=\"1280\" height=\"720\">");
        sb.AppendLine("      <Text id=\"Title\" top=\"10\" left=\"20\" width=\"1200\" height=\"30\" fontstyle=\"Reg26\" foreground=\"argb(255,228,0,115)\">XML Pages Test</Text>");

        sb.AppendLine("      <VerticalFlowPanel id=\"XmlGrid\" top=\"60\" left=\"40\" width=\"1200\" height=\"580\" clipsChildren=\"true\" itemSpacing=\"20\">");

        int cardWidth = 260;
        int cardHeight = 60;
        string basePath = @"page:file:///";

        // Paginated XML buttons
        for (int i = offset; i < endIndex; i++)
        {
            JObject item = xmlList[i] as JObject;
            if (item == null)
                continue;

            string name = item.Value<string>("name") ?? "";
            string filename = item.Value<string>("filename") ?? "";

            if (string.IsNullOrEmpty(filename))
                continue;

            string href = basePath + filename;

            string safeId = MakeSafeId(name);
            if (string.IsNullOrEmpty(safeId))
                safeId = "xml_" + i.ToString();

            sb.AppendLine("        <Button id=\"" + EscapeXml(safeId) + "\" width=\"" + cardWidth + "\" height=\"" + cardHeight + "\" focusScale=\"1.05\" justification=\"center\" href=\"" + EscapeXml(href) + "\">");
            sb.AppendLine("          <Text top=\"10\" left=\"10\" width=\"" + (cardWidth - 20) + "\" height=\"40\" fontstyle=\"Reg18\" lines=\"2\" alignment=\"center\">" + EscapeXml(name) + "</Text>");
            sb.AppendLine("          <Actions>");
            sb.AppendLine("            <Event type=\"onclick\" action=\"navigate\" url=\"page:" + EscapeXml(href) + "\" />");
            sb.AppendLine("          </Actions>");
            sb.AppendLine("        </Button>");
        }

        sb.AppendLine("      </VerticalFlowPanel>");

        // Load more button
        if (endIndex < total)
        {
            string nextUrl = Request.Url.GetLeftPart(UriPartial.Path)
                + "?offset=" + endIndex
                + "&pageSize=" + pageSize;

            sb.AppendLine("      <Button id=\"LoadMoreButton\" top=\"650\" left=\"40\" width=\"300\" height=\"40\" focusScale=\"1.05\" justification=\"center\" href=\"page:" + EscapeXml(nextUrl) + "\">");
            sb.AppendLine("        <Text top=\"8\" left=\"10\" width=\"280\" height=\"24\" fontstyle=\"Reg18\" alignment=\"center\">Load more</Text>");
            sb.AppendLine("      </Button>");
                        sb.AppendLine("      <Button id=\"LoadMoreButton\" top=\"1\" left=\"40\" width=\"300\" height=\"40\" focusScale=\"1.05\" justification=\"center\" href=\"page:" + EscapeXml(nextUrl) + "\">");
            sb.AppendLine("        <Text top=\"8\" left=\"10\" width=\"280\" height=\"24\" fontstyle=\"Reg18\" alignment=\"center\">Load more</Text>");
            sb.AppendLine("      </Button>");
        }

        // Previous button
        if (offset > 0)
        {
            int prevOffset = offset - pageSize;
            if (prevOffset < 0) prevOffset = 0;

            string prevUrl = Request.Url.GetLeftPart(UriPartial.Path)
                + "?offset=" + prevOffset
                + "&pageSize=" + pageSize;

            sb.AppendLine("      <Button id=\"PrevButton\" top=\"650\" left=\"360\" width=\"300\" height=\"40\" focusScale=\"1.05\" justification=\"center\" href=\"page:" + EscapeXml(prevUrl) + "\">");
            sb.AppendLine("        <Text top=\"8\" left=\"10\" width=\"280\" height=\"24\" fontstyle=\"Reg18\" alignment=\"center\">Previous</Text>");
            sb.AppendLine("      </Button>");
        }

        // Special Monkey.xml button
        string monkeyHref = basePath + "Monkey.xml";
        sb.AppendLine("      <Button id=\"MonkeyButton\" top=\"650\" left=\"680\" width=\"300\" height=\"40\" focusScale=\"1.05\" justification=\"center\" href=\"" + EscapeXml(monkeyHref) + "\">");
        sb.AppendLine("        <Text top=\"8\" left=\"10\" width=\"280\" height=\"24\" fontstyle=\"Reg18\" alignment=\"center\">Open Monkey.xml</Text>");
        sb.AppendLine("      </Button>");

         // Special Monkey.xml button
        string soundeffectsHref = basePath + "SoundsSettings.xml";
        sb.AppendLine("      <Button id=\"SoundeffectsButton\" top=\"650\" left=\"1000\" width=\"300\" height=\"40\" focusScale=\"1.05\" justification=\"center\" href=\"" + EscapeXml(soundeffectsHref) + "\">");
        sb.AppendLine("        <Text top=\"8\" left=\"10\" width=\"280\" height=\"24\" fontstyle=\"Reg18\" alignment=\"center\">Open SoundsSettings.xml</Text>");
        sb.AppendLine("      </Button>");

        sb.AppendLine("    </Panel>");
        sb.AppendLine("  </MrmlPage>");
        sb.AppendLine("</uidescription>");

        Response.Write(sb.ToString());
        Response.Flush();
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }

    private string EscapeXml(string s)
    {
        if (s == null) return "";
        return System.Security.SecurityElement.Escape(s);
    }

    private string MakeSafeId(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "";

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if ((c >= 'a' && c <= 'z') ||
                (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9') ||
                c == '_')
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('_');
            }
        }
        return sb.ToString();
    }
}