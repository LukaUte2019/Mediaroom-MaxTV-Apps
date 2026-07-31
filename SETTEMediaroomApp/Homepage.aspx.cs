using System;
using System.Text;

public partial class Homepage : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.ContentEncoding = Encoding.UTF8;

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");

        // ROOT FIRST (IMPORTANT)
        sb.AppendLine("<uidescription version=\"38.0\" appid=\"ericsson.mediaroom.storefront/2.5/Main25\" width=\"1280\" height=\"720\" background=\"argb(191,17,17,17)\">");

        // SECOND LEVEL PAGE
        sb.AppendLine("<MrmlPage id=\"homepage\">");

        // HEADER
        sb.AppendLine("<Header />");


        // DEFINITIONS
        sb.AppendLine("<Definitions id=\"pageVars\">");
        sb.AppendLine("<Definition name=\"initialLockState\" type=\"literal\" value=\"1\"/>");
        sb.AppendLine("<Definition name=\"currentLockState\" type=\"binding\" value=\"{Binding Source=local://access-control,Path=AdultLocked}\"/>");
        sb.AppendLine("<Definition name=\"drillRight\" type=\"literal\" value=\"false\"/>");
        sb.AppendLine("<Definition name=\"selectedMenu\" type=\"literal\" value=\"sideMenuGrid\"/>");
        sb.AppendLine("</Definitions>");

        // ANIMATIONS
        sb.AppendLine("<Animations>");
        sb.AppendLine("<Animation name=\"GeneralFadeIn\"><Fade from=\"0\" to=\"1\" duration=\"0.4\"/></Animation>");
        sb.AppendLine("<Animation name=\"GeneralFadeOut\"><Fade from=\"1\" to=\"0\" duration=\"0.4\"/></Animation>");
        sb.AppendLine("</Animations>");

        // DATASOURCES

        sb.AppendLine("<DataSource id=\"appData\" uri=\"app://PFStorefront\"/>");
        sb.AppendLine("<DataSource id=\"subpageData\"/>");

        // SAFE linkData (NO invalid nesting)
        sb.AppendLine("<DataSource id=\"linkData\">");
        sb.AppendLine("<links>");

        sb.AppendLine("<link title=\"Filmovi\" url=\"http://p2pfsf10.prod.iptv.mt/MediaroomV2.5/VodStorefront.Main25/category?id=98595\"/>");
        sb.AppendLine("<link title=\"Subscriptions\" url=\"http://p2pfsf10.prod.iptv.mt/MediaroomV2.5/VodStorefront.Main25/purchase/subscriptions\"/>");
        sb.AppendLine("<link title=\"Detski\" url=\"http://p2pfsf10.prod.iptv.mt/MediaroomV2.5/VodStorefront.Main25/category?id=52773639\"/>");
        sb.AppendLine("<link title=\"MyVideos\" url=\"http://p2pfsf10.prod.iptv.mt/MediaroomV2.5/VodStorefront.Main25/mycontent\"/>");
        sb.AppendLine("<link title=\"TV Teka\" url=\"http://p2pfsf10.prod.iptv.mt/MediaroomV2.5/VodStorefront.Main25/category?id=98717\"/>");
        sb.AppendLine("<link title=\"TV Archive\" url=\"http://p2pfsf10.prod.iptv.mt/MediaroomV2.5/VodStorefront.Main25/category?id=97003\"/>");
        sb.AppendLine("<link title=\"Ostanati\" url=\"http://p2pfsf10.prod.iptv.mt/MediaroomV2.5/VodStorefront.Main25/category?id=10782619\"/>");
        sb.AppendLine("<link title=\"MaxTV\" url=\"http://p2pfsf10.prod.iptv.mt/MediaroomV2.5/VodStorefront.Main25/category?id=12509934\"/>");
        sb.AppendLine("<link title=\"Search\" url=\"http://p2pfsf10.prod.iptv.mt/MediaroomV2.5/VodStorefront.Main25/search\"/>");

        sb.AppendLine("</links>");
        sb.AppendLine("</DataSource>");


        // SIDE MENU (FIXED PATH)
        sb.AppendLine("<Panel id=\"sidemenu\" width=\"382\" height=\"720\">");
        sb.AppendLine("<PhysicsGrid id=\"sideMenuGrid\" elementWidth=\"380\" elementHeight=\"60\" datasource=\"{Binding Source=linkData,Path=links/link}\"/>");
        sb.AppendLine("</Panel>");

        // SUBPAGE
        sb.AppendLine("<Subpage id=\"contentSubpage\" left=\"480\" width=\"828\" height=\"720\" datasource=\"{Binding Source=subpageData,Path=//Panel[@id='content']/*}\"/>");

        // ACTIONS
        sb.AppendLine("<Actions>");
        sb.AppendLine("<Event type=\"onready\" action=\"purgeDS OnSubPageReady OnSubpageLoad\"/>");
        sb.AppendLine("<Event type=\"onenter\" action=\"focusOnBottomSideMenu\"/>");
        sb.AppendLine("<Event type=\"onleave\" action=\"closeLayers\"/>");
        sb.AppendLine("<Event type=\"onreturn\" action=\"refreshIfAssetStateChanged hasLockstateChanged\"/>");
        sb.AppendLine("<Event type=\"onerror\" action=\"Error\" target=\"subpageData\"/>");
        sb.AppendLine("</Actions>");

        // CLOSE MRML PAGE FIRST
        sb.AppendLine("</MrmlPage>");

        // CLOSE ROOT LAST
        sb.AppendLine("</uidescription>");

        Response.Write(sb.ToString());
        Response.End();
    }
}