/**
 * Layout del workspace Diamond: pantalla completa y persistencia de modo.
 */
(function () {
	"use strict";

	var KEY_LAYOUT = "diamond.workspaceLayout";
	var KEY_DOCK = "diamond.workspaceDock";

	window.diamondWorkspace = {
		requestFullscreen: function (el) {
			if (!el) {
				return Promise.resolve(false);
			}
			var req = el.requestFullscreen || el.webkitRequestFullscreen;
			if (!req) {
				return Promise.resolve(false);
			}
			return Promise.resolve(req.call(el)).then(function () { return true; }).catch(function () { return false; });
		},
		exitFullscreen: function () {
			var cur = document.fullscreenElement || document.webkitFullscreenElement;
			if (!cur) {
				return Promise.resolve(false);
			}
			var ext = document.exitFullscreen || document.webkitExitFullscreen;
			if (!ext) {
				return Promise.resolve(false);
			}
			return Promise.resolve(ext.call(document)).then(function () { return true; }).catch(function () { return false; });
		},
		isFullscreen: function () {
			return !!(document.fullscreenElement || document.webkitFullscreenElement);
		},
		watchFullscreen: function (dotNet, method) {
			if (!dotNet || !method) {
				return false;
			}
			function notify() {
				try {
					dotNet.invokeMethodAsync(method, !!(document.fullscreenElement || document.webkitFullscreenElement));
				} catch (e) {
					// ignore
				}
			}
			document.addEventListener("fullscreenchange", notify);
			document.addEventListener("webkitfullscreenchange", notify);
			return true;
		},
		loadPrefs: function () {
			var layout = "Split";
			var dock = "Right";
			try {
				layout = localStorage.getItem(KEY_LAYOUT) || layout;
				dock = localStorage.getItem(KEY_DOCK) || dock;
			} catch (e) {
				// ignore
			}
			return { layout: layout, dock: dock };
		},
		clearSplit: function (root) {
			if (!root) {
				return false;
			}
			root.style.gridTemplateColumns = "";
			root.style.gridTemplateRows = "";
			return true;
		},
		savePrefs: function (layout, dock) {
			try {
				if (layout) localStorage.setItem(KEY_LAYOUT, layout);
				if (dock) localStorage.setItem(KEY_DOCK, dock);
			} catch (e) {
				// ignore
			}
			return true;
		}
	};
})();
