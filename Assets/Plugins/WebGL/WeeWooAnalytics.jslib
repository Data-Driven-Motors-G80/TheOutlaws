// Browser side of WeeWooAnalytics.cs.
// Batches are queued and sent one at a time; each is only removed once the Apps Script
// replies {"ok":true}, otherwise it is retried with back-off. Anything still unconfirmed when
// the page closes is handed to sendBeacon together with the final exit rows.
mergeInto(LibraryManager.library, {
  WWR_Init: function (urlPtr) {
    var state = { url: UTF8ToString(urlPtr), exit: "", queue: [], busy: false, failures: 0 };
    window.__weeWooAnalytics = state;

    state.pump = function () {
      if (state.busy || state.queue.length === 0) return;
      state.busy = true;
      var body = state.queue[0];
      // text/plain keeps this a "simple" request (no CORS preflight); Apps Script's reply is
      // readable after its redirect, so we can confirm the rows were written.
      fetch(state.url, {
        method: "POST",
        headers: { "Content-Type": "text/plain;charset=utf-8" },
        body: body
      }).then(function (response) {
        return response.text();
      }).then(function (text) {
        if (text.indexOf('"ok":true') < 0) throw new Error("not confirmed: " + text.slice(0, 120));
        state.queue.shift();
        state.failures = 0;
        state.busy = false;
        state.pump();
      }).catch(function (error) {
        state.busy = false;
        state.failures++;
        console.warn("[Analytics] upload retry " + state.failures + ": " + error);
        if (state.failures > 8) {
          // Give up on this batch rather than block everything behind it.
          state.queue.shift();
          state.failures = 0;
        }
        setTimeout(state.pump, Math.min(30000, 1500 * Math.pow(2, state.failures)));
      });
    };

    var sendExit = function () {
      var bodies = state.queue.slice();
      if (state.exit) bodies.push(state.exit);
      for (var i = 0; i < bodies.length; i++) {
        try {
          navigator.sendBeacon(state.url, new Blob([bodies[i]], { type: "text/plain;charset=utf-8" }));
        } catch (e) {}
      }
      state.queue = [];
      state.exit = "";
    };
    // pagehide fires on close, reload and navigation (also on mobile, unlike beforeunload).
    window.addEventListener("pagehide", sendExit);
  },

  WWR_Post: function (bodyPtr) {
    var state = window.__weeWooAnalytics;
    if (!state) return;
    state.queue.push(UTF8ToString(bodyPtr));
    state.pump();
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
