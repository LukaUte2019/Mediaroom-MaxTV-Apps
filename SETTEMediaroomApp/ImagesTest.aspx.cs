using System;
using System.Text;
using System.Web;
using System.Web.UI;
using Newtonsoft.Json.Linq;
using System.IO;

public partial class ImagesTest : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.Cache.SetNoStore();

        int pageSize = 3; // images per page
        int offset = 0;

        string offsetStr = Request.QueryString["offset"];
        int off;
        if (!string.IsNullOrEmpty(offsetStr) && int.TryParse(offsetStr, out off))
        {
            offset = Math.Max(0, off);
        }

        string jsonFile = Server.MapPath("~/SETTEMediaroomApp/png_files.json"); // your JSON list
        if (!File.Exists(jsonFile))
        {
            Response.Write("No images.json found.");
            Response.End();
        }

        string jsonText = File.ReadAllText(jsonFile);
        JArray imageList = JArray.Parse(jsonText);

        string mrml = BuildImageGallery(imageList, offset, pageSize, Request);
        Response.Write(mrml);
        Response.End();
    }

    private string BuildImageGallery(JArray imageList, int offset, int pageSize, HttpRequest req)
    {
        int total = imageList.Count;
        int endIndex = Math.Min(offset + pageSize, total);

        const int IMAGE_WIDTH = 360;
        const int IMAGE_HEIGHT = 180;
        const int ITEM_SPACING = 20;
        string basePath = "file:///"; // Adjust if needed

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<uidescription version=\"3.0\">");
        sb.AppendLine("  <MrmlPage id=\"ImageGallery\" appid=\"images.app/1.0\" width=\"1280\" height=\"720\">");
        sb.AppendLine("    <Panel id=\"MainPanel\" left=\"0\" top=\"0\" width=\"1280\" height=\"720\">");
        sb.AppendLine("      <Text id=\"Title\" top=\"10\" left=\"20\" width=\"1240\" height=\"30\" fontstyle=\"Reg26\" foreground=\"argb(255,228,0,115)\">Content Images</Text>");

        // HorizontalFlowPanel
        sb.AppendLine("      <HorizontalFlowPanel id=\"ImageGrid\" top=\"60\" left=\"40\" width=\"1200\" height=\"580\" clipsChildren=\"true\" itemSpacing=\"" + ITEM_SPACING + "\" wrap=\"true\">");

        for (int i = offset; i < endIndex; i++)
        {
            JObject item = imageList[i] as JObject;
            if (item == null) continue;

            string filename = item.Value<string>("filename") ?? "Image" + i;
            string path = item.Value<string>("path") ?? "";
            if (string.IsNullOrEmpty(path)) continue;

            string href = basePath + path;

            sb.AppendLine("        <Button id=\"img_" + i + "\" width=\"" + IMAGE_WIDTH + "\" height=\"" + IMAGE_HEIGHT + "\" focusScale=\"1.05\" justification=\"center\" href=\"page:" + EscapeXml(href) + "\">");
            sb.AppendLine("          <Image url=\"" + EscapeXml(href) + "\" width=\"" + IMAGE_WIDTH + "\" height=\"" + IMAGE_HEIGHT + "\" />");
            sb.AppendLine("          <Text top=\"10\" left=\"10\" width=\"" + (IMAGE_WIDTH - 20) + "\" height=\"24\" fontstyle=\"Reg18\" alignment=\"center\">" + EscapeXml(filename) + "</Text>");
            sb.AppendLine("        </Button>");
        }

        sb.AppendLine("      </HorizontalFlowPanel>");

        // Load More / Show More buttons
        if (endIndex < total)
        {
            string nextUrl = req.Url.GetLeftPart(UriPartial.Path) + "?offset=" + endIndex + "&pageSize=" + pageSize;

            sb.AppendLine("      <Button id=\"LoadMoreButton\" top=\"650\" left=\"40\" width=\"300\" height=\"40\" focusScale=\"1.05\" justification=\"center\" href=\"page:" + EscapeXml(nextUrl) + "\">");
            sb.AppendLine("        <Text top=\"8\" left=\"10\" width=\"280\" height=\"24\" fontstyle=\"Reg18\" alignment=\"center\">Load more</Text>");
            sb.AppendLine("      </Button>");

            sb.AppendLine("      <Button id=\"ShowMoreButton\" top=\"650\" left=\"360\" width=\"300\" height=\"40\" focusScale=\"1.05\" justification=\"center\" href=\"page:" + EscapeXml(nextUrl) + "\">");
            sb.AppendLine("        <Text top=\"8\" left=\"10\" width=\"280\" height=\"24\" fontstyle=\"Reg18\" alignment=\"center\">Show more</Text>");
            sb.AppendLine("      </Button>");
        }

        sb.AppendLine("    </Panel>");
        sb.AppendLine("  </MrmlPage>");
        sb.AppendLine("</uidescription>");

        return sb.ToString();
    }

    private string EscapeXml(string s)
    {
        return s == null ? "" : System.Security.SecurityElement.Escape(s);
    }
}