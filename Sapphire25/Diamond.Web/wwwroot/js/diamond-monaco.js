/**
 * Lenguaje Monaco "diamond-demand" + tema oscuro "diamond-dark".
 * Compatible con la carga AMD de BlazorMonaco (loader.js + require).
 */
(function () {
	"use strict";

	var registered = false;
	var registering = null;

	function defineTheme() {
		monaco.editor.defineTheme("diamond-dark", {
			base: "vs-dark",
			inherit: true,
			rules: [
				{ token: "comment", foreground: "64748b", fontStyle: "italic" },
				{ token: "keyword", foreground: "c4b5fd" },
				{ token: "string", foreground: "86efac" },
				{ token: "number", foreground: "fbbf24" },
				{ token: "regexp", foreground: "fbbf24" },
				{ token: "operator", foreground: "94a3b8" },
				{ token: "type", foreground: "38bdf8" },
				{ token: "type.identifier", foreground: "38bdf8" },
				{ token: "identifier", foreground: "e2e8f0" },
				{ token: "delimiter", foreground: "94a3b8" }
			],
			colors: {
				"editor.background": "#0f1419",
				"editor.foreground": "#e2e8f0",
				"editor.lineHighlightBackground": "#1a2332",
				"editorLineNumber.foreground": "#64748b",
				"editorLineNumber.activeForeground": "#94a3b8",
				"editorCursor.foreground": "#38bdf8",
				"editor.selectionBackground": "#334155",
				"editorWidget.background": "#1a2332",
				"editorGutter.background": "#0f1419"
			}
		});
	}

	function defineLanguage() {
		try {
			monaco.languages.register({
				id: "diamond-demand",
				extensions: [".ddm", ".diamond"],
				aliases: ["Diamond Demand", "diamond-demand", "Diamond"]
			});
		} catch (e) {
			// already registered
		}

		monaco.languages.setLanguageConfiguration("diamond-demand", {
			comments: { lineComment: "#" },
			brackets: [["(", ")"]],
			autoClosingPairs: [
				{ open: "\"", close: "\"" },
				{ open: "(", close: ")" }
			],
			surroundingPairs: [
				{ open: "\"", close: "\"" },
				{ open: "(", close: ")" }
			]
		});

		// Construir regex con RegExp() evita ambiguedades del lexer con literales /.../
		var reComment = /#.*$/;
		var reWhite = /[ \t\r\n]+/;
		var reStringBad = /\"([^\"\\]|\\.)*$/;
		var reStringOpen = /\"/;
		var reArrow = /->/;
		var reKeyword = new RegExp(
			"\\b(?:plan|require|req|delete|del|all|any|overlap|journey|both|ways|using|as|from|to|days|on|stops|skip|dwell|cross|at|color|colour|with|con|region|every|min|mins|minutes|per|hour|hours)\\b"
		);
		var reHexColor = /#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})\b/;
		var reDay = new RegExp(
			"\\b(?:lab|laborables|fes|festivos|all|todos|daily|weekday|weekdays|weekend|we|" +
			"lun|mar|mie|jue|vie|sab|dom|mon|tue|wed|thu|fri|sat|sun|" +
			"monday|tuesday|wednesday|thursday|friday|saturday|sunday)\\b"
		);
		var reDayRange = new RegExp(
			"\\b(?:mon|tue|wed|thu|fri|sat|sun|lun|mar|mie|jue|vie|sab|dom)-" +
			"(?:mon|tue|wed|thu|fri|sat|sun|lun|mar|mie|jue|vie|sab|dom)\\b"
		);
		var reTime = /\b\d{1,2}:\d{2}(-\d{1,2}:\d{2})?\b/;
		var rePerHour = /\b\d+(\.\d+)?\s*\/\s*h\b/;
		var reDuration = /\b\d+(\.\d+)?(min|mins|minutes|s|sec|secs|seconds)\b/;
		var reNumber = /\b\d+(\.\d+)?\b/;
		var reIdent = new RegExp("[A-Za-z_][\\w.\\-]*");
		var reDelim = new RegExp("[=,:;()]");
		var reStringBody = /[^\\"]+/;
		var reStringEsc = /\\./;
		var reStringClose = /\"/;

		monaco.languages.setMonarchTokensProvider("diamond-demand", {
			defaultToken: "",
			ignoreCase: true,
			tokenizer: {
				root: [
					// Hex color antes que comentario (#rrggbb no es # comentario)
					[reHexColor, "number"],
					[reComment, "comment"],
					[reWhite, "white"],
					[reStringBad, "string.invalid"],
					[reStringOpen, "string", "@string"],
					[reArrow, "operator"],
					[reKeyword, "keyword"],
					[reDay, "type"],
					[reDayRange, "type"],
					[reTime, "number"],
					[rePerHour, "number"],
					[reDuration, "number"],
					[reNumber, "number"],
					[reIdent, "identifier"],
					[reDelim, "delimiter"]
				],
				string: [
					[reStringBody, "string"],
					[reStringEsc, "string.escape.invalid"],
					[reStringClose, "string", "@pop"]
				]
			}
		});
	}

	function registerNow() {
		if (typeof monaco === "undefined" || !monaco.languages || !monaco.editor) {
			return false;
		}

		if (!registered) {
			defineLanguage();
			defineTheme();
			registered = true;
		}

		try {
			monaco.editor.setTheme("diamond-dark");
		} catch (e) {
			// ignore
		}

		return true;
	}

	function ensureLanguageAsync() {
		if (registerNow()) {
			return Promise.resolve(true);
		}

		if (registering) {
			return registering;
		}

		registering = new Promise(function (resolve) {
			var attempts = 0;
			var maxAttempts = 100;

			function finish(ok) {
				registering = null;
				resolve(ok);
			}

			function retry() {
				attempts++;
				if (attempts >= maxAttempts) {
					finish(false);
					return;
				}
				setTimeout(tryRegister, 50);
			}

			function tryRegister() {
				if (registerNow()) {
					finish(true);
					return;
				}

				if (typeof require === "function") {
					try {
						require(["vs/editor/editor.main"], function () {
							if (registerNow()) {
								finish(true);
							} else {
								retry();
							}
						});
						return;
					} catch (e) {
						// fallthrough
					}
				}

				retry();
			}

			tryRegister();
		});

		return registering;
	}

	function applyToEditorAsync() {
		return ensureLanguageAsync().then(function (ok) {
			if (!ok || typeof monaco === "undefined") {
				return false;
			}

			try {
				monaco.editor.setTheme("diamond-dark");
			} catch (e) {
				// ignore
			}

			var models = monaco.editor.getModels();
			var i;
			for (i = 0; i < models.length; i++) {
				try {
					monaco.editor.setModelLanguage(models[i], "diamond-demand");
				} catch (e) {
					// ignore
				}
			}

			var editors = monaco.editor.getEditors ? monaco.editor.getEditors() : [];
			for (i = 0; i < editors.length; i++) {
				try {
					editors[i].updateOptions({ theme: "diamond-dark" });
				} catch (e) {
					// ignore
				}
			}

			return true;
		});
	}

	/**
	 * Marca errores/avisos en el modelo Monaco activo (como un IDE).
	 * markers: [{ line, column?, endLine?, endColumn?, message, severity: 'error'|'warning'|'info' }]
	 */
	function setMarkers(markers) {
		if (typeof monaco === "undefined" || !monaco.editor) {
			return false;
		}

		var list = markers || [];
		var models = monaco.editor.getModels();
		if (!models || models.length === 0) {
			return false;
		}

		var model = models[0];
		var monacoMarkers = [];
		var i;
		for (i = 0; i < list.length; i++) {
			var m = list[i] || {};
			var line = parseInt(m.line, 10);
			if (!line || line < 1) {
				line = 1;
			}

			var endLine = parseInt(m.endLine, 10);
			if (!endLine || endLine < line) {
				endLine = line;
			}

			var col = parseInt(m.column, 10);
			if (!col || col < 1) {
				col = 1;
			}

			var endCol = parseInt(m.endColumn, 10);
			if (!endCol || endCol < col) {
				// Marcar toda la línea
				var lineLen = 1;
				try {
					var content = model.getLineContent(line);
					lineLen = Math.max(1, (content ? content.length : 0) + 1);
				} catch (e) {
					lineLen = 200;
				}
				endCol = lineLen;
			}

			var severity = monaco.MarkerSeverity.Error;
			if (m.severity === "warning") {
				severity = monaco.MarkerSeverity.Warning;
			} else if (m.severity === "info") {
				severity = monaco.MarkerSeverity.Info;
			}

			monacoMarkers.push({
				severity: severity,
				message: m.message || "Error",
				startLineNumber: line,
				startColumn: col,
				endLineNumber: endLine,
				endColumn: endCol
			});
		}

		monaco.editor.setModelMarkers(model, "diamond-demand", monacoMarkers);
		return true;
	}

	function clearMarkers() {
		return setMarkers([]);
	}

	window.diamondMonaco = {
		ensureLanguage: function () {
			return registerNow();
		},
		ensureLanguageAsync: ensureLanguageAsync,
		applyToEditor: applyToEditorAsync,
		setMarkers: setMarkers,
		clearMarkers: clearMarkers
	};

	if (document.readyState === "loading") {
		document.addEventListener("DOMContentLoaded", function () {
			ensureLanguageAsync();
		});
	} else {
		ensureLanguageAsync();
	}
})();
