// Browser side of WeeWooAnalytics.cs: fire-and-forget uploads plus a tab-close beacon.
mergeInto(LibraryManager.library, {
  WWR_Init: function (urlPtr) {
    var state = { url: UTF8ToString(urlPtr), exit: "" };
    window.__weeWooAnalytics = state;
    var sendExit = function () {
      if (!state.exit) return;
      try {
        navigator.sendBeacon(state.url, new Blob([state.exit], { type: "text/plain;charset=utf-8" }));
      } catch (e) {}
      state.exit = "";
    };
    // pagehide fires on close, reload and navigation (also on mobile, unlike beforeunload).
    window.addEventListener("pagehide", sendExit);
  },

  WWR_Post: function (bodyPtr) {
    var state = window.__weeWooAnalytics;
    if (!state) return;
    var body = UTF8ToString(bodyPtr);
    try {
      // no-cors + text/plain: a simple request Apps Script accepts; keepalive survives page close.
      fetch(state.url, {
        method: "POST",
        mode: "no-cors",
        keepalive: body.length < 60000,
        headers: { "Content-Type": "text/plain;charset=utf-8" },
        body: body
      }).catch(function () {
        try { navigator.sendBeacon(state.url, new Blob([body], { type: "text/plain;charset=utf-8" })); } catch (e) {}
      });
    } catch (e) {
      try { navigator.sendBeacon(state.url, new Blob([body], { type: "text/plain;charset=utf-8" })); } catch (e2) {}
    }
  },

  WWR_SetExit: function (bodyPtr) {
    var state = window.__weeWooAnalytics;
    if (state) state.exit = UTF8ToString(bodyPtr);
  },

  WWR_UserAgent: function () {
    var text = (navigator.userAgent || "unknown");
    var size = lengthBytesUTF8(text) + 1;
    var buffer = _malloc(size);
    stringToUTF8(text, buffer, size);
    return buffer;
  }
});
