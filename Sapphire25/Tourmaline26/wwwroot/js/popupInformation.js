// Ajuste de fuente para que el texto del popup ocupe el contenedor sin truncar.
window.tourmalinePopup = {
    /**
     * Busca el mayor font-size (px) que cabe en el contenedor sin overflow.
     * @param {HTMLElement} element - nodo del texto
     * @param {number} [minPx=10]
     * @param {number} [maxPx=220]
     * @returns {number} tamaño aplicado
     */
    fitText: function (element, minPx, maxPx) {
        if (!element) return 0;

        // Contenedor de medición: preferir el host dedicado, si no el padre directo.
        const parent =
            element.closest(".popup-information-text-host") || element.parentElement;
        if (!parent) return 0;

        const min = typeof minPx === "number" ? minPx : 10;
        const max = typeof maxPx === "number" ? maxPx : 220;

        // Restablece estilos de medición
        element.style.whiteSpace = "pre-wrap";
        element.style.wordBreak = "break-word";
        element.style.overflow = "hidden";
        element.style.width = "100%";
        element.style.height = "auto";
        element.style.maxHeight = "none";
        element.style.margin = "0";

        const availW = parent.clientWidth;
        const availH = parent.clientHeight;
        if (availW <= 0 || availH <= 0) return 0;

        let low = min;
        let high = max;
        let best = min;

        while (low <= high) {
            const mid = (low + high) >> 1;
            element.style.fontSize = mid + "px";

            // Forzar reflow y medir contra el área útil del host
            const fits =
                element.scrollWidth <= availW + 1 &&
                element.scrollHeight <= availH + 1;

            if (fits) {
                best = mid;
                low = mid + 1;
            } else {
                high = mid - 1;
            }
        }

        element.style.fontSize = best + "px";
        return best;
    },

    /**
     * Observa el contenedor y reajusta la fuente al redimensionar.
     * @param {HTMLElement} element
     * @param {number} [minPx]
     * @param {number} [maxPx]
     */
    observe: function (element, minPx, maxPx) {
        if (!element) return;

        // Cierra observador previo del mismo nodo
        this.unobserve(element);

        const parent =
            element.closest(".popup-information-text-host") || element.parentElement;
        if (!parent) return;

        const run = () => this.fitText(element, minPx, maxPx);
        // Doble rAF: asegura layout tras el paint de Blazor
        requestAnimationFrame(() => requestAnimationFrame(run));

        if (typeof ResizeObserver !== "undefined") {
            const ro = new ResizeObserver(() => run());
            ro.observe(parent);
            element._tourmalinePopupRo = ro;
        }
    },

    unobserve: function (element) {
        if (!element) return;
        if (element._tourmalinePopupRo) {
            element._tourmalinePopupRo.disconnect();
            delete element._tourmalinePopupRo;
        }
    }
};
