// Viewport de malla: rueda (zoom) + mapeo client→SVG para hit-test de trazas.
// El listener de wheel no es passive para poder preventDefault.
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
    return element.querySelector("svg.mesh-svg-canvas")
      || element.querySelector("svg");
  },

  /**
   * Mapea coordenadas de pantalla (clientX/Y) a unidades del viewBox del SVG.
   * Usado por MeshDiagram.TrySelectAtClientAsync (selección de circulación).
   * @param {HTMLElement} element host del plot
   * @param {number} clientX
   * @param {number} clientY
   * @returns {{ x: number, y: number, scaleX: number, scaleY: number }|null}
   *   scaleX/Y = unidades SVG por píxel de pantalla (radio de influencia).
   */
  clientToSvg: function (element, clientX, clientY) {
    if (!element || typeof clientX !== "number" || typeof clientY !== "number") {
      return null;
    }

    const svg = this._findSvg(element);
    if (!svg) {
      return null;
    }

    const rect = svg.getBoundingClientRect();
    if (rect.width <= 0 || rect.height <= 0) {
      return null;
    }

    // Preferir CTM (respeta transformaciones y viewBox).
    try {
      if (typeof svg.createSVGPoint === "function" && typeof svg.getScreenCTM === "function") {
        const ctm = svg.getScreenCTM();
        if (ctm) {
          const inv = ctm.inverse();
          const pt = svg.createSVGPoint();
          pt.x = clientX;
          pt.y = clientY;
          const svgP = pt.matrixTransform(inv);
          // inv.a / inv.d ≈ svg-units per screen-pixel en ejes sin skew.
          let scaleX = Math.abs(inv.a);
          let scaleY = Math.abs(inv.d);
          if (!(scaleX > 1e-12)) {
            scaleX = 1;
          }
          if (!(scaleY > 1e-12)) {
            scaleY = 1;
          }
          return {
            x: svgP.x,
            y: svgP.y,
            scaleX: scaleX,
            scaleY: scaleY
          };
        }
      }
    } catch (err) {
      // Fallback manual.
    }

    const vb = svg.viewBox && svg.viewBox.baseVal;
    const vbX = vb ? vb.x : 0;
    const vbY = vb ? vb.y : 0;
    const vbW = vb && vb.width > 0 ? vb.width : rect.width;
    const vbH = vb && vb.height > 0 ? vb.height : rect.height;
    const scaleX = vbW / rect.width;
    const scaleY = vbH / rect.height;
    const offsetX = clientX - rect.left;
    const offsetY = clientY - rect.top;
    return {
      x: vbX + offsetX * scaleX,
      y: vbY + offsetY * scaleY,
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

      // Coordenadas en espacio SVG (mismo criterio que clientToSvg).
      let sx = 0;
      let sy = 0;
      const mapped = self.clientToSvg(element, e.clientX, e.clientY);
      if (mapped) {
        sx = mapped.x;
        sy = mapped.y;
      } else {
        const rect = element.getBoundingClientRect();
        sx = e.clientX - rect.left;
        sy = e.clientY - rect.top;
      }

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
