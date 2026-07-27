// Bloqueo fiable del scroll de página sobre el host de malla.
// El listener no es passive para poder preventDefault en la rueda.
window.diamondMeshViewport = {
  /** @type {WeakMap<Element, EventListener>} */
  _handlers: new WeakMap(),

  /**
   * @param {HTMLElement} element
   * @returns {SVGSVGElement|null}
   */
  _findSvg: function (element) {
    if (!element) {
      return null;
    }
    return element.querySelector("svg.diamond-mesh-plot-svg")
      || element.querySelector("svg.mesh-svg-canvas")
      || element.querySelector("svg");
  },

  /**
   * Convierte coordenadas de cliente a unidades del viewBox SVG.
   * @param {HTMLElement} element host del plot
   * @param {number} clientX
   * @param {number} clientY
   * @returns {{x:number,y:number,scaleX:number,scaleY:number}|null}
   */
  clientToSvg: function (element, clientX, clientY) {
    if (!element) {
      return null;
    }

    const rect = element.getBoundingClientRect();
    const offsetX = clientX - rect.left;
    const offsetY = clientY - rect.top;
    const svg = this._findSvg(element);
    if (!svg || svg.clientWidth <= 0 || svg.clientHeight <= 0) {
      return { x: offsetX, y: offsetY, scaleX: 1, scaleY: 1 };
    }

    const vb = svg.viewBox && svg.viewBox.baseVal;
    const vbW = vb && vb.width > 0 ? vb.width : (svg.width && svg.width.baseVal ? svg.width.baseVal.value : svg.clientWidth);
    const vbH = vb && vb.height > 0 ? vb.height : (svg.height && svg.height.baseVal ? svg.height.baseVal.value : svg.clientHeight);
    const scaleX = vbW / svg.clientWidth;
    const scaleY = vbH / svg.clientHeight;
    return {
      x: offsetX * scaleX,
      y: offsetY * scaleY,
      scaleX: scaleX,
      scaleY: scaleY
    };
  },

  /**
   * @param {HTMLElement} element
   * @param {DotNetObjectReference} dotNetRef
   */
  attach: function (element, dotNetRef) {
    if (!element) {
      return;
    }

    this.detach(element);

    const self = this;
    const handler = function (e) {
      e.preventDefault();
      e.stopPropagation();

      const mapped = self.clientToSvg(element, e.clientX, e.clientY);
      const sx = mapped ? mapped.x : 0;
      const sy = mapped ? mapped.y : 0;

      // Evitar unhandled promise rejection si el circuito Blazor está ocupado/disposed.
      try {
        var p = dotNetRef.invokeMethodAsync("OnWheelFromJs", e.deltaY, sx, sy);
        if (p && typeof p.catch === "function") {
          p.catch(function () { /* ignore */ });
        }
      } catch (err) {
        // ignore
      }
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
