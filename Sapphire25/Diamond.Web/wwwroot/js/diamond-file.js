/**
 * Carga/guardado de scripts de demanda en disco local.
 * Prefer File System Access API; fallback a <input type=file> y descarga.
 */
(function () {
	"use strict";

	var fileHandle = null;
	var fileName = null;

	function supportsFsa() {
		return typeof window.showOpenFilePicker === "function"
			&& typeof window.showSaveFilePicker === "function";
	}

	function clearHandle() {
		fileHandle = null;
		fileName = null;
	}

	function setHandle(handle, name) {
		fileHandle = handle || null;
		fileName = name || (handle && handle.name) || null;
	}

	async function readHandle(handle) {
		var file = await handle.getFile();
		var text = await file.text();
		return { name: file.name || "script.ddm", content: text, hasHandle: true };
	}

	function pickFileFallback() {
		return new Promise(function (resolve) {
			var input = document.createElement("input");
			input.type = "file";
			input.accept = ".ddm,.diamond,.txt,text/plain";
			input.style.display = "none";
			document.body.appendChild(input);
			input.addEventListener("change", function () {
				var file = input.files && input.files[0];
				if (!file) {
					document.body.removeChild(input);
					resolve(null);
					return;
				}
				var reader = new FileReader();
				reader.onload = function () {
					document.body.removeChild(input);
					clearHandle();
					fileName = file.name;
					resolve({
						name: file.name,
						content: String(reader.result || ""),
						hasHandle: false
					});
				};
				reader.onerror = function () {
					document.body.removeChild(input);
					resolve(null);
				};
				reader.readAsText(file, "utf-8");
			}, { once: true });
			input.click();
		});
	}

	function downloadText(name, content) {
		var blob = new Blob([content], { type: "text/plain;charset=utf-8" });
		var url = URL.createObjectURL(blob);
		var a = document.createElement("a");
		a.href = url;
		a.download = name || "script.ddm";
		document.body.appendChild(a);
		a.click();
		document.body.removeChild(a);
		setTimeout(function () { URL.revokeObjectURL(url); }, 1500);
		fileName = name || "script.ddm";
		return { name: fileName, saved: true, hasHandle: false, method: "download" };
	}

	async function writeHandle(handle, content) {
		var writable = await handle.createWritable();
		await writable.write(content);
		await writable.close();
		setHandle(handle, handle.name);
		return { name: handle.name, saved: true, hasHandle: true, method: "fsa" };
	}

	window.diamondFile = {
		supportsFsa: supportsFsa,

		getState: function () {
			return {
				name: fileName,
				hasHandle: !!fileHandle,
				supportsFsa: supportsFsa()
			};
		},

		clear: function () {
			clearHandle();
			return true;
		},

		open: async function () {
			if (supportsFsa()) {
				try {
					var handles = await window.showOpenFilePicker({
						multiple: false,
						types: [{
							description: "Script Diamond demand",
							accept: {
								"text/plain": [".ddm", ".diamond", ".txt"]
							}
						}],
						excludeAcceptAllOption: false
					});
					if (!handles || !handles.length) {
						return null;
					}
					var handle = handles[0];
					var result = await readHandle(handle);
					setHandle(handle, result.name);
					return result;
				} catch (e) {
					if (e && (e.name === "AbortError" || e.name === "NotAllowedError")) {
						return null;
					}
					// Fallback si el picker falla
					return await pickFileFallback();
				}
			}
			return await pickFileFallback();
		},

		/**
		 * Guarda en el handle actual; si no hay, Save As.
		 */
		save: async function (content, suggestedName) {
			content = content == null ? "" : String(content);
			if (fileHandle && supportsFsa()) {
				try {
					return await writeHandle(fileHandle, content);
				} catch (e) {
					if (e && e.name === "AbortError") {
						return null;
					}
					// reintentar save as
				}
			}
			return await window.diamondFile.saveAs(content, suggestedName || fileName || "script.ddm");
		},

		saveAs: async function (content, suggestedName) {
			content = content == null ? "" : String(content);
			var name = suggestedName || fileName || "script.ddm";
			if (!/\.(ddm|diamond|txt)$/i.test(name)) {
				name = name + ".ddm";
			}

			if (supportsFsa()) {
				try {
					var handle = await window.showSaveFilePicker({
						suggestedName: name,
						types: [{
							description: "Script Diamond demand",
							accept: {
								"text/plain": [".ddm", ".diamond", ".txt"]
							}
						}]
					});
					return await writeHandle(handle, content);
				} catch (e) {
					if (e && (e.name === "AbortError" || e.name === "NotAllowedError")) {
						return null;
					}
					return downloadText(name, content);
				}
			}
			return downloadText(name, content);
		},

		/**
		 * Abre un archivo binario (p. ej. malla .dmesh). Devuelve { name, base64 }.
		 */
		openBinary: async function (acceptExtensions) {
			var accept = acceptExtensions || ".dmesh";
			if (supportsFsa()) {
				try {
					var handles = await window.showOpenFilePicker({
						multiple: false,
						types: [{
							description: "Malla Diamond planificada",
							accept: {
								"application/octet-stream": [".dmesh"]
							}
						}],
						excludeAcceptAllOption: false
					});
					if (!handles || !handles.length) {
						return null;
					}
					var file = await handles[0].getFile();
					var buf = await file.arrayBuffer();
					return { name: file.name || "malla.dmesh", base64: arrayBufferToBase64(buf) };
				} catch (e) {
					if (e && (e.name === "AbortError" || e.name === "NotAllowedError")) {
						return null;
					}
				}
			}
			return await pickBinaryFallback(accept);
		},

		/**
		 * Descarga bytes (Uint8Array o array de números) como archivo binario.
		 */
		saveBinary: async function (bytes, suggestedName) {
			var name = suggestedName || "malla.dmesh";
			if (!/\.dmesh$/i.test(name)) {
				name = name + ".dmesh";
			}
			var u8;
			if (bytes instanceof Uint8Array) {
				u8 = bytes;
			} else if (Array.isArray(bytes)) {
				u8 = new Uint8Array(bytes);
			} else if (bytes && bytes.buffer) {
				u8 = new Uint8Array(bytes.buffer, bytes.byteOffset || 0, bytes.byteLength || bytes.length);
			} else {
				return null;
			}
			var blob = new Blob([u8], { type: "application/octet-stream" });
			var url = URL.createObjectURL(blob);
			var a = document.createElement("a");
			a.href = url;
			a.download = name;
			document.body.appendChild(a);
			a.click();
			document.body.removeChild(a);
			setTimeout(function () { URL.revokeObjectURL(url); }, 1500);
			return { name: name, saved: true, method: "download" };
		}
	};

	function arrayBufferToBase64(buffer) {
		var bytes = new Uint8Array(buffer);
		var binary = "";
		var chunk = 0x8000;
		var i = 0;
		while (i < bytes.length) {
			var sub = bytes.subarray(i, Math.min(i + chunk, bytes.length));
			binary += String.fromCharCode.apply(null, sub);
			i += chunk;
		}
		return btoa(binary);
	}

	function pickBinaryFallback(accept) {
		return new Promise(function (resolve) {
			var input = document.createElement("input");
			input.type = "file";
			input.accept = accept || ".dmesh";
			input.style.display = "none";
			document.body.appendChild(input);
			input.addEventListener("change", function () {
				var file = input.files && input.files[0];
				if (!file) {
					document.body.removeChild(input);
					resolve(null);
					return;
				}
				var reader = new FileReader();
				reader.onload = function () {
					document.body.removeChild(input);
					var buf = reader.result;
					resolve({
						name: file.name,
						base64: arrayBufferToBase64(buf)
					});
				};
				reader.onerror = function () {
					document.body.removeChild(input);
					resolve(null);
				};
				reader.readAsArrayBuffer(file);
			}, { once: true });
			input.click();
		});
	}
})();
