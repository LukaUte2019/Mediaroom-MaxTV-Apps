using System;
using System.Text;
using System.Web;
using System.Web.UI;

public partial class TextComponents : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.ContentEncoding = Encoding.UTF8;
        Response.Cache.SetNoStore();

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");

        sb.AppendLine("<uidescription version=\"3.0\">");

        sb.AppendLine("<MrmlPage id=\"TextComponentsPage\"");
        sb.AppendLine("appid=\"lukatube.textcomponents/1.0\"");
        sb.AppendLine("width=\"1280\"");
        sb.AppendLine("height=\"720\">");

sb.AppendLine("      <Video id=\"backgroundVideoPlayer\" width=\"1280\" height=\"720\" visible=\"true\" showcontrols=\"true\" showbusyindicator=\"true\" tuneurl=\"current\"></Video>");

        //
        // ROOT PANEL
        //
        sb.AppendLine("<Button top=\"100\" left=\"40\" width=\"200\" height=\"50\" href=\"action:ShowSeekBar\" >test</Button>");
        sb.AppendLine("<Actions>");

sb.AppendLine("<Action name=\"ShowSeekBar\">");
sb.AppendLine("  <fireevent name=\"#urn:microsoft:mediaroom:event:media:state:seekbar\"/>");
sb.AppendLine("</Action>");

 sb.AppendLine("</Actions>");
        //

        // CLOSE PAGE
        //
        sb.AppendLine("</MrmlPage>");

        sb.AppendLine("</uidescription>");

        Response.Write(sb.ToString());
        Response.End();
    }
}