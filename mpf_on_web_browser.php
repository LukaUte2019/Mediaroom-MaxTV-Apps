<?php

function q($key, $default = "") {
    return isset($_GET[$key]) ? $_GET[$key] : $default;
}

function attr($node, $name, $default = "") {
    $attrs = $node->attributes();
    return isset($attrs[$name]) ? (string)$attrs[$name] : $default;
}

function decode($v) {
    return urldecode($v ?? "");
}

function stylePos($node) {
    $x = attr($node, "left", "0");
    $y = attr($node, "top", "0");
    $w = attr($node, "width", "auto");
    $h = attr($node, "height", "auto");

    $style = "position:absolute; left:{$x}px; top:{$y}px;";
    if ($w !== "auto") $style .= " width:{$w}px;";
    if ($h !== "auto") $style .= " height:{$h}px;";
    return $style;
}

function selfPage() {
    return "mpf_on_web_browser.php";
}

/**
 * Convert page: links into browser navigation
 */
function fixHref($href) {
    $href = trim($href);

    if ($href === "") return "";

    // page:http://...
    if (strpos($href, "page:") === 0) {
        $target = substr($href, 5);
        return selfPage() . "?mpf_page_url=" . urlencode($target);
    }

    return $href;
}

function renderChildren($node) {
    $out = "";
    foreach ($node->children() as $child) {
        $out .= convertNode($child);
    }
    return $out;
}

function convertNode($node) {

    $tag = $node->getName();

    switch ($tag) {

        // ================= TEXT =================
        case "Text":
            $style = stylePos($node);
            $text = htmlspecialchars((string)$node);

            return "<div style='{$style} color:white; font-size:20px;'>
                        {$text}
                    </div>";

        // ================= BUTTON =================
        case "Button":
            $style = stylePos($node);

            $text = "";
            foreach ($node->children() as $child) {
                if ($child->getName() === "Text") {
                    $text .= (string)$child;
                }
            }

            if ($text === "") {
                $text = attr($node, "id", "Button");
            }

            $href = fixHref(attr($node, "href", ""));

            $inner = "
                <div style='
                    width:100%;
                    height:100%;
                    background:#222;
                    border:1px solid #444;
                    color:white;
                    display:flex;
                    align-items:center;
                    justify-content:center;
                    border-radius:6px;
                '>
                    " . htmlspecialchars($text) . "
                </div>
            ";

            if ($href !== "") {
                return "<a href='{$href}' style='{$style} text-decoration:none; display:block;'>
                            {$inner}
                        </a>";
            }

            return "<div style='{$style}'>{$inner}</div>";

        // ================= IMAGE =================
        case "Image":
            $style = stylePos($node);
            $url = attr($node, "url", "");

            return "<img src='{$url}' style='{$style} object-fit:contain;' />";

        // ================= EDIT TEXT =================
        case "EditText":
            $style = stylePos($node);
            $hint = attr($node, "hint", "");
            $value = (string)$node;

            return "<input type='text'
                placeholder='{$hint}'
                value='" . htmlspecialchars($value) . "'
                style='{$style} padding:5px; background:#111; color:white; border:1px solid #555;' />";

        // ================= VIDEO =================
        case "Video":
            $style = stylePos($node);

            $src = attr($node, "tuneurl", "");
            if (!$src) $src = attr($node, "src", "");

            $controls = attr($node, "showcontrols", "true") === "true" ? "controls" : "";
            $autoplay = attr($node, "autoplay", "true") === "true" ? "autoplay" : "";
            $muted = attr($node, "muted", "false") === "true" ? "muted" : "";

            return "<video
                        src='{$src}'
                        style='{$style} background:black;'
                        {$controls} {$autoplay} {$muted}>
                    </video>";

        // ================= PANELS =================
        case "Panel":
        case "VerticalFlowPanel":
        case "HorizontalFlowPanel":

            $style = stylePos($node);

            if ($tag === "VerticalFlowPanel") {
                $style .= " display:flex; flex-direction:column; gap:10px; position:absolute;";
            }

            if ($tag === "HorizontalFlowPanel") {
                $style .= " display:flex; flex-direction:row; gap:10px; position:absolute;";
            }

            return "<div style='{$style}'>" . renderChildren($node) . "</div>";
    }

    return "";
}

// ================= LOAD MRML =================

$source = q("mpf_page_url", "page.mrml");
$source = decode($source);

libxml_use_internal_errors(true);
$xml = simplexml_load_file($source);
libxml_clear_errors();

echo "<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<title>MPF Web Browser</title>

<style>
body {
    margin:0;
    background:black;
    font-family:Arial;
    overflow:hidden;
}
</style>

</head>
<body>";

if ($xml && isset($xml->MrmlPage)) {
    foreach ($xml->MrmlPage->children() as $node) {
        echo convertNode($node);
    }
} else {
    echo "<div style='color:red;padding:20px;'>Failed to load MRML</div>";
}

echo "</body></html>";
?>