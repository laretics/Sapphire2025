window.TeslaToolBar_registerKeyHandler = function (dotNetHelper) {
    window.teslaToolbarKeyHandler = function (e) {
        // Solo si el foco no está en un input
        if (["INPUT", "TEXTAREA"].indexOf(document.activeElement.tagName) === -1) {
            const key = e.key;
            if (
                /^[0-9]$/.test(key) ||
                key === "Enter" ||
                key === "ArrowUp" ||
                key === "ArrowDown" ||
                key === "ArrowLeft" ||
                key === "ArrowRight" ||
                key === "F7" ||
                key === "F8" ||
                key === "F9"
            ) {
                if (key === "F7" || key === "F8" || key === "F9") {
                    e.preventDefault();
                }
                dotNetHelper.invokeMethodAsync('HandleKey', key);
            }
        }
    };
    window.addEventListener('keydown', window.teslaToolbarKeyHandler);
};

window.TeslaToolBar_unregisterKeyHandler = function () {
    if (window.teslaToolbarKeyHandler) {
        window.removeEventListener('keydown', window.teslaToolbarKeyHandler);
        window.teslaToolbarKeyHandler = null;
    }
};