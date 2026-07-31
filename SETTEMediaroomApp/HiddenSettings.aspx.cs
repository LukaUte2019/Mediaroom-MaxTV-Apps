using System;
using System.Text;
using System.Web;
using System.Web.UI;

public partial class HiddenSettings : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.Cache.SetNoStore();

        StringBuilder sb = new StringBuilder();
        string basePath = @"page:file:///";

        int pageSize = 6;
        int offset = 0;
        int tmp;

        if (int.TryParse(Request.QueryString["pageSize"], out tmp) && tmp > 0)
            pageSize = tmp;

        if (int.TryParse(Request.QueryString["offset"], out tmp) && tmp >= 0)
            offset = tmp;

        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<uidescription version=\"3.0\">");
        sb.AppendLine("  <MrmlPage id=\"HiddenSettings\" width=\"1280\" height=\"720\">");
        sb.AppendLine("    <Panel id=\"MainPanel\" width=\"1280\" height=\"720\">");

        sb.AppendLine("      <Text top=\"20\" left=\"20\" width=\"1200\" height=\"40\" fontstyle=\"Reg26\">Hidden Settings</Text>");

        string[] ids = new string[]
        {
            "MonkeyButton",
            "SoundeffectsButton",
            "AudiodescriptionButton",
            "ClosedcaptionButton",
            "DemoButton",
            "HdmisecurityButton",
            "TransponderscanButton",
            "PersonalmediasettingsButton",
            "ParentalcontrolmenuButton",
            "ProtectdvrButton",
            "AddrfremoteButton",
            "SecondaryaudioButton",
            "SubscribedchannelssettingsButton",
            "WifisettingsButton",
            "WifisettingschooseButton",
            "ServiceselectorButton",
            "ServiceMenuButton",
            "StaticIpSettings"
        };

        string[] texts = new string[]
        {
            "Open Monkey mode",
            "Open Sound Settings",
            "Open Audio Description Settings",
            "Open Closed Caption Settings",
            "Open Demo Settings",
            "Open HDMI Security Settings",
            "Open Transponder Settings",
            "Open Personal Media Settings",
            "Open Parental Control Settings",
            "Open Protect DVR Settings",
            "Open Add RF Remote Settings",
            "Open Secondary Audio Settings",
            "Open Subscribed Channels Settings",
            "Open WiFi Settings",
            "Open Choose WiFi Settings",
            "Open Service Selector",
            "Open Service Menu",
            "Static IP Setting"

        };

        string[] hrefs = new string[]
        {
            basePath + "Monkey.xml",
            basePath + "SoundsSettings.xml",
            basePath + "AudioDescriptionSettings.xml",
            basePath + "ClosedCaptionSettings.xml",
            basePath + "Demo.xml",
            basePath + "EnhancedHdmiSecurity.xml",
            basePath + "ManualTransponderScanWizardStep1.xml",
            basePath + "personalmediasettings.xml",
            basePath + "ParentalControl_Menu.xml",
            basePath + "ParentalControl_ProtectDvr.xml",
            basePath + "RfAddRemote.xml",
            basePath + "SecondaryAudioSettings.xml",
            basePath + "SubscribedChannelsSettings.xml",
            basePath + "WiFiSettings.xml",
            basePath + "WiFiSettingsNetworkSelection.xml",
            basePath + "ServiceSelector.xml",
            basePath + "ServiceMenu.xml",
            basePath + "StaticIPSetting.xml"
        };

        int total = ids.Length;
        int endIndex = offset + pageSize;
        if (endIndex > total)
            endIndex = total;

        int left = 40;
        int top = 90;
        int buttonWidth = 500;
        int buttonHeight = 40;
        int step = 50;

        for (int i = offset; i < endIndex; i++)
        {
            sb.AppendLine("      <Button id=\"" + EscapeXml(ids[i]) + "\" top=\"" + top + "\" left=\"" + left + "\" width=\"" + buttonWidth + "\" height=\"" + buttonHeight + "\" focusScale=\"1.05\" justification=\"center\" href=\"" + EscapeXml(hrefs[i]) + "\">");
            sb.AppendLine("        <Text top=\"8\" left=\"10\" width=\"" + (buttonWidth - 20) + "\" height=\"24\" fontstyle=\"Reg18\" alignment=\"center\">" + EscapeXml(texts[i]) + "</Text>");
            sb.AppendLine("        <Actions>");
            sb.AppendLine("          <Event type=\"onclick\" action=\"navigate\" url=\"page:" + EscapeXml(hrefs[i]) + "\" />");
            sb.AppendLine("        </Actions>");
            sb.AppendLine("      </Button>");

            top += step;
        }

        if (endIndex < total)
        {
            string nextUrl = Request.Url.GetLeftPart(UriPartial.Path)
                + "?offset=" + endIndex
                + "&pageSize=" + pageSize;

            sb.AppendLine("      <Button id=\"LoadMoreButton\" top=\"620\" left=\"40\" width=\"220\" height=\"40\" focusScale=\"1.05\" justification=\"center\" href=\"page:" + EscapeXml(nextUrl) + "\">");
            sb.AppendLine("        <Text top=\"8\" left=\"10\" width=\"200\" height=\"24\" fontstyle=\"Reg18\" alignment=\"center\">Load more</Text>");
            sb.AppendLine("        <Actions>");
            sb.AppendLine("          <Event type=\"onclick\" action=\"navigate\" url=\"page:" + EscapeXml(nextUrl) + "\" />");
            sb.AppendLine("        </Actions>");
            sb.AppendLine("      </Button>");
        }

        if (offset > 0)
        {
            int prevOffset = offset - pageSize;
            if (prevOffset < 0) prevOffset = 0;

            string prevUrl = Request.Url.GetLeftPart(UriPartial.Path)
                + "?offset=" + prevOffset
                + "&pageSize=" + pageSize;

            sb.AppendLine("      <Button id=\"PrevButton\" top=\"620\" left=\"280\" width=\"220\" height=\"40\" focusScale=\"1.05\" justification=\"center\" href=\"page:" + EscapeXml(prevUrl) + "\">");
            sb.AppendLine("        <Text top=\"8\" left=\"10\" width=\"200\" height=\"24\" fontstyle=\"Reg18\" alignment=\"center\">Previous</Text>");
            sb.AppendLine("        <Actions>");
            sb.AppendLine("          <Event type=\"onclick\" action=\"navigate\" url=\"page:" + EscapeXml(prevUrl) + "\" />");
            sb.AppendLine("        </Actions>");
            sb.AppendLine("      </Button>");
        }

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
}