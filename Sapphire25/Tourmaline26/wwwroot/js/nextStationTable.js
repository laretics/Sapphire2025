window.NstTable = {
	_obs: new Map(),

	observe: function (el, netRef) {
		if (!el)
			return;
		const existing = this._obs.get(el);
		if (existing) {
			existing.netRef = netRef;
			this._run(el);
			return;
		}

		const self = this;
		const ro = new ResizeObserver(function () { self._run(el); });
		ro.observe(el);
		this._obs.set(el, { ro: ro, netRef: netRef, last: -1 });
		requestAnimationFrame(function () { self._run(el); });
	},

	layout: function (el) {
		const rec = this._obs.get(el);
		if (rec)
			rec.last = -1;
		this._run(el);
	},

	dispose: function (el) {
		const rec = this._obs.get(el);
		if (!rec)
			return;
		rec.ro.disconnect();
		this._obs.delete(el);
	},

	_run: function (el) {
		if (!el)
			return;
		this._fitDestinations(el);
		const rec = this._obs.get(el);
		if (!rec || !rec.netRef)
			return;
		const n = this._countRows(el);
		if (n <= 0 || n === rec.last)
			return;
		rec.last = n;
		rec.netRef.invokeMethodAsync("OnTableFit", n);
	},

	_countRows: function (root) {
		const wrap = root.classList && root.classList.contains("nst-table-wrap")
			? root
			: root.querySelector(".nst-table-wrap");
		const board = root.querySelector ? (root.querySelector(".nst-board") || root) : root;
		if (!wrap)
			return 0;
		// El wrap se encoge al contenido: medir el tablero (hueco real), no la tabla.
		const avail = Math.max(
			board.clientHeight || 0,
			root.clientHeight || 0);
		const row = wrap.querySelector("tbody tr");
		if (avail <= 0)
			return 0;
		const h = row ? row.getBoundingClientRect().height : 0;
		const rowH = h >= 8 ? h : Math.max(36, avail * 0.08);
		return Math.max(1, Math.floor((avail + 0.25) / rowH));
	},

	_fitDestinations: function (root) {
		const nodes = root.querySelectorAll(".nst-dest-text");
		for (let i = 0; i < nodes.length; i++) {
			const el = nodes[i];
			el.style.fontSize = "";
			if (el.scrollWidth > el.clientWidth + 1)
				el.style.fontSize = "87%";
		}
	}
};
