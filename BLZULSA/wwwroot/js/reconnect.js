(function () {
    "use strict";

    var MODAL_ID = "components-reconnect-modal";
    var TERMINAL_STATES = ["components-reconnect-failed", "components-reconnect-rejected"];
    var RETRY_DELAY_MS = 5000;

    function isTerminal(element) {
        return TERMINAL_STATES.some(function (state) {
            return element.classList.contains(state);
        });
    }

    function scheduleReload() {
        window.setTimeout(function () {
            window.location.reload();
        }, RETRY_DELAY_MS);
    }

    function watch(modal) {
        var reloading = false;

        var observer = new MutationObserver(function () {
            if (!reloading && isTerminal(modal)) {
                reloading = true;
                observer.disconnect();
                scheduleReload();
            }
        });

        observer.observe(modal, { attributes: true, attributeFilter: ["class"] });
    }

    function start() {
        var modal = document.getElementById(MODAL_ID);
        if (modal) {
            watch(modal);
        }
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", start);
    } else {
        start();
    }
})();
