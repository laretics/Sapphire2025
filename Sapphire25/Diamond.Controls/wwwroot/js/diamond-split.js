/**
 * Divisor horizontal redimensionable entre malla y editor.
 */
(function () {
	"use strict";

	var EDITOR_ID = "diamond-demand-editor";

	function notifyMonacoLayout() {
		try {
			if (!window.diamondMonaco) {
				return;
			}
			// No forzar layout si BlazorMonaco aún no registró el editor
			// (evita carrera con el primer render del split).
			if (typeof window.diamondMonaco.isEditorReady === "function"
				&& !window.diamondMonaco.isEditorReady(EDITOR_ID)) {
				return;
			}
			if (typeof window.diamondMonaco.layout === "function") {
				window.diamondMonaco.layout(EDITOR_ID);
			}
		} catch (e) {
			// ignore
		}
	}

	window.diamondSplit = {
		/**
		 * @param {HTMLElement} root - .mesh-split
		 * @param {HTMLElement} gutter - .mesh-gutter
		 */
		attach: function (root, gutter) {
			if (!root || !gutter) {
				return false;
			}
			if (gutter._diamondSplitAttached) {
				return true;
			}
			gutter._diamondSplitAttached = true;

			var dragging = false;

			function splitMode() {
				if (root.classList.contains("mesh-layout-mesh") || root.classList.contains("mesh-layout-script")) {
					return "fixed";
				}
				if (root.classList.contains("mesh-dock-bottom")) {
					return "bottom";
				}
				return "right";
			}

			function onMove(ev) {
				if (!dragging) {
					return;
				}
				var mode = splitMode();
				if (mode === "fixed") {
					return;
				}
				var rect = root.getBoundingClientRect();
				var evX = ev.touches ? ev.touches[0].clientX : ev.clientX;
				var evY = ev.touches ? ev.touches[0].clientY : ev.clientY;
				if (mode === "bottom") {
					var y = evY - rect.top;
					var rpct = (y / rect.height) * 100;
					if (rpct < 28) rpct = 28;
					if (rpct > 78) rpct = 78;
					root.style.gridTemplateColumns = "1fr";
					root.style.gridTemplateRows = rpct + "% 6px 1fr";
				} else {
					var x = evX - rect.left;
					var pct = (x / rect.width) * 100;
					if (pct < 28) pct = 28;
					if (pct > 72) pct = 72;
					root.style.gridTemplateRows = "";
					root.style.gridTemplateColumns = pct + "% 6px 1fr";
				}
				// No layout en cada mousemove: el ResizeObserver (debounced) del editor basta.
				ev.preventDefault();
			}

			function onUp() {
				if (!dragging) {
					return;
				}
				dragging = false;
				document.body.classList.remove("mesh-split-dragging");
				document.removeEventListener("mousemove", onMove);
				document.removeEventListener("mouseup", onUp);
				document.removeEventListener("touchmove", onMove);
				document.removeEventListener("touchend", onUp);
				notifyMonacoLayout();
				requestAnimationFrame(notifyMonacoLayout);
			}

			function onDown(ev) {
				if (splitMode() === "fixed") {
					return;
				}
				dragging = true;
				document.body.classList.add("mesh-split-dragging");
				document.addEventListener("mousemove", onMove);
				document.addEventListener("mouseup", onUp);
				document.addEventListener("touchmove", onMove, { passive: false });
				document.addEventListener("touchend", onUp);
				ev.preventDefault();
			}

			gutter.addEventListener("mousedown", onDown);
			gutter.addEventListener("touchstart", onDown, { passive: false });
			// Primer layout tras montar el split (panel aún midiendo).
			requestAnimationFrame(function () {
				notifyMonacoLayout();
				setTimeout(notifyMonacoLayout, 100);
			});
			return true;
		}
	};
})();
