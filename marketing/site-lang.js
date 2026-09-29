/* Shared language chooser for the static marketing pages (hub, RomaERP, Roma HR).
   English is the default; Arabic is chosen explicitly (toggle button or a ?lang=ar link) and remembered
   in localStorage under the SAME "lang" key the app and pricing.html use, so one choice follows the
   visitor across every page. Pages keep their Arabic text in the HTML and give English through
   SiteLang.apply({ key: "english html", ... }) for elements tagged data-i18n="key". */
(function () {
  var root = document.documentElement;
  var query = new URLSearchParams(location.search).get("lang");
  var stored = null;
  try { stored = localStorage.getItem("lang"); } catch (e) { /* private mode etc. */ }
  var lang = query === "ar" || query === "en" ? query : stored === "ar" ? "ar" : "en";
  if (query === "ar" || query === "en") {
    try { localStorage.setItem("lang", query); } catch (e) { /* ignore */ }
  }

  root.lang = lang;
  root.dir = lang === "ar" ? "rtl" : "ltr";
  // Hide the page until English is applied so English visitors never see a flash of Arabic; a timer
  // guarantees it is always revealed even if a page forgets to call apply().
  if (lang === "en") {
    root.className += " i18n-pending";
    var style = document.createElement("style");
    style.textContent = "html.i18n-pending body{visibility:hidden}";
    document.head.appendChild(style);
    setTimeout(function () { root.className = root.className.replace(" i18n-pending", ""); }, 1500);
  }

  function setMeta(selector, value) {
    var el = document.querySelector(selector);
    if (el && value) el.setAttribute("content", value);
  }

  window.SiteLang = {
    lang: lang,
    apply: function (dict) {
      var toggle = document.getElementById("langToggle");
      if (toggle) {
        toggle.textContent = lang === "ar" ? "English" : "العربية";
        toggle.onclick = function () {
          try { localStorage.setItem("lang", lang === "ar" ? "en" : "ar"); } catch (e) { /* ignore */ }
          var url = new URL(location.href);
          url.searchParams.delete("lang");
          location.href = url.toString();
        };
      }
      if (lang === "en") {
        var nodes = document.querySelectorAll("[data-i18n]");
        for (var i = 0; i < nodes.length; i++) {
          var value = dict[nodes[i].getAttribute("data-i18n")];
          if (value !== undefined) nodes[i].innerHTML = value;
        }
        if (dict["@title"]) document.title = dict["@title"];
        setMeta('meta[name="description"]', dict["@description"]);
        setMeta('meta[property="og:title"]', dict["@ogTitle"] || dict["@title"]);
        setMeta('meta[property="og:description"]', dict["@ogDescription"] || dict["@description"]);
        root.className = root.className.replace(" i18n-pending", "");
      }
    },
  };
})();
