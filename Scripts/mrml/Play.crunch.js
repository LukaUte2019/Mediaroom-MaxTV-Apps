var readTimerId = 0,
    allowZoetropeAccess = "false",
    zoetropePosition = "",
    defaultPositionValue = "-1";

var videoDuration = 0;
var lastPosition = 0;
/* =========================
   ZOETROPE VISIBILITY
   ========================= */

function showZoetrope() {
    setProperty(
        "thumbnailList",
        "datasource",
        "{Binding Source=ZoetropeDataSource,Path=SessionNameFULLSCREEN/StartAt-1}"
    );
}

function hideZoetrope() {
    cancelReadTimer();
    allowZoetropeAccess = "false";
    setProperty("thumbnailList", "datasource", "invalid");
}

/* =========================
   POSITION HANDLING
   ========================= */

function setCurrentPosition() {
    var a = getProperty("thumbnailList", "selectedItem/@timestamp");
    sendCurrentPositionEvent(a);
}

function resetCurrentPosition() {
    var a = "0ticks";
    sendCurrentPositionEvent(a);
}

/* =========================
   KEY HANDLING
   ========================= */

function onKeyPressed() {
    var b = readContext("keyname"),
        a = null;

    switch (b) {

        case "exit":
        case "vod":
        case "red":
        case "green":
        case "blue":
        case "yellow":
        case "app1":
        case "app2":
        case "app3":
        case "app4":
        case "up":
        case "down":
        case "channelup":
        case "channeldown":
        case "0":
        case "1":
        case "2":
        case "3":
        case "4":
        case "5":
        case "6":
        case "7":
        case "8":
        case "9":
            invokeAction("foregroundTVPage", "saveBookmark");
            break;

        case "menu":
            a = "menu";
            break;

        case "recordedtv":
            a = "dvr";
            break;

        case "guide":
            a = "guide";
            break;

        case "info":
            invokeAction("foregroundTVPage", "saveBookmark");
            invokeAction("foregroundTVPage", "programInfoAction");
            break;
    }

    if (a != null) {
        invokeAction("foregroundTVPage", "saveBookmark");
        invokeAction("foregroundTVPage", "NavigateOnKeyPress", a);
    }
}

/* =========================
   CONDITIONAL LOGIC
   ========================= */

function conditionalPause() {
    var a = getProperty(
        "ZoetropeDataSourceForActions",
        "SessionNameFULLSCREEN/TotalCount"
    );

    if (a == "0") {
        writeContext("handled", "false");
    } else {
        invokeAction(null, "pauseAction");
    }
}

/* =========================
   TIMESTAMP EVENTS
   ========================= */

function sendCurrentPositionEvent(a) {
    var b =
        "#urn:microsoft:mediaroom:storefront:event:setcurrentposition?timestamp=" +
        a;

    invokeAction(null, "sendSetCurrentPositionCustomEvent", b);
}

/* =========================
   TIMER SYSTEM
   ========================= */

function startReadTimer() {
    readTimerId = setTimeout("readValue()", 500);
}

function cancelReadTimer() {
    clearTimeout(readTimerId);
}

function readValue() {
    if (allowZoetropeAccess == "true") {
        zoetropePosition = getProperty(
            "thumbnailList",
            "currentItem/@timestamp",
            defaultPositionValue
        );

        if (
            zoetropePosition != "" &&
            zoetropePosition != defaultPositionValue
        ) {
            sendCurrentPositionEvent(zoetropePosition);
        }
    }

    startReadTimer();
}

/* =========================
   SPEED CHECK
   ========================= */

function checkSpeedBeforeShow() {
    var a = getProperty("FullScreenDataSource", "speed");

    if (a == 0) {
        invokeAction(null, "conditionalShowAction");
    } else {
        conditionalPause();
    }
}

/* =========================
   READY STATE
   ========================= */

function zoetropeReady() {
    allowZoetropeAccess = "true";
}

function startVideoWatch() {
    videoDuration = getProperty("backgroundVideoPlayer", "duration");
    startReadTimer();
}

function readVideoPosition() {

    var currentPos = getProperty("backgroundVideoPlayer", "position");

    if (currentPos != "") {

        lastPosition = currentPos;

        // ✅ VIDEO FINISHED CHECK
        if (videoDuration > 0 && currentPos >= (videoDuration - 2)) {

            cancelReadTimer();

            // trigger your action
            invokeAction("TVPage", "NextVideo");

            return;
        }
    }

    startReadTimer();
}

function scriptTesting() {
    invokeAction("TVPage", "showRecordFail");
}