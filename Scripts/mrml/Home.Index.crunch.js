var iSMV = true,
    iSOBR = false,
    iSOS = false,
    loading = "t",
    cSK = "",
    td = 250,
    trd = 144,
    cK = "alpha",
    keyValue = "",
    inputKey = "";

// =========================
// POSTER GRID HANDLING
// =========================
function posterItemHandleOnkey() {
    var a = readContext("keyname");

    if (a == "right" || a == "down" || a == "channeldown") {

        var c = parseInt(getProperty("posterGrid", "selectedColumn")),
            d = parseInt(getProperty("posterGrid", "selectedRow")),
            e = parseInt(getProperty("posterGrid", "totalRowCount")),
            h = parseInt(getProperty("posterGrid", "totalCount")),
            g = parseInt(getProperty("posterGrid", "totalColumnCount")),
            b = (h - 1) % g,
            i = 3;

        var j = (a == "right" && d == e - 1 && c >= b);
        if (j) setVal("posterGrid", b, d, 0, readContext("data"));

        var k = (a == "down" && d >= e - 2 && c > b);
        var f = (a == "channeldown" && d >= e - i - 1 && c > b);

        if (k || f)
            setVal("posterGrid", c, e - 2, 0, readContext("data"));
    }
}

// =========================
// ROW NAVIGATION
// =========================
function nextRow() {
    if (inputKey == "right") {

        var e = getProperty("posterGrid", "totalColumnCount"),
            b = getProperty("posterGrid", "selectedColumn");

        if (parseInt(b) == parseInt(e) - 1) {

            var a = readContext("data"),
                c = getProperty("posterGrid", "selectedRow"),
                d = getProperty("posterGrid", "totalRowCount");

            if (parseInt(c) < parseInt(d) - 1) {

                setProperty("pageVars", "isAdultRefresh", "true");
                invokeAction(a, "deFocusGrid");
                setProperty("posterCursor", "visible", "false");

                setTimeout(
                    "setVal('posterGrid',0,'" + (parseInt(c) + 1) + "','" + b + "','" + a + "')",
                    td
                );

            } else if (d == 1) {

                setProperty("pageVars", "isAdultRefresh", "true");
                invokeAction(a, "deFocusGrid");
                setProperty("posterCursor", "visible", "false");

                setTimeout(
                    "setVal('posterGrid',0,0,'" + b + "','" + a + "')",
                    td
                );

            } else {

                invokeAction(a, "deFocusGrid");
                setProperty("pageVars", "isAdultRefresh", "true");
                setVal("posterGrid", b, c, 0, readContext("data"));
            }
        }
    }
}

// =========================
// GRID SET VALUE
// =========================
function setVal(a, c, d, e, b) {
    setProperty(a, "target.horizontal", c);
    setProperty(a, "target.vertical", d);
    setTimeout("showTitle('" + b + "');", e * trd);
}

// =========================
// SHOW TITLE
// =========================
function showTitle(a) {
    invokeAction(a, "focusGrid");
    setProperty("posterCursor", "visible", "true");
}

// =========================
// KEY HANDLER
// =========================
function handleOnKey() {

    var c = /^\d$/,
        d = /^[a-zA-Z]$/,
        b = getProperty("pageVars", "inputType"),
        a = readContext("keyname");

    keyValue = a;
    inputKey = a;

    if (getProperty("pageVars", "optionsVisible") == "Y")
        return;

    if (
        a == "menu" || a == "guide" || a == "vod" || a == "exit"
    ) {
        setProperty("pageVars", "optionsVisible", "N");
        invokeAction("searchContainer", "closeLayers");
    }

    else if (a == "options" || a == "enter")
        invokeAction("searchContainer", "OnOptionScript");

    else if (a == "back" && getProperty("pageVars", "optionsVisible") == "N")
        navigateBack();

    else if (a == "select")
        navigateOnSelect();

    else if (c.test(a)) {

        if (b == "T9")
            invokeAction(null, "SendKey", a);
        else
            invokeAction(null, "SendKey", "#" + a);
    }

    else if (d.test(a)) {

        if (b == "TripleTap")
            invokeAction(null, "SendKey", a);
    }
}