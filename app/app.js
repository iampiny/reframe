(function () {
  'use strict';

  // Shared layout tokens and named elements define the TV UI.
  function uiNode(name) { return document.querySelector('[data-ui="' + name + '"]'); }
  var uiLayout = JSON.parse(document.getElementById('ui-layout').textContent);
  var device = window.frameDevice;
  var stage = document.body.firstElementChild;
  stage.id = 'frame-home';
  stage.className = 'frame-home';
  var bindings = [
    ['netflix-tile', 'Netflix', 'org.tizen.netflix-app'],
    ['youtube-tile', 'YouTube', '9Ur5IzDKqV.TizenYouTube'],
    ['plex-tile', 'Plex', 'kIciSQlYEM.plex'],
    ['geforce-now-tile', 'GeForce NOW', 'w0wZu3xkQq.GeForceNOW'],
    ['artwork-action', 'Artwork', 'org.tizen.art-app'],
    ['apps-action', 'Apps', 'com.samsung.tv.store'],
    ['settings-action', 'Settings', 'org.tizen.menu'],
    ['samsung-ui-action', 'Samsung UI', 'com.samsung.tv.csfs']
  ];
  bindings.forEach(function (binding, index) {
    var layer = uiNode(binding[0]);
    var button = document.createElement('button');
    Array.prototype.forEach.call(layer.attributes, function (attribute) { button.setAttribute(attribute.name, attribute.value); });
    button.type = 'button';
    button.className = 'tile' + (index >= 4 ? ' utility' : '');
    button.setAttribute('aria-label', binding[1]);
    button.setAttribute('data-name', binding[1]);
    button.setAttribute('data-app', binding[2]);
    if (index < 4) {
      var card = document.createElement('span');
      card.className = 'card';
      while (layer.firstChild) card.appendChild(layer.firstChild);
      button.appendChild(card);
    } else {
      while (layer.firstChild) button.appendChild(layer.firstChild);
    }
    layer.parentNode.replaceChild(button, layer);
  });
  uiNode('sidebar-always-open').className = 'sidebar';
  uiNode('streaming-apps').className = 'row primary';
  uiNode('tv-controls').className = 'utilities';
  uiNode('navigation-viewport').className = 'navigation-viewport';
  uiNode('pinned-navigation-list').className = 'navigation-list';
  uiNode('selected-app-preview').className = 'hero';
  uiNode('preview-eyebrow').id = 'preview-caption';
  uiNode('preview-title').id = 'preview-title';
  uiNode('preview-description').id = 'preview-description';
  uiNode('clock').id = 'clock';
  document.getElementById('clock').className = 'clock';
  document.getElementById('clock').setAttribute('aria-label', 'Current time');
  var footer = document.createElement('footer');
  footer.innerHTML = '<span id="status" role="status" aria-live="polite"></span>';
  stage.appendChild(footer);
  // A single scale preserves the TV's 1920 × 1080 design, including SVGs and type.
  function fitStage() {
    var inset = device.screenInset;
    var scale = Math.min((window.innerWidth - inset * 2) / 1920, (window.innerHeight - inset * 2) / 1080);
    stage.style.position = 'absolute';
    stage.style.left = ((window.innerWidth - 1920 * scale) / 2) + 'px';
    stage.style.top = ((window.innerHeight - 1080 * scale) / 2) + 'px';
    stage.style.transform = 'scale(' + scale + ')';
  }
  window.addEventListener('resize', fitStage);
  fitStage();
  var buttons = Array.prototype.slice.call(document.querySelectorAll('.tile'));
  var selected = 0;
  var status = document.getElementById('status');
  stage.classList.add('home-stage');
  var launching = false, pendingActivation;
  var overviewFrame;
  var clockTimer;
  var viewMode = 'home';
  var pendingHomeFocus;
  var primary = uiNode('streaming-apps');
  var tileTemplate = primary.firstElementChild.cloneNode(true);
  var enterDown = false;
  var catalogBusy = false;
  var devicePending = [], homeSuspended = true;
  var assetTimer, reportTimer, artworkReadyAt = 0;
  var channelKeys = device.channelKeys;
  var defaultPins = bindings.slice(0, 4).map(function (binding) { return binding[2]; });
  var savedOrder = defaultPins.slice();
  try {
    var pins = JSON.parse(localStorage.getItem('framePinnedApps'));
    if (Array.isArray(pins) && pins.every(function (id) { return typeof id === 'string'; })) savedOrder = pins;
  } catch (ignore) {}
  // Backlight controls use the current TV picture profile; Art remains native.
  var brightnessControl = uiNode('brightness-control');
  var brightnessValue = null;
  var brightnessBusy = false;
  var automaticBrightness = null, pendingAutomaticToggle = false;
  var brightnessAuto = uiNode('brightness-auto-button');
  brightnessAuto.tabIndex = 0;
  brightnessAuto.setAttribute('role', 'button');
  brightnessAuto.setAttribute('aria-label', 'Automatic brightness');
  brightnessControl.className = 'brightness-control';
  brightnessControl.tabIndex = 0;
  brightnessControl.setAttribute('role', 'group');
  var brightnessTrack = uiNode('brightness-track');
  brightnessTrack.setAttribute('role', 'slider');
  brightnessTrack.setAttribute('aria-label', 'Brightness level');
  brightnessControl.setAttribute('aria-label', 'Brightness');
  brightnessTrack.setAttribute('aria-orientation', 'vertical');
  brightnessTrack.setAttribute('aria-valuemin', '0');
  brightnessTrack.setAttribute('aria-valuemax', '100');
  function renderBrightness() {
    var level = brightnessValue === null ? 0 : brightnessValue / 50;
    var track = uiLayout.brightness.track, thumb = uiLayout.brightness.thumb;
    var top = track.y + track.height * (1 - level);
    uiNode('brightness-fill').style.height = (track.height * level) + 'px';
    uiNode('brightness-fill').style.top = top + 'px';
    uiNode('brightness-thumb').style.top = (top - thumb.height / 2) + 'px';
    brightnessControl.setAttribute('aria-disabled', String(brightnessValue === null));
    brightnessControl.setAttribute('aria-busy', String(brightnessBusy));
    var pressed = String(automaticBrightness === true);
    if (brightnessAuto.getAttribute('aria-pressed') !== pressed) brightnessAuto.setAttribute('aria-pressed', pressed);
    var disabled = String(automaticBrightness === null && !brightnessBusy);
    if (brightnessAuto.getAttribute('aria-disabled') !== disabled) brightnessAuto.setAttribute('aria-disabled', disabled);
    brightnessAuto.setAttribute('aria-busy', String(brightnessBusy));
    if (brightnessValue !== null) brightnessTrack.setAttribute('aria-valuenow', String(brightnessValue * 2));
    else brightnessTrack.removeAttribute('aria-valuenow');
  }
  function requestBrightness(value, automatic) {
    if (brightnessBusy || homeSuspended) return;
    var previous = brightnessValue;
    brightnessBusy = true;
    // Move immediately; the reply confirms this position or marks it unavailable.
    if (value !== undefined) { brightnessValue = value; automaticBrightness = false; }
    if (automatic !== undefined) automaticBrightness = automatic;
    renderBrightness();
    var values = {};
    if (value !== undefined) { values.value = String(value); values.expected = String(previous); }
    if (automatic !== undefined) values.automatic = automatic ? '1' : '0';
    deviceRequest(automatic !== undefined ? 'set-auto-brightness' : value === undefined ? 'get-brightness' : 'set-brightness', values, 8000)
      .then(function (reply) {
        if (reply.error === 'Art Mode controls its own brightness.') throw new Error(reply.error);
        var result = Number(reply.brightness);
        if (!Number.isInteger(result) || result < 0 || result > 50) throw new Error('Invalid brightness');
        brightnessValue = result;
        automaticBrightness = reply.automatic === '0' ? false : reply.automatic === '1' ? true : null;
      }).catch(function (error) {
        brightnessValue = null; automaticBrightness = null;
        message(error.message === 'Art Mode controls its own brightness.' ? error.message : 'Brightness unavailable.');
      })
      .then(function () {
        brightnessBusy = false; renderBrightness();
        if (pendingAutomaticToggle) { pendingAutomaticToggle = false; toggleAutomaticBrightness(); }
      });
  }
  function adjustBrightness(direction) {
    // Each 10% stop maps exactly to five of the TV’s 0–50 backlight levels.
    if (brightnessValue !== null) requestBrightness(Math.max(0, Math.min(50, (Math.round(brightnessValue / 5) + direction) * 5)));
  }
  function toggleAutomaticBrightness() {
    // Keep a quick press while confirmation is pending. Two presses cancel each
    // other; the confirmed state supplies the next target, never a guessed value.
    if (brightnessBusy) { pendingAutomaticToggle = !pendingAutomaticToggle; return; }
    if (automaticBrightness !== null) requestBrightness(undefined, !automaticBrightness);
  }
  brightnessAuto.addEventListener('focus', function () { stage.classList.add('brightness-focused'); });
  brightnessAuto.addEventListener('click', function (event) { event.stopPropagation(); brightnessAuto.focus({preventScroll: true}); toggleAutomaticBrightness(); });
  // Activation refreshes the current profile. Moving into this control must not
  // disable an already-confirmed Auto toggle for a redundant hardware read.
  brightnessControl.addEventListener('focus', function () {
    stage.classList.add('brightness-focused');
    if (brightnessValue === null || automaticBrightness === null) requestBrightness();
  });
  uiNode('brightness-icon').addEventListener('mouseenter', function () { stage.classList.add('brightness-focused'); brightnessControl.focus({preventScroll: true}); });
  brightnessControl.addEventListener('click', function (event) {
    if (event.target.closest('[data-ui="brightness-auto-button"]')) return;
    stage.classList.add('brightness-focused'); brightnessControl.focus({preventScroll: true});
    if (event.target.closest('[data-ui="brightness-icon"], [data-ui="brightness-dim-icon"]')) return;
    var track = uiNode('brightness-track').getBoundingClientRect();
    if (event.clientY >= track.top - 14 && event.clientY <= track.bottom + 14 && brightnessValue !== null)
      requestBrightness(Math.round(10 * Math.max(0, Math.min(1, (track.bottom - event.clientY) / track.height))) * 5);
  });
  // One owner for pausing work and resuming Home, including simulated launches.
  function suspendHome() {
    homeSuspended = true; enterDown = false; pendingAutomaticToggle = false;
    clearTimeout(homeIdleTimer); clearTimeout(assetTimer); clearTimeout(reportTimer);
    cancelAnimationFrame(overviewFrame);
    imageQueue.forEach(function (job) { job.button._requested = false; }); imageQueue = [];
  }
  function activateHome(id, mode) {
    if (document.hidden) return false;
    mode = mode || 'home';
    if (launching) {
      // Return events can precede the SDK launch reply. Preserve a real return,
      // but ignore incidental focus events before the external screen opens.
      if (viewMode === 'inactive' || id) pendingActivation = {id: id, mode: mode};
      return false;
    }
    if (!homeSuspended && viewMode === mode && !id) return true;
    var wasSuspended = homeSuspended;
    viewMode = mode; homeSuspended = false;
    stage.classList.remove('home-inactive');
    stage.classList.toggle('playback-sidebar', viewMode === 'overlay');
    uiNode('sidebar-always-open').setAttribute('aria-hidden', 'false');
    if (id) { pendingHomeFocus = id; try { localStorage.setItem('frameFocus', id); } catch (ignore) {} }
    restoreHomeSelection();
    if (wasSuspended) Object.keys(feedModels).forEach(function (app) { feedModels[app].loadedAt = 0; });
    focus(selected); updateClock(); requestBrightness(); refreshCatalog(); resetHomeIdle();
    return true;
  }
  function hideHome() {
    suspendHome(); pendingActivation = null; viewMode = 'inactive';
    stage.classList.add('home-inactive'); stage.classList.remove('playback-sidebar', 'brightness-focused', 'content-focused');
    uiNode('sidebar-always-open').setAttribute('aria-hidden', 'true');
    if (document.activeElement && stage.contains(document.activeElement)) document.activeElement.blur();
    rowsViewport.style.visibility = 'hidden'; showAppBackground(false);
  }
  // Home owns the row/card templates; Samsung supplies their content.
  var rowsViewport = uiNode('content-rows');
  var rowTemplate = uiNode('content-row').cloneNode(true);
  var contentTemplate = uiNode('content-card').cloneNode(true);
  rowsViewport.innerHTML = '';
  rowsViewport.className = 'content-rows';
  var rowsList = document.createElement('div');
  rowsList.className = 'content-row-list'; rowsViewport.appendChild(rowsList);
  rowsViewport.setAttribute('aria-label', 'App content');
  // A screen-sized paint boundary retains scrolling beneath both side controls.
  stage.appendChild(rowsViewport);
  var feedModels = Object.create(null), feedRequests = Object.create(null);
  var contentButtons = [], contentRow = 0, contentColumn = 0;
  var displayedFeed, selectedContentButton = null, brightnessReturnCard = null;
  var feedViews = Object.create(null), displayedView;
  var appBackground = uiNode('app-background');
  appBackground.className = 'app-background';
  // The bundled alpha field; cache its tint instead of repainting CSS gradients.
  var backgroundGlow = uiNode('app-background-glow');
  backgroundGlow.style.backgroundImage = 'none';
  var backgroundMask = new Image(), backgroundPalettes = Object.create(null);
  var lightAccent = '';
  backgroundMask.onload = function () { lightAccent = ''; showPreview(); };
  backgroundMask.src = 'app-gradient-mask.png';
  var fallbackColors = Object.create(null);

  // Sample each already-loaded launcher icon once. Protected images use a quiet default.
  function appAccent(button) {
    var name = button.getAttribute('data-name').toLowerCase().replace(/[^a-z]/g, '');
    var fallback = {geforcenow: '118,185,0', spotify: '29,185,84', hbomax: '132,64,212', max: '82,104,224',
      netflix: '229,9,20', youtube: '255,32,32', plex: '229,160,13'}[name] || '110,128,164';
    var icon = button.querySelector('.app-icon');
    if (!icon || !icon.complete || !icon.naturalWidth) return fallback;
    var source = icon.getAttribute('src');
    if (fallbackColors[source]) return fallbackColors[source];
    try {
      var canvas = document.createElement('canvas'); canvas.width = 32; canvas.height = 24;
      var context = canvas.getContext('2d'); context.drawImage(icon, 0, 0, 32, 24);
      var pixels = context.getImageData(0, 0, 32, 24).data;
      var red = 0, green = 0, blue = 0, weight = 0;
      for (var i = 0; i < pixels.length; i += 4) {
        var saturation = (Math.max(pixels[i], pixels[i + 1], pixels[i + 2]) - Math.min(pixels[i], pixels[i + 1], pixels[i + 2])) / 255;
        if (saturation < .18) continue;
        var sample = saturation * pixels[i + 3] / 255;
        red += pixels[i] * sample; green += pixels[i + 1] * sample; blue += pixels[i + 2] * sample; weight += sample;
      }
      if (weight > 3) {
        // Dark logo backgrounds still need a visible accent; keep their hue intact.
        var lift = Math.max(1, 185 / Math.max(red / weight, green / weight, blue / weight));
        fallback = [red, green, blue].map(function (channel) { return Math.round(channel / weight * lift); }).join(',');
      }
    } catch (ignore) {}
    return fallbackColors[source] = fallback;
  }
  function showAppBackground(visible, soft) {
    appBackground.classList.toggle('visible', visible);
    if (!visible) return;
    appBackground.classList.toggle('soft', !!soft);
    var accent = appAccent(buttons[selected]);
    if (accent === lightAccent) return;
    if (!backgroundMask.complete || !backgroundMask.naturalWidth) return;
    if (!backgroundPalettes[accent]) {
      var canvas = document.createElement('canvas');
      canvas.width = backgroundMask.naturalWidth / 2; canvas.height = backgroundMask.naturalHeight / 2;
      var context = canvas.getContext('2d');
      context.drawImage(backgroundMask, 0, 0, canvas.width, canvas.height);
      context.globalCompositeOperation = 'source-in';
      context.fillStyle = 'rgb(' + accent + ')'; context.fillRect(0, 0, canvas.width, canvas.height);
      backgroundPalettes[accent] = canvas.toDataURL('image/png');
    }
    backgroundGlow.style.backgroundImage = 'url(' + backgroundPalettes[accent] + ')';
    lightAccent = accent;
  }
  var imageQueue = [], imageRequests = 0;
  // Idle Art entry belongs only to full Home, never to an app or its overlay.
  var homeIdleTimer, homeIdleSince;
  var homeIdleDelay = 5 * 60 * 1000;
  function homeIdleEligible() { return !document.hidden && !launching && !homeSuspended && viewMode === 'home'; }
  function resetHomeIdle() {
    clearTimeout(homeIdleTimer);
    homeIdleSince = performance.now();
    if (homeIdleEligible()) homeIdleTimer = setTimeout(enterIdleArt, homeIdleDelay);
  }
  function enterIdleArt() {
    clearTimeout(homeIdleTimer);
    if (!homeIdleEligible()) return;
    var remaining = homeIdleDelay - (performance.now() - homeIdleSince);
    if (remaining > 0) { homeIdleTimer = setTimeout(enterIdleArt, remaining); return; }
    enterDown = false;
    performLaunch('Art Mode', function () { return deviceRequest('enter-art', {}); });
  }
  ['keydown', 'mousedown', 'mousemove', 'touchstart', 'wheel'].forEach(function (type) {
    document.addEventListener(type, resetHomeIdle, true);
  });
  window.addEventListener('pagehide', function () { clearTimeout(homeIdleTimer); });

  // Both SDK launches and helper replies have bounded completion. Late SDK
  // callbacks cannot settle twice or remove a different pending operation.
  function deviceCall(operation, timeoutMs) {
    var request = new Promise(function (resolve, reject) {
      var finished = false;
      var timer = setTimeout(function () { finish(new Error('Device operation timed out')); }, timeoutMs || 12000);
      function finish(error, reply) {
        if (finished) return;
        finished = true; clearTimeout(timer);
        if (error) reject(error); else resolve(reply);
      }
      try { operation().then(function (reply) { finish(null, reply); }, function (error) { finish(error || new Error('Request unavailable')); }); }
      catch (error) { finish(error); }
    });
    devicePending.push(request);
    function settled() { devicePending.splice(devicePending.indexOf(request), 1); }
    request.then(settled, settled);
    return request;
  }
  function deviceRequest(action, values, timeoutMs) {
    if ((homeSuspended && ['samsung', 'open-feed-item', 'enter-art'].indexOf(action) < 0) || document.hidden)
      return Promise.reject(new Error('Home is inactive'));
    return deviceCall(function () {
      return device.request(action, values).then(function (reply) {
        if (!reply) throw new Error('Request unavailable');
        return reply;
      });
    }, timeoutMs);
  }
  function getFeed(app) {
    if (homeSuspended || feedRequests[app]) return;
    var saved = feedModels[app];
    if (saved && Date.now() - saved.loadedAt < 60000) return;
    feedRequests[app] = true;
    var request = deviceRequest('get-feed', {app: app}).then(function (reply) { return JSON.parse(reply.feed); });
    request.then(function (model) {
      if (!model || model.appId !== app || !Array.isArray(model.sections)) throw new Error('No feed');
      model.loadedAt = Date.now(); feedModels[app] = model;
    }).catch(function () {
      // A busy reader or network failure is not a confirmed empty feed.
      feedModels[app] = saved || {appId: app, status: 'unavailable', sections: [], loadedAt: 0};
    }).then(function () {
      delete feedRequests[app];
      if (buttons[selected].getAttribute('data-app') === app) showPreview();
    });
  }
  function reportFeedRender() {
    clearTimeout(reportTimer);
    if (homeSuspended || !displayedFeed || imageRequests || imageQueue.length) return;
    reportTimer = setTimeout(function () {
      if (homeSuspended || document.hidden || imageRequests || imageQueue.length || !displayedFeed) return;
      var images = rowsViewport.querySelectorAll('img');
      var loaded = Array.prototype.filter.call(images, function (image) { return image.complete && image.naturalWidth > 0; }).length;
      deviceRequest('report-feed-render', {app: displayedFeed.appId, cards: String(contentButtons.reduce(function (n, row) { return n + row.length; }, 0)), images: String(loaded)}).catch(function () {});
    }, 300);
  }
  function loadArtwork() {
    clearTimeout(assetTimer);
    artworkReadyAt = performance.now() + 160;
    // Input and the existing view update first. Decode only after selection settles.
    assetTimer = setTimeout(function () {
      if (homeSuspended || document.hidden || viewMode !== 'home') return;
      artworkReadyAt = 0;
      var app = buttons[selected].getAttribute('data-app');
      if (defaultPins.slice(0, 3).indexOf(app) >= 0) getFeed(app);
      contentButtons.forEach(function (row, index) {
        if (Math.abs(index - contentRow) > 1) return;
        var left = index === contentRow ? contentColumn : 0;
        row.slice(Math.max(0, left - 1), left + 5).forEach(function (button) {
          if (button._requested || !button._card.hasArtwork) return;
          button._requested = true;
          imageQueue.push({button: button, view: displayedView});
        });
      });
      pumpArtwork();
    }, 160);
  }
  function pumpArtwork() {
    if (homeSuspended || document.hidden || performance.now() < artworkReadyAt || viewMode !== 'home') return;
    while (imageRequests < 2 && imageQueue.length) {
      var item = imageQueue.shift();
      if (item.view !== displayedView) { item.button._requested = false; continue; }
      imageRequests++;
      (function (job) {
        var finished = false;
        function finish() {
          if (finished) return;
          finished = true; imageRequests--; pumpArtwork(); reportFeedRender();
        }
        var card = job.button._card;
        var request = deviceRequest('get-feed-image', {item: card.id}).then(function (reply) { return reply.image; });
        request.then(function (source) {
          function paint() {
            if (!source || homeSuspended || document.hidden || job.view !== displayedView || viewMode !== 'home') {
              job.button._requested = false; finish(); return;
            }
            var remaining = artworkReadyAt - performance.now();
            if (remaining > 0) { setTimeout(paint, remaining); return; }
            var image = document.createElement('img');
            image.alt = ''; image.className = 'content-thumbnail';
            image.onload = finish;
            image.onerror = function () { image.remove(); job.button._requested = false; finish(); };
            image.src = source;
            job.button.querySelector('[data-ui="content-artwork"]').appendChild(image);
          }
          paint();
        }).catch(function () { job.button._requested = false; finish(); });
      }(item));
    }
  }
  function renderFeed(model) {
    if (displayedFeed === model) return;
    var restoreFocus = stage.classList.contains('content-focused') && !stage.classList.contains('brightness-focused');
    var previousCard = restoreFocus && contentButtons[contentRow][contentColumn]._card.id;
    imageQueue.forEach(function (job) { job.button._requested = false; }); imageQueue = [];
    if (rowsList.parentNode) rowsList.parentNode.removeChild(rowsList);
    displayedFeed = model;
    displayedView = model && feedViews[model.appId];
    var signature = model && JSON.stringify(model.sections.map(function (section) { return [section.title, section.cards]; }));
    if (!displayedView || displayedView.signature !== signature) {
      displayedView = {model: model, signature: signature, list: document.createElement('div'), buttons: []};
      displayedView.list.className = 'content-row-list';
      if (model) feedViews[model.appId] = displayedView;
    }
    displayedView.model = model;
    rowsList = displayedView.list; contentButtons = displayedView.buttons;
    rowsViewport.appendChild(rowsList);
    rowsList.style.transform = 'translateY(0)'; contentRow = 0; contentColumn = 0;
    var sections = model ? model.sections : [];
    var firstRender = !contentButtons.length;
    if (firstRender) sections.forEach(function (section, rowIndex) {
      if (!section.cards || !section.cards.length) return;
      var row = rowTemplate.cloneNode(true);
      row.className = 'content-row';
      row.querySelector('[data-ui="content-row-heading"]').textContent = section.title || 'From ' + model.name;
      var strip = row.querySelector('[data-ui="content-cards"]');
      strip.className = 'content-cards'; strip.innerHTML = '';
      var cards = [];
      section.cards.forEach(function (card, column) {
        var button = document.createElement('button');
        button.type = 'button'; button.className = 'content-card'; button.tabIndex = -1;
        button.setAttribute('aria-label', card.title); button._card = card;
        var content = contentTemplate.cloneNode(true);
        content.querySelector('[data-ui="content-title"]').textContent = card.title;
        content.querySelector('[data-ui="content-subtitle"]').textContent = card.subtitle || '';
        button.appendChild(content);
        button.addEventListener('focus', function () { focusContent(rowIndex, column, false); });
        button.addEventListener('click', function () { focusContent(rowIndex, column); openContent(); });
        strip.appendChild(button); cards.push(button);
      });
      rowsList.appendChild(row); contentButtons.push(cards);
    });
    if (restoreFocus && contentButtons.length) {
      var position = [0, 0];
      contentButtons.forEach(function (cards, r) { cards.forEach(function (card, c) { if (card._card.id === previousCard) position = [r, c]; }); });
      focusContent(position[0], position[1]);
    } else if (restoreFocus) focus(selected);
    loadArtwork();
    // Cached row selection must not wake the native reader just to repeat a count.
    if (firstRender) reportFeedRender();
  }
  function focusContent(row, column, moveFocus) {
    if (!contentButtons.length) return;
    var nextRow = Math.max(0, Math.min(contentButtons.length - 1, row));
    var nextColumn = Math.max(0, Math.min(contentButtons[nextRow].length - 1, column));
    var button = contentButtons[nextRow][nextColumn];
    // The focus listener owns selection; do not update again after focus().
    if (moveFocus !== false && document.activeElement !== button) { button.focus({preventScroll: true}); return; }
    if (selectedContentButton) selectedContentButton.classList.remove('selected');
    button.classList.add('selected'); selectedContentButton = button;
    contentRow = nextRow; contentColumn = nextColumn;
    var entering = !stage.classList.contains('content-focused');
    stage.classList.remove('brightness-focused'); stage.classList.add('content-focused');
    if (entering) layoutNavigation();
    // Shared geometry avoids forced layout reads after changing selected styles.
    var width = uiLayout.contentViewportWidth, height = uiLayout.contentViewportHeight;
    var stride = uiLayout.contentCardWidth + uiLayout.contentCardGap;
    var totalWidth = contentButtons[contentRow].length * stride - uiLayout.contentCardGap;
    var offset = Math.max(0, Math.min(totalWidth - width, contentColumn * stride + uiLayout.contentCardWidth / 2 - width / 2));
    button.parentNode.style.transform = 'translateX(' + (-offset) + 'px)';
    var rowStride = uiLayout.contentRowHeight + uiLayout.contentRowGap;
    var totalHeight = contentButtons.length * rowStride - uiLayout.contentRowGap;
    var top = Math.max(0, Math.min(totalHeight - height, contentRow * rowStride - 8));
    rowsList.style.transform = 'translateY(' + (-top) + 'px)';
    loadArtwork();
    message('Enter opens selected title');
  }
  function returnFromBrightness() {
    var card = brightnessReturnCard; brightnessReturnCard = null;
    stage.classList.remove('brightness-focused');
    if (card && contentButtons.length) focusContent(card[0], card[1]); else focus(selected);
  }
  function openContent() {
    var card = contentButtons[contentRow][contentColumn]._card;
    performLaunch('selected title', function () { return deviceRequest('open-feed-item', {item: card.id}); });
  }

  // The same files preview on a computer and launch installed apps on the TV.
  function message(text) { status.textContent = text; }
  function select(index) {
    // Returning to the sidebar restores the app overview instead of leaving
    // an earlier content row scrolled across its heading.
    if (selectedContentButton) {
      rowsList.style.transform = 'translateY(0)';
      selectedContentButton.classList.remove('selected'); selectedContentButton = null;
      Array.prototype.forEach.call(rowsList.children, function (row) { row.querySelector('.content-cards').style.transform = 'translateX(0)'; });
      contentRow = 0; contentColumn = 0;
    }
    stage.classList.remove('brightness-focused', 'content-focused');
    brightnessReturnCard = null;
    selected = index;
    loadArtwork();
    message('');
    buttons.forEach(function (button, i) { button.classList.toggle('selected', i === index); });
    schedulePreview();
  }
  function focus(index) {
    var button = buttons[index];
    if (document.activeElement === button) select(index);
    else button.focus({preventScroll: true});
  }

  // Use the TV’s local clock; one update per minute leaves navigation untouched.
  function updateClock() {
    clearTimeout(clockTimer);
    if (document.hidden) return;
    var now = new Date();
    var minutes = now.getMinutes();
    document.getElementById('clock').textContent = now.getHours() + ':' + (minutes < 10 ? '0' : '') + minutes;
    clockTimer = setTimeout(updateClock, 60000 - (now.getSeconds() * 1000 + now.getMilliseconds()));
  }

  // Coalesce overview changes into the next frame, outside the arrow handler.
  function showPreview() {
    if (document.hidden || homeSuspended) return;
    var home = viewMode === 'home';
    uiNode('selected-app-preview').classList.toggle('empty', !home || buttons[selected].classList.contains('utility'));
    if (!home) { rowsViewport.style.visibility = 'hidden'; showAppBackground(false); return; }
    var app = buttons[selected].getAttribute('data-app');
    document.getElementById('preview-title').textContent = buttons[selected].getAttribute('data-name');
    document.getElementById('preview-caption').textContent = '';
    if (buttons[selected].classList.contains('utility')) { renderFeed(null); rowsViewport.style.visibility = 'hidden'; showAppBackground(false); return; }
    var model = feedModels[app];
    renderFeed(model || null);
    var supported = defaultPins.slice(0, 3).indexOf(app) >= 0;
    var fallback = (!model || !model.sections.length) && (!supported || !!model);
    rowsViewport.style.visibility = fallback ? 'hidden' : '';
    document.getElementById('preview-title').style.visibility = fallback ? 'hidden' : '';
    showAppBackground(true, !!model && model.sections.length > 0);
    document.getElementById('preview-description').textContent = supported && !model ? 'Loading…' : '';
  }
  function schedulePreview() {
    cancelAnimationFrame(overviewFrame);
    overviewFrame = requestAnimationFrame(function () {
      showPreview();
      layoutNavigation();
    });
  }
  function layoutNavigation() {
    var appCount = primary.children.length;
    var activeApp = selected < appCount && !stage.classList.contains('brightness-focused') && !stage.classList.contains('content-focused');
    var focusedHeight = uiLayout.tileHeight * uiLayout.appFocusScale;
    var growth = activeApp ? focusedHeight - uiLayout.tileHeight : 0;
    // Slots stay at rest size. Only their painted positions and scales animate.
    Array.prototype.forEach.call(primary.children, function (button, index) {
      var shift = activeApp && index >= selected ? (index === selected ? growth / 2 : growth) : 0;
      button.style.transform = 'translateY(' + shift + 'px)';
    });
    uiNode('tv-controls').style.transform = 'translateY(' + growth + 'px)';
    var primaryHeight = appCount * uiLayout.tileHeight + Math.max(0, appCount - 1) * uiLayout.tileGap + (activeApp ? focusedHeight - uiLayout.tileHeight : 0);
    var controlsTop = uiLayout.verticalInset + primaryHeight + (appCount ? uiLayout.controlsGap : 0);
    var total = controlsTop + 4 * uiLayout.controlHeight + 3 * uiLayout.controlGap + uiLayout.verticalInset;
    var centre = selected < appCount ? uiLayout.verticalInset + selected * (uiLayout.tileHeight + uiLayout.tileGap) + (activeApp ? focusedHeight : uiLayout.tileHeight) / 2 :
      controlsTop + (selected - appCount) * (uiLayout.controlHeight + uiLayout.controlGap) + uiLayout.controlHeight / 2;
    var offset = Math.max(0, Math.min(Math.max(0, total - uiLayout.viewportHeight), centre - uiLayout.viewportHeight / 2));
    uiNode('pinned-navigation-list').style.transform = 'translateY(' + (-offset) + 'px)';
  }
  // Both tile and title launches stop new artwork work, drain in-flight replies,
  // then await device confirmation. A failed launch restores the same Home UI.
  function performLaunch(name, operation) {
    if (launching || document.hidden || viewMode === 'inactive') return;
    try { localStorage.setItem('frameFocus', buttons[selected].getAttribute('data-app') || buttons[selected].getAttribute('data-name')); } catch (ignore) {}
    launching = true; stage.setAttribute('aria-busy', 'true'); suspendHome(); message('Opening ' + name + '…');
    function finish(error) {
      launching = false; stage.setAttribute('aria-busy', 'false');
      var activation = pendingActivation; pendingActivation = null;
      if (activation) activateHome(activation.id, activation.mode);
      else if (error && viewMode !== 'inactive' && !document.hidden) activateHome(undefined, viewMode);
      // A failure after the TV has hidden Home must not reveal it over an app.
      message(error ? 'Could not open ' + name + ': ' + (error.message || error.name || 'unknown error') : '');
    }
    Promise.all(devicePending.map(function (request) { return request.catch(function () {}); }))
      .then(operation).then(function (reply) {
        if (reply && reply.error) throw new Error(reply.error);
        finish();
      }).catch(finish);
  }
  function launch(app, name) {
    performLaunch(name, function () {
      // Samsung confirms that its partial window is active before acknowledging.
      return app === 'com.samsung.tv.csfs' ? deviceRequest('samsung', {}, 8000) : deviceCall(function () { return device.launch(app, name); });
    });
  }

  function launchSelected() {
    var button = buttons[selected];
    launch(button.getAttribute('data-app'), button.getAttribute('data-name'));
  }

  // Shared focus navigation; the device boundary translates remote input.
  // Home owns the channel rocker, including list/previous and release events.
  // Samsung UI and other apps retain their own handlers when they have focus.
  function consumeChannelKey(event) {
    if (!Object.keys(channelKeys).some(function (name) { return channelKeys[name] === event.keyCode; })) return false;
    event.preventDefault(); event.stopPropagation(); return true;
  }
  document.addEventListener('keydown', function (event) {
    var key = event.keyCode;
    var target = event.target;
    if (target.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(target.tagName)) return;
    if (launching) {
      if (consumeChannelKey(event) || key >= 37 && key <= 40 || key === 13 || key === 10009 || key === 27) event.preventDefault();
      return;
    }
    key = device.key(event);
    if (key === null) return;
    if (consumeChannelKey(event)) {
      if (viewMode !== 'inactive' && (key === channelKeys.up || key === channelKeys.down)) adjustBrightness(key === channelKeys.up ? 1 : -1);
      return;
    }
    if (viewMode === 'inactive') return;
    if (stage.classList.contains('content-focused') && !stage.classList.contains('brightness-focused')) {
      if (key >= 37 && key <= 40 || key === 13 || key === 10009 || key === 27) event.preventDefault();
      if (key === 37) { if (contentColumn === 0) focus(selected); else focusContent(contentRow, contentColumn - 1); }
      else if (key === 39) {
        if (contentColumn < contentButtons[contentRow].length - 1) focusContent(contentRow, contentColumn + 1);
        else { brightnessReturnCard = [contentRow, contentColumn]; stage.classList.add('brightness-focused'); brightnessControl.focus({preventScroll: true}); }
      }
      else if (key === 38 || key === 40) focusContent(contentRow + (key === 38 ? -1 : 1), contentColumn);
      else if (key === 10009 || key === 27) focus(selected);
      else if (key === 13 && !event.repeat) openContent();
      return;
    }
    if (key === 39) {
      event.preventDefault();
      if (!stage.classList.contains('brightness-focused') && !buttons[selected].classList.contains('utility') && viewMode === 'home' && contentButtons.length) { focusContent(0, 0); return; }
      stage.classList.add('brightness-focused');
      brightnessControl.focus({preventScroll: true});
      return;
    }
    if (document.activeElement === brightnessAuto) {
      if (key === 37 || key === 10009 || key === 27) { event.preventDefault(); returnFromBrightness(); }
      else if (key === 13) { event.preventDefault(); if (!event.repeat) toggleAutomaticBrightness(); }
      return;
    }
    if (document.activeElement === brightnessControl) {
      if (key === 37) { event.preventDefault(); brightnessAuto.focus({preventScroll: true}); }
      else if (key === 38 || key === 40) {
        event.preventDefault();
        adjustBrightness(key === 38 ? 1 : -1);
      }
      else if (key === 10009 || key === 27) { event.preventDefault(); returnFromBrightness(); }
      return;
    }
    if (key === 13) {
      event.preventDefault();
      if (!event.repeat) enterDown = true;
      return;
    }
    if (key === 10009 || key === 27) { event.preventDefault(); device.back(); return; }
    if (key < 37 || key > 40) return;
    event.preventDefault();
    if (key === 38) focus(Math.max(0, selected - 1));
    if (key === 40) focus(Math.min(buttons.length - 1, selected + 1));
  });
  document.addEventListener('keyup', function (event) {
    if (consumeChannelKey(event)) return;
    if (event.keyCode !== 13 || !enterDown) return;
    event.preventDefault(); enterDown = false; launchSelected();
  });
  function bindButton(button) {
    button.addEventListener('focus', function () { select(buttons.indexOf(button)); });
    button.addEventListener('mousemove', function () { if (buttons[selected] !== button) focus(buttons.indexOf(button)); });
    button.addEventListener('click', function () { focus(buttons.indexOf(button)); launchSelected(); });
  }
  buttons.forEach(bindButton);
  window.addEventListener('blur', function () { enterDown = false; });
  document.addEventListener('visibilitychange', function () {
    cancelAnimationFrame(overviewFrame);
    enterDown = false;
    updateClock();
    resetHomeIdle();
  });

  // Installed apps supply names and original icon paths; shared layout supplies tile geometry.
  function reconcileApps(information) {
    var current = buttons[selected], active = document.activeElement;
    var known = bindings.slice(0, 4).map(function (binding) { return binding[2]; });
    var excluded = bindings.slice(4).map(function (binding) { return binding[2]; }).concat(['Fr4meHome1.Launcher', 'org.framehome.overlayprobe', 'org.framehome.overlayprobe.startup']);
    var seen = Object.create(null);
    var apps = information.filter(function (app) {
      if (!app || typeof app.id !== 'string' || typeof app.name !== 'string' || seen[app.id] || excluded.indexOf(app.id) !== -1) return false;
      seen[app.id] = true;
      // Samsung also marks internal native helpers as visible. Store widgets on
      // this TV use a ten-character package ID; keep the known native Netflix app.
      return savedOrder.indexOf(app.id) !== -1 && (known.indexOf(app.id) !== -1 || app.show === true);
    });
    var order = savedOrder;
    apps.sort(function (a, b) {
      var ai = order.indexOf(a.id), bi = order.indexOf(b.id);
      if (ai < 0) ai = order.length;
      if (bi < 0) bi = order.length;
      return ai - bi || a.name.localeCompare(b.name);
    });
    // Unchanged catalogue replies must not detach tiles or steal card/slider focus.
    var signature = JSON.stringify(apps.map(function (app) { return [app.id, app.name, app.iconPath || '']; }));
    if (primary.getAttribute('data-catalog') === signature) return;
    primary.setAttribute('data-catalog', signature);
    var existing = Array.prototype.slice.call(primary.children);
    var rendered = apps.map(function (app, index) {
      var button = existing.filter(function (item) { return item.getAttribute('data-app') === app.id; })[0];
      if (!button) {
        button = tileTemplate.cloneNode(true);
        button.removeAttribute('data-ui');
        button.className = 'tile';
        // A generic tile reuses the shared surface, without copying Netflix branding.
        var card = button.querySelector('.card');
        while (card.children.length > 1) card.removeChild(card.lastElementChild);
        card.querySelectorAll('image').forEach(function (image) { image.remove(); });
        var fallback = document.createElement('span');
        fallback.className = 'app-name-fallback';
        card.appendChild(fallback);
        // Cloned SVG mask references must stay unique within the document.
        card.querySelectorAll('svg').forEach(function (svg) {
          svg.querySelectorAll('[id]').forEach(function (element) {
            var oldId = element.id, newId = oldId + '-app-' + encodeURIComponent(app.id).replace(/%/g, '-');
            svg.innerHTML = svg.innerHTML.split(oldId).join(newId);
          });
        });
        bindButton(button);
      }
      button.setAttribute('data-app', app.id);
      button.setAttribute('data-name', app.name);
      button.setAttribute('aria-label', app.name);
      var fallback = button.querySelector('.app-name-fallback');
      if (fallback) fallback.textContent = app.name;
      var card = button.querySelector('.card');
      var oldIcon = card.querySelector('.app-icon');
      // Use the actual installed Samsung launcher artwork, not generic package marks.
      var source = typeof app.iconPath === 'string' && app.iconPath ? app.iconPath : '';
      if (source.charAt(0) === '/') source = 'file://' + source;
      if (!source && oldIcon) {
        oldIcon.remove();
        Array.prototype.forEach.call(card.children, function (child) { child.style.visibility = ''; });
      }
      if (source && (!oldIcon || oldIcon.getAttribute('src') !== source)) {
        if (oldIcon) oldIcon.remove();
        Array.prototype.forEach.call(card.children, function (child) { child.style.visibility = ''; });
        var image = document.createElement('img');
        image.className = 'app-icon';
        image.alt = '';
        image.onload = function () {
          Array.prototype.forEach.call(card.children, function (child) { if (child !== image) child.style.visibility = 'hidden'; });
          if (button === buttons[selected]) schedulePreview();
        };
        image.onerror = function () { image.remove(); };
        image.src = source;
        card.appendChild(image);
      }
      return button;
    });
    existing.forEach(function (button) { primary.removeChild(button); });
    rendered.forEach(function (button) { primary.appendChild(button); });
    try { localStorage.setItem('framePinnedApps', JSON.stringify(savedOrder)); } catch (ignore) {}
    buttons = Array.prototype.slice.call(document.querySelectorAll('.tile'));
    selected = Math.max(0, buttons.indexOf(current));
    restoreHomeSelection();
    if (!homeSuspended && viewMode !== 'inactive') {
      if (buttons[selected] === current && active && !active.classList.contains('tile')) schedulePreview();
      else focus(selected);
    }
  }
  function refreshCatalog() {
    if (catalogBusy || homeSuspended) return;
    catalogBusy = true;
    deviceRequest('get-order', {}, 8000).then(function (reply) {
      if (!reply.order || reply.order.length > 32768) throw new Error('Invalid order reply');
      var order = JSON.parse(reply.order), catalog = JSON.parse(reply.catalog);
      if (!Array.isArray(catalog) || catalog.length > 512 || !Array.isArray(order) || order.length > 512 ||
          order.some(function (id) { return typeof id !== 'string' || !id || id.length > 128; })) throw new Error('Invalid catalogue');
      savedOrder = order;
      reconcileApps(catalog);
    }).catch(function () { message('Could not refresh apps.'); }).then(function () { catalogBusy = false; });
  }
  // Only our native sidebar may hand its cursor to Home. IDs survive list changes.
  function restoreHomeSelection() {
    if (!pendingHomeFocus) return;
    var index = buttons.map(function (button) { return button.getAttribute('data-app') || button.getAttribute('data-name'); }).indexOf(pendingHomeFocus);
    if (index >= 0) { selected = index; pendingHomeFocus = null; }
  }
  try {
    var lastFocus = localStorage.getItem('frameFocus');
    var savedFocus = buttons.map(function (button) { return button.getAttribute('data-app') || button.getAttribute('data-name'); }).indexOf(lastFocus);
    if (savedFocus >= 0) selected = savedFocus;
  } catch (ignore) {}
  device.start({activate: activateHome, suspend: hideHome, catalogueChanged: refreshCatalog});
}());
