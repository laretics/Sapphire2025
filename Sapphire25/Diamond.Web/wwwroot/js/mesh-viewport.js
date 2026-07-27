// Bloqueo fiable del scroll de página sobre el host de malla.
// El listener no es passive para poder preventDefault en la rueda.
window.diamondMeshViewport = {
  /** @type {WeakMap<Element, EventListener>} */
  _handlers: new WeakMap(),

  /**
   * @param {HTMLElement} element
   * @param {DotNetObjectReference} dotNetRef
   */
  attach: function (element, dotNetRef) {
    if (!element) {
      return;
    }

    this.detach(element);

    const handler = function (e) {
      e.preventDefault();
      e.stopPropagation();

      // Coordenadas relativas al host (como OffsetX/Y de Blazor)
      const rect = element.getBoundingClientRect();
      const offsetX = e.clientX - rect.left;
      const offsetY = e.clientY - rect.top;

      // Escala si el SVG se redimensiona por CSS (width 100%)
      const svg = element.querySelector("svg.mesh-svg-canvas");
      let sx = offsetX;
      let sy = offsetY;
      if (svg) {
        const vb = svg.viewBox && svg.viewBox.baseVal;
        if (vb && svg.clientWidth > 0 && svg.clientHeight > 0) {
          sx = (offsetX / svg.clientWidth) * vb.width;
          sy = (offsetY / svg.clientHeight) * vb.height;
        }
      }

      dotNetRef.invokeMethodAsync("OnWheelFromJs", e.deltaY, sx, sy);
    };

    element.addEventListener("wheel", handler, { passive: false, capture: true });
    this._handlers.set(element, handler);
  },

  /**
   * @param {HTMLElement} element
   */
  detach: function (element) {
    if (!element) {
      return;
    }

    const handler = this._handlers.get(element);
    if (handler) {
      element.removeEventListener("wheel", handler, { capture: true });
      this._handlers.delete(element);
    }
  }
};
