/* global maplibregl */
/* Mapa de red SFM. Dos modos:
   - page: clon de horarios (Positron + teselas).
   - overlay: recorte circular, isla más opaca, mar transparente, toponimia grande. */

(function (global) {
  'use strict';

  const DEFAULT_ROUTE_COLOR = '#000000';
  const ISLAND_FILL = '#e8eef4';
  const ISLAND_STROKE = '#1e293b';
  const TRAIN_COLOR = '#e11d48';
  const LINE_LAYOUT = {
    'line-join': 'round',
    'line-cap': 'round',
    'line-sort-key': ['coalesce', ['get', 'priority'], 0]
  };

  const instances = new Map();

  function resolveBase(baseUrl) {
    const page = global.location && global.location.href
      ? global.location.href
      : 'http://localhost/';
    try {
      if (baseUrl) {
        const abs = new URL(baseUrl, page).href;
        return abs.endsWith('/') ? abs : abs + '/';
      }
    } catch {
    }

    const script = document.querySelector('script[src*="sfm-map.js"]');
    if (script && script.src) {
      return new URL('../', script.src).href;
    }
    return new URL('/maps/sfm/', page).href;
  }

  function assetUrl(baseUrl, relativePath) {
    const href = new URL(relativePath, resolveBase(baseUrl)).href;
    return href
      .replace(/%7Bfontstack%7D/gi, '{fontstack}')
      .replace(/%7Brange%7D/gi, '{range}');
  }

  function resolveContainer(container) {
    if (!container) {
      throw new Error('SfmMap: container requerido');
    }
    if (typeof container === 'string') {
      const el = document.getElementById(container);
      if (!el) {
        throw new Error('SfmMap: no existe #' + container);
      }
      return el;
    }
    return container;
  }

  function parseArgs(containerOrOptions, maybeOptions) {
    if (
      containerOrOptions &&
      (typeof containerOrOptions === 'string' || containerOrOptions.nodeType === 1)
    ) {
      return {
        container: resolveContainer(containerOrOptions),
        options: maybeOptions || {}
      };
    }
    const options = containerOrOptions || {};
    return {
      container: resolveContainer(options.container),
      options: options
    };
  }

  function getInstance(container) {
    return instances.get(resolveContainer(container));
  }

  function firstInstance() {
    const iterator = instances.values().next();
    return iterator.done ? null : iterator.value;
  }

  function showStatus(message) {
    const el = document.getElementById('sfm-status');
    if (!el) {
      return;
    }
    if (!message) {
      el.hidden = true;
      el.textContent = '';
      return;
    }
    el.hidden = false;
    el.textContent = message;
  }

  function assignLinePriority(geojson) {
    if (!geojson || !geojson.features) {
      return;
    }
    geojson.features.forEach((feature) => {
      const type = feature.geometry.type.toLowerCase();
      if (type === 'linestring' || type === 'multilinestring') {
        let pointCount = 0;
        if (type === 'linestring') {
          pointCount = feature.geometry.coordinates.length;
        } else {
          pointCount = feature.geometry.coordinates.reduce(
            (acc, coords) => acc + coords.length,
            0
          );
        }
        feature.properties.priority = Math.max(0, 10000 - pointCount);
      } else {
        feature.properties.priority = 0;
      }
    });
  }

  function stationLabel(name) {
    if (!name) {
      return '';
    }
    return String(name).replace(/\s+And[eé]n\s+\S+\s*$/i, '').trim();
  }

  function uniqueStationStops(geojson) {
    const seen = {};
    const features = [];
    geojson.features.forEach((feature) => {
      if (!feature.properties || !feature.properties.stop_id) {
        features.push(feature);
        return;
      }
      const key = feature.properties.parent_station || feature.properties.stop_name || feature.properties.stop_id;
      if (seen[key]) {
        return;
      }
      seen[key] = true;
      feature.properties.stop_label = stationLabel(feature.properties.stop_name);
      features.push(feature);
    });
    return { type: 'FeatureCollection', features: features };
  }

  function clamp(value, min, max) {
    return Math.max(min, Math.min(max, value));
  }

  function overlayMetrics(sizePx) {
    const size = Math.max(160, sizePx || 320);
    return {
      routeText: clamp(size * 0.056, 14, 30),
      stationText: clamp(size * 0.046, 13, 24),
      placeText: clamp(size * 0.052, 14, 26),
      townText: clamp(size * 0.042, 12, 20),
      routeWidth: clamp(size * 0.012, 3, 7),
      outlineWidth: clamp(size * 0.021, 5.2, 12),
      shadowWidth: clamp(size * 0.03, 7.5, 16),
      stopRadius: clamp(size * 0.013, 3.4, 7.5),
      trainRadius: clamp(size * 0.02, 5.5, 11),
      islandOutline: clamp(size * 0.012, 3.2, 7),
      symbolSpacing: clamp(size * 0.26, 58, 140)
    };
  }

  function containerSize(map) {
    const el = map && map.getContainer ? map.getContainer() : null;
    if (!el) {
      return 320;
    }
    return Math.min(el.clientWidth || 0, el.clientHeight || 0) || 320;
  }

  function trySetPaint(map, layerId, prop, value) {
    if (!map.getLayer(layerId)) {
      return;
    }
    map.setPaintProperty(layerId, prop, value);
  }

  function trySetLayout(map, layerId, prop, value) {
    if (!map.getLayer(layerId)) {
      return;
    }
    map.setLayoutProperty(layerId, prop, value);
  }

  function applyOverlayMetrics(instance) {
    if (!instance || !instance.map || instance.mode !== 'overlay') {
      return;
    }
    const map = instance.map;
    const metrics = overlayMetrics(containerSize(map));
    instance.metrics = metrics;
    trySetPaint(map, 'route-line-shadows', 'line-width', metrics.shadowWidth);
    trySetPaint(map, 'route-outlines', 'line-width', metrics.outlineWidth);
    trySetPaint(map, 'routes', 'line-width', metrics.routeWidth);
    trySetPaint(map, 'stops', 'circle-radius', metrics.stopRadius);
    trySetLayout(map, 'route-labels', 'text-size', metrics.routeText);
    trySetLayout(map, 'route-labels', 'symbol-spacing', metrics.symbolSpacing);
    trySetLayout(map, 'stop-labels', 'text-size', metrics.stationText);
    trySetLayout(map, 'context-label-city', 'text-size', metrics.placeText);
    trySetLayout(map, 'context-label-town', 'text-size', metrics.townText);
    trySetLayout(map, 'context-label-village', 'text-size', metrics.townText);
    trySetPaint(map, 'sfm-island-outline', 'line-width', metrics.islandOutline);
    trySetPaint(map, 'sfm-island-outline-light', 'line-width', metrics.islandOutline * 1.6);
    trySetPaint(map, 'sfm-train', 'circle-radius', metrics.trainRadius);
    trySetPaint(map, 'sfm-train-halo', 'circle-radius', metrics.trainRadius * 1.65);
  }

  function extendBounds(bounds, coordinates) {
    if (!coordinates || typeof coordinates[0] !== 'object') {
      bounds.extend(coordinates);
      return;
    }
    coordinates.forEach((item) => extendBounds(bounds, item));
  }

  function getBounds(geojson) {
    const bounds = new maplibregl.LngLatBounds();
    geojson.features.forEach((feature) => {
      extendBounds(bounds, feature.geometry.coordinates);
    });
    return bounds;
  }

  function emptyTrain() {
    return {
      type: 'FeatureCollection',
      features: []
    };
  }

  function trainFeature(lat, lon) {
    return {
      type: 'FeatureCollection',
      features: [
        {
          type: 'Feature',
          properties: {},
          geometry: { type: 'Point', coordinates: [lon, lat] }
        }
      ]
    };
  }

  function transparentStyle(baseUrl) {
    return {
      version: 8,
      name: 'sfm-overlay',
      glyphs: assetUrl(baseUrl, 'fonts/{fontstack}/{range}.pbf'),
      sources: {},
      layers: [
        {
          id: 'background',
          type: 'background',
          paint: { 'background-color': 'rgba(0,0,0,0)' }
        }
      ]
    };
  }

  function localizeStyleUrls(style, baseUrl) {
    style.sprite = assetUrl(baseUrl, 'sprites/ofm');
    style.glyphs = assetUrl(baseUrl, 'fonts/{fontstack}/{range}.pbf');
    return style;
  }

  async function loadPageStyle(baseUrl, offline) {
    const stylePath = offline ? 'styles/positron-offline.json' : 'styles/positron.json';
    const response = await fetch(assetUrl(baseUrl, stylePath));
    if (!response.ok) {
      throw new Error('No se pudo cargar ' + stylePath);
    }
    return localizeStyleUrls(await response.json(), baseUrl);
  }

  function disablePointsOfInterest(map) {
    const layers = map.getStyle().layers || [];
    layers
      .filter((layer) => layer.id.startsWith('poi'))
      .forEach((layer) => {
        map.setLayoutProperty(layer.id, 'visibility', 'none');
      });
  }

  function firstLabelLayerId(map) {
    const layers = map.getStyle().layers || [];
    const found = layers.find(
      (layer) => layer.type === 'symbol' && layer.id.includes('label')
    );
    return found ? found.id : undefined;
  }

  function addIslandLayers(map, island) {
    map.addSource('sfm-island', { type: 'geojson', data: island });
    map.addLayer({
      id: 'sfm-island-fill',
      type: 'fill',
      source: 'sfm-island',
      paint: {
        'fill-color': ISLAND_FILL,
        'fill-opacity': 0.92
      }
    });
    addContextRoadLayers(map);
    map.addLayer({
      id: 'sfm-island-outline-light',
      type: 'line',
      source: 'sfm-island',
      paint: {
        'line-color': '#ffffff',
        'line-width': 5.5,
        'line-opacity': 0.88,
        'line-blur': 0.35
      },
      layout: {
        'line-join': 'round',
        'line-cap': 'round'
      }
    });
    map.addLayer({
      id: 'sfm-island-outline',
      type: 'line',
      source: 'sfm-island',
      paint: {
        'line-color': ISLAND_STROKE,
        'line-width': 3.4,
        'line-opacity': 1
      },
      layout: {
        'line-join': 'round',
        'line-cap': 'round'
      }
    });
  }

  function ensureOpenMapTiles(map) {
    try {
      if (!map.getSource('openmaptiles')) {
        map.addSource('openmaptiles', {
          type: 'vector',
          url: 'https://tiles.openfreemap.org/planet'
        });
      }
      return true;
    } catch (error) {
      return false;
    }
  }

  function addContextRoadLayers(map) {
    if (!ensureOpenMapTiles(map)) {
      return;
    }

    map.addLayer({
      id: 'context-motorway',
      type: 'line',
      source: 'openmaptiles',
      'source-layer': 'transportation',
      minzoom: 6,
      filter: [
        'all',
        ['match', ['geometry-type'], ['LineString', 'MultiLineString'], true, false],
        ['==', ['get', 'class'], 'motorway']
      ],
      layout: { 'line-cap': 'round', 'line-join': 'round' },
      paint: {
        'line-color': '#ffffff',
        'line-width': ['interpolate', ['exponential', 1.4], ['zoom'], 6, 1.5, 12, 4.6]
      }
    });

    map.addLayer({
      id: 'context-primary',
      type: 'line',
      source: 'openmaptiles',
      'source-layer': 'transportation',
      minzoom: 8,
      filter: [
        'all',
        ['match', ['geometry-type'], ['LineString', 'MultiLineString'], true, false],
        ['match', ['get', 'class'], ['primary', 'trunk', 'secondary'], true, false]
      ],
      layout: { 'line-cap': 'round', 'line-join': 'round' },
      paint: {
        'line-color': '#b7c2cb',
        'line-width': ['interpolate', ['exponential', 1.3], ['zoom'], 8, 1.05, 12, 2.9]
      }
    });
  }

  function addContextPlaceLabels(map) {
    if (!ensureOpenMapTiles(map)) {
      return;
    }

    const nameField = [
      'coalesce',
      ['get', 'name'],
      ['get', 'name:ca'],
      ['get', 'name:es'],
      ['get', 'name_en']
    ];

    map.addLayer({
      id: 'context-label-city',
      type: 'symbol',
      source: 'openmaptiles',
      'source-layer': 'place',
      minzoom: 6,
      filter: ['==', ['get', 'class'], 'city'],
      layout: {
        'text-field': nameField,
        'text-font': ['Noto Sans Bold'],
        'text-size': ['interpolate', ['linear'], ['zoom'], 6, 16, 11, 24],
        'text-anchor': 'center',
        'text-padding': 2,
        'text-max-width': 8
      },
      paint: {
        'text-color': '#0f172a',
        'text-halo-color': '#ffffff',
        'text-halo-width': 2.4,
        'text-halo-blur': 0.35
      }
    });

    map.addLayer({
      id: 'context-label-town',
      type: 'symbol',
      source: 'openmaptiles',
      'source-layer': 'place',
      minzoom: 7,
      filter: ['==', ['get', 'class'], 'town'],
      layout: {
        'text-field': nameField,
        'text-font': ['Noto Sans Bold'],
        'text-size': ['interpolate', ['linear'], ['zoom'], 8, 13, 12, 18],
        'text-anchor': 'center',
        'text-padding': 2,
        'text-max-width': 8
      },
      paint: {
        'text-color': '#1e293b',
        'text-halo-color': '#ffffff',
        'text-halo-width': 2.2,
        'text-halo-blur': 0.3
      }
    });

    map.addLayer({
      id: 'context-label-village',
      type: 'symbol',
      source: 'openmaptiles',
      'source-layer': 'place',
      minzoom: 9,
      filter: ['==', ['get', 'class'], 'village'],
      layout: {
        'text-field': nameField,
        'text-font': ['Noto Sans Bold'],
        'text-size': 14,
        'text-anchor': 'center',
        'text-padding': 2,
        'text-max-width': 8
      },
      paint: {
        'text-color': '#334155',
        'text-halo-color': '#ffffff',
        'text-halo-width': 2
      }
    });
  }

  function zoomForSpeed(kmh, factor) {
    const speed = Math.max(0, Math.min(110, kmh || 0));
    const base = 11.4 - (speed / 110) * 2.3;
    let f = factor;
    if (f == null || !(f > 0)) {
      f = 1;
    }
    f = Math.max(0.5, Math.min(2, f));
    return Math.max(7.5, Math.min(14, base * f));
  }

  function addRouteLayers(map, geojson, overlay) {
    const beforeId = overlay ? undefined : firstLabelLayerId(map);
    const metrics = overlay ? overlayMetrics(containerSize(map)) : null;
    const shadowWidth = overlay
      ? metrics.shadowWidth
      : { base: 12, stops: [[14, 20], [18, 42]] };
    const outlineWidth = overlay
      ? metrics.outlineWidth
      : { base: 8, stops: [[14, 12], [18, 32]] };
    const routeWidth = overlay
      ? metrics.routeWidth
      : { base: 4, stops: [[14, 6], [18, 16]] };
    const stopRadius = overlay
      ? metrics.stopRadius
      : { base: 1.75, stops: [[12, 4], [22, 100]] };
    const stopOpacity = overlay
      ? 1
      : ['interpolate', ['linear'], ['zoom'], 13, 0, 13.5, 1];
    const routeText = overlay ? metrics.routeText : 14;
    const routeSpacing = overlay ? metrics.symbolSpacing : 250;
    const stationText = overlay ? metrics.stationText : 13;

    map.addLayer(
      {
        id: 'route-line-shadows',
        type: 'line',
        source: { type: 'geojson', data: geojson },
        paint: {
          'line-color': '#000000',
          'line-opacity': overlay ? 0.22 : 0.3,
          'line-width': shadowWidth,
          'line-blur': overlay ? 2 : { base: 12, stops: [[14, 20], [18, 42]] }
        },
        layout: LINE_LAYOUT,
        filter: ['!has', 'stop_id']
      },
      beforeId
    );

    map.addLayer(
      {
        id: 'route-outlines',
        type: 'line',
        source: { type: 'geojson', data: geojson },
        paint: {
          'line-color': '#FFFFFF',
          'line-opacity': 1,
          'line-width': outlineWidth
        },
        layout: LINE_LAYOUT,
        filter: ['has', 'route_id']
      },
      beforeId
    );

    map.addLayer(
      {
        id: 'routes',
        type: 'line',
        source: { type: 'geojson', data: geojson },
        paint: {
          'line-color': ['coalesce', ['get', 'route_color'], DEFAULT_ROUTE_COLOR],
          'line-opacity': 1,
          'line-width': routeWidth
        },
        layout: LINE_LAYOUT,
        filter: ['has', 'route_id']
      },
      beforeId
    );

    map.addLayer({
      id: 'stops',
      type: 'circle',
      source: { type: 'geojson', data: geojson },
      paint: {
        'circle-color': '#fff',
        'circle-radius': stopRadius,
        'circle-stroke-color': '#3F4A5C',
        'circle-stroke-width': overlay ? 1.6 : 2,
        'circle-opacity': stopOpacity,
        'circle-stroke-opacity': stopOpacity
      },
      filter: ['has', 'stop_id']
    });

    map.addLayer({
      id: 'route-labels',
      type: 'symbol',
      source: { type: 'geojson', data: geojson },
      layout: {
        'symbol-placement': 'line',
        'text-field': ['get', 'route_short_name'],
        'text-size': routeText,
        'text-font': overlay ? ['Noto Sans Bold'] : ['Noto Sans Regular'],
        'symbol-spacing': routeSpacing,
        'text-max-angle': 30
      },
      paint: {
        'text-color': '#000000',
        'text-halo-width': overlay ? 2.8 : 2,
        'text-halo-color': '#ffffff'
      },
      filter: ['has', 'route_short_name']
    });

    if (overlay) {
      map.addLayer({
        id: 'stop-labels',
        type: 'symbol',
        source: { type: 'geojson', data: geojson },
        minzoom: 8.4,
        layout: {
          'text-field': [
            'coalesce',
            ['get', 'stop_label'],
            ['get', 'stop_name']
          ],
          'text-font': ['Noto Sans Bold'],
          'text-size': stationText,
          'text-variable-anchor': ['top', 'bottom', 'left', 'right'],
          'text-radial-offset': 1.15,
          'text-padding': 3,
          'text-optional': true,
          'text-max-width': 8,
          'text-allow-overlap': false,
          'icon-allow-overlap': true
        },
        paint: {
          'text-color': '#0f172a',
          'text-halo-color': '#ffffff',
          'text-halo-width': 2.4,
          'text-halo-blur': 0.25
        },
        filter: ['has', 'stop_id']
      });
    }
  }

  function addTrainLayers(map) {
    map.addSource('sfm-train', { type: 'geojson', data: emptyTrain() });
    map.addLayer({
      id: 'sfm-train-halo',
      type: 'circle',
      source: 'sfm-train',
      paint: {
        'circle-color': '#ffffff',
        'circle-radius': 9,
        'circle-opacity': 0.95
      }
    });
    map.addLayer({
      id: 'sfm-train',
      type: 'circle',
      source: 'sfm-train',
      paint: {
        'circle-color': TRAIN_COLOR,
        'circle-radius': 5.5,
        'circle-stroke-color': '#ffffff',
        'circle-stroke-width': 1.6
      }
    });
  }

  function applyTrain(instance, lat, lon, options) {
    if (!instance || !instance.map || !instance.map.isStyleLoaded()) {
      if (instance) {
        instance.pendingTrain = { lat: lat, lon: lon, options: options || {} };
      }
      return;
    }
    const source = instance.map.getSource('sfm-train');
    if (!source) {
      return;
    }
    source.setData(trainFeature(lat, lon));
    instance.hasTrain = true;
    const settings = options || {};
    if (settings.zoomFactor != null) {
      instance.zoomFactor = settings.zoomFactor;
    }
    if (instance.mode === 'overlay' && settings.follow !== false) {
      const zoom = settings.zoom != null
        ? settings.zoom
        : zoomForSpeed(settings.speed, instance.zoomFactor);
      instance.map.easeTo({
        center: [lon, lat],
        zoom: zoom,
        duration: 700,
        essential: true
      });
    } else if (settings.center && instance.mode !== 'overlay') {
      instance.map.easeTo({ center: [lon, lat], duration: 400 });
    }
  }

  function clearTrainOn(instance) {
    if (!instance) {
      return;
    }
    instance.pendingTrain = null;
    instance.hasTrain = false;
    if (!instance.map || !instance.map.getSource('sfm-train')) {
      return;
    }
    instance.map.getSource('sfm-train').setData(emptyTrain());
  }

  function parseRoutesProperty(value) {
    if (Array.isArray(value)) {
      return value;
    }
    if (typeof value === 'string' && value.length > 0) {
      try {
        return JSON.parse(value);
      } catch {
        return [];
      }
    }
    return [];
  }

  function formatRoute(route) {
    const color = route.route_color || '#000000';
    const text = route.route_text_color || '#FFFFFF';
    return (
      '<div class="map-route-item"><div class="route-color-swatch" style="background-color:' +
      color +
      ';color:' +
      text +
      '">' +
      (route.route_short_name || '') +
      '</div><div>' +
      (route.route_long_name || '') +
      '</div></div>'
    );
  }

  function setupHover(map) {
    map.on('mousemove', (event) => {
      const features = map.queryRenderedFeatures(event.point, {
        layers: ['routes', 'route-outlines', 'stops']
      });
      map.getCanvas().style.cursor = features.length > 0 ? 'pointer' : '';
    });

    map.on('click', (event) => {
      const bbox = [
        [event.point.x - 5, event.point.y - 5],
        [event.point.x + 5, event.point.y + 5]
      ];
      const stopFeatures = map.queryRenderedFeatures(bbox, { layers: ['stops'] });
      if (stopFeatures.length > 0) {
        const feature = stopFeatures[0];
        const routes = parseRoutesProperty(feature.properties.routes);
        let html = '<div class="popup-title">' + (feature.properties.stop_name || '') + '</div>';
        routes.forEach((route) => {
          html += formatRoute(route);
        });
        new maplibregl.Popup()
          .setLngLat(feature.geometry.coordinates)
          .setHTML(html)
          .addTo(map);
        return;
      }
      const routeFeatures = map.queryRenderedFeatures(bbox, {
        layers: ['routes', 'route-outlines']
      });
      if (routeFeatures.length === 0) {
        return;
      }
      const seen = {};
      const unique = [];
      routeFeatures.forEach((feature) => {
        const key = feature.properties.route_short_name;
        if (key && !seen[key]) {
          seen[key] = true;
          unique.push(feature);
        }
      });
      new maplibregl.Popup()
        .setLngLat(event.lngLat)
        .setHTML(unique.map((feature) => formatRoute(feature.properties)).join(''))
        .addTo(map);
    });
  }

  function waitForSize(element) {
    return new Promise((resolve) => {
      let tries = 0;
      const tick = () => {
        if (element.clientWidth > 8 && element.clientHeight > 8) {
          resolve();
          return;
        }
        tries += 1;
        if (tries >= 40) {
          resolve();
          return;
        }
        global.requestAnimationFrame(tick);
      };
      tick();
    });
  }

  async function create(containerOrOptions, maybeOptions) {
    const parsed = parseArgs(containerOrOptions, maybeOptions);
    const container = parsed.container;
    const settings = parsed.options;
    const overlay = settings.mode === 'overlay';
    const baseUrl = resolveBase(settings.baseUrl);

    if (typeof maplibregl === 'undefined') {
      throw new Error('MapLibre no está cargado');
    }

    await waitForSize(container);
    destroy(container);
    if (!overlay) {
      showStatus('Cargando mapa…');
    }

    const netResponse = await fetch(assetUrl(baseUrl, 'data/sfm-network.geojson'));
    if (!netResponse.ok) {
      showStatus('No se pudo cargar la red SFM');
      throw new Error('GeoJSON SFM no disponible');
    }
    let network = await netResponse.json();
    assignLinePriority(network);
    if (overlay) {
      network = uniqueStationStops(network);
    }

    let island = null;
    if (overlay) {
      const islandResponse = await fetch(assetUrl(baseUrl, 'data/mallorca.geojson'));
      if (!islandResponse.ok) {
        throw new Error('GeoJSON Mallorca no disponible');
      }
      island = await islandResponse.json();
    }

    let style;
    if (overlay) {
      style = transparentStyle(baseUrl);
    } else {
      let useOffline = !!settings.offline;
      if (!useOffline) {
        try {
          const probe = await fetch('https://tiles.openfreemap.org/planet', {
            signal: AbortSignal.timeout(2500)
          });
          if (!probe.ok) {
            useOffline = true;
          }
        } catch {
          useOffline = true;
        }
      }
      try {
        style = await loadPageStyle(baseUrl, useOffline);
      } catch {
        style = transparentStyle(baseUrl);
        style.layers[0].paint['background-color'] = 'rgb(242,243,240)';
      }
    }

    const fitSource = island || network;
    const bounds = getBounds(fitSource);

    const map = new maplibregl.Map({
      container: container,
      style: style,
      center: bounds.getCenter(),
      zoom: overlay ? zoomForSpeed(0, settings.zoomFactor) : 11,
      attributionControl: !overlay,
      interactive: !overlay,
      dragPan: !overlay,
      scrollZoom: !overlay,
      boxZoom: !overlay,
      doubleClickZoom: !overlay,
      keyboard: !overlay,
      touchZoomRotate: !overlay,
      fadeDuration: 0,
      canvasContextAttributes: { alpha: true, antialias: true }
    });

    if (!overlay && settings.showNavigation !== false) {
      map.addControl(new maplibregl.NavigationControl(), 'top-right');
    }

    const instance = {
      map: map,
      mode: overlay ? 'overlay' : 'page',
      pendingTrain: null,
      hasTrain: false,
      resizeObserver: null,
      zoomFactor: settings.zoomFactor
    };
    instances.set(container, instance);

    if (typeof ResizeObserver !== 'undefined') {
      instance.resizeObserver = new ResizeObserver(() => {
        if (!instance.map) {
          return;
        }
        instance.map.resize();
        applyOverlayMetrics(instance);
      });
      instance.resizeObserver.observe(container);
    }

    map.on('load', () => {
      map.resize();
      if (overlay) {
        addIslandLayers(map, island);
      } else {
        disablePointsOfInterest(map);
      }
      addRouteLayers(map, network, overlay);
      if (overlay) {
        addContextPlaceLabels(map);
      }
      addTrainLayers(map);
      if (overlay) {
        applyOverlayMetrics(instance);
      }
      if (!overlay) {
        setupHover(map);
      }
      const overlayPad = Math.round(
        Math.min(container.clientWidth, container.clientHeight) * 0.16
      );
      if (!overlay || !instance.pendingTrain) {
        map.fitBounds(bounds, {
          padding: overlay ? Math.max(24, overlayPad) : 40,
          duration: 0
        });
      }
      if (instance.pendingTrain) {
        applyTrain(
          instance,
          instance.pendingTrain.lat,
          instance.pendingTrain.lon,
          instance.pendingTrain.options
        );
        instance.pendingTrain = null;
      }
      showStatus('');
    });

    return map;
  }

  function setTrainPosition(containerOrLat, latOrLon, lonOrOptions, options) {
    if (typeof containerOrLat === 'number') {
      applyTrain(firstInstance(), containerOrLat, latOrLon, lonOrOptions);
      return;
    }
    applyTrain(getInstance(containerOrLat), latOrLon, lonOrOptions, options);
  }

  function clearTrain(container) {
    if (!container) {
      instances.forEach((instance) => clearTrainOn(instance));
      return;
    }
    clearTrainOn(getInstance(container));
  }

  function resize(container) {
    const instance = container ? getInstance(container) : firstInstance();
    if (instance && instance.map) {
      instance.map.resize();
      applyOverlayMetrics(instance);
    }
  }

  function destroy(container) {
    if (!container) {
      instances.forEach((_, el) => destroy(el));
      return;
    }
    const el = resolveContainer(container);
    const instance = instances.get(el);
    if (!instance) {
      return;
    }
    if (instance.resizeObserver) {
      instance.resizeObserver.disconnect();
    }
    if (instance.map) {
      instance.map.remove();
    }
    instances.delete(el);
  }

  global.SfmMap = {
    create: create,
    setTrainPosition: setTrainPosition,
    clearTrain: clearTrain,
    resize: resize,
    destroy: destroy
  };
})(window);
