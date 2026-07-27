/**
 * Divisor horizontal redimensionable entre malla y editor.
 */
(function () {
	"use strict";

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

			function onMove(ev) {
				if (!dragging) {
					return;
				}
				var rect = root.getBoundingClientRect();
				var x = (ev.touches ? ev.touches[0].clientX : ev.clientX) - rect.left;
				var pct = (x / rect.width) * 100;
				if (pct < 28) pct = 28;
				if (pct > 72) pct = 72;
				root.style.gridTemplateColumns = pct + "% 6px 1fr";
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
			}

			function onDown(ev) {
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
			return true;
		}
	};
})();
