/* Development device responses and external-app stand-ins; never packaged for TV. */
(function () {
  'use strict';
  var options = new URLSearchParams(location.search);
  var delay = Number(options.get('delay') || 40);
  if (!Number.isFinite(delay) || delay < 0 || delay > 30000) delay = 40;
  var failure = options.get('fail');
  var feeds = fetch('preview-feeds.json', {cache: 'no-store'}).then(function (reply) {
    return reply.ok ? reply.json() : {};
  }).catch(function () { return {}; });
  var apps = [
    {id: 'org.tizen.netflix-app', name: 'Netflix', iconPath: 'preview-icon-netflix.png', show: true},
    {id: '9Ur5IzDKqV.TizenYouTube', name: 'YouTube', iconPath: 'preview-icon-youtube.png', show: true},
    {id: 'kIciSQlYEM.plex', name: 'Plex', iconPath: 'preview-icon-plex.png', show: true},
    {id: 'w0wZu3xkQq.GeForceNOW', name: 'GeForce NOW', iconPath: 'preview-icon-geforce.png', show: true},
    {id: 'preview-spotify', name: 'Spotify', iconPath: 'preview-icon-spotify.png', show: true},
    {id: 'preview-hbo', name: 'HBO Max', iconPath: 'preview-icon-hbo.png', show: true}
  ];
  var installed = [];
  try {
    var savedApps = JSON.parse(localStorage.getItem('framePreviewApps') || '[]');
    if (Array.isArray(savedApps)) savedApps.forEach(function (app) {
      if (app && typeof app.id === 'string' && typeof app.name === 'string' && !apps.some(function (other) { return other.id === app.id; })) {
        installed.push(app); apps.push(app);
      }
    });
  } catch (ignore) {}
  var order = [];
  try { order = JSON.parse(localStorage.getItem('framePinnedApps') || '[]'); } catch (ignore) {}
  if (!Array.isArray(order) || !order.every(function (id) { return typeof id === 'string'; })) order = [];
  apps.forEach(function (app) { if (order.indexOf(app.id) < 0) order.push(app.id); });
  // A fixed demo room chooses 50%; this is not a Samsung brightness default.
  var automaticLevel = 25;
  var level = 35, automatic = false;
  var home, screen, mode = 'home', playingApp, externalKind = 'app';

  // Replies are asynchronous, including failures. Shared Home validates results,
  // tracks busy/pending work and performs the launch drain before invoking these.
  function reply(action, operation) {
    return new Promise(function (resolve, reject) {
      setTimeout(function () {
        if (failure === action) { reject(new Error('Simulated ' + action + ' failure')); return; }
        try { resolve(operation()); } catch (error) { reject(error); }
      }, delay);
    });
  }
  function activate(nextMode, id) {
    if (!home.activate(id, nextMode)) return;
    mode = nextMode; screen.hidden = mode === 'home';
  }
  function showApp(app, title, kind) {
    playingApp = app; externalKind = kind; mode = 'inactive';
    screen.classList.toggle('simulated-youtube', app === '9Ur5IzDKqV.TizenYouTube');
    screen.querySelector('h1').textContent = title;
    screen.querySelector('.simulated-hint').textContent = 'Simulated app · H: Home sidebar';
    screen.querySelector('form').hidden = app !== 'com.samsung.tv.store';
    screen.hidden = false; home.suspend();
  }
  function findCard(models, id) {
    var match;
    Object.keys(models).some(function (app) {
      return models[app].sections.some(function (section) {
        var card = section.cards.find(function (item) { return item.id === id; });
        if (card) { match = {app: app, card: card}; return true; }
        return false;
      });
    });
    if (!match) throw new Error('Title unavailable');
    return match;
  }
  function back() {
    if (mode === 'overlay') { mode = 'inactive'; home.suspend(); }
    else if (mode === 'inactive' && externalKind === 'utility') activate('home');
  }
  window.frameDevice = {
    screenInset: 24,
    channelKeys: {up: 33, down: 34},
    key: function (event) {
      var key = event.keyCode;
      if (event.ctrlKey || event.metaKey || event.altKey) return key;
      if (key === 72 || (key === 82 && mode === 'overlay')) {
        event.preventDefault();
        if (!event.repeat) {
          if (key === 82) back();
          else activate(mode === 'inactive' && externalKind === 'app' ? 'overlay' : 'home', mode === 'inactive' && externalKind === 'app' ? playingApp : undefined);
        }
        return null;
      }
      if (key === 8 || key === 46 || key === 27) {
        if (mode === 'inactive') { event.preventDefault(); if (!event.repeat) back(); return null; }
        return 10009;
      }
      return key;
    },
    back: back,
    request: function (action, values) {
      return reply(action, function () {
        if (action === 'get-order') return {order: JSON.stringify(order), catalog: JSON.stringify(apps)};
        if (action === 'get-feed') return feeds.then(function (models) { return {feed: JSON.stringify(models[values.app] || null)}; });
        if (action === 'get-feed-image' || action === 'open-feed-item') return feeds.then(function (models) {
          var item = findCard(models, values.item);
          if (action === 'get-feed-image') return {image: item.card.artwork};
          showApp(item.app, item.card.title, 'app'); return {};
        });
        if (action === 'report-feed-render') return {};
        if (action === 'get-brightness' || action === 'set-brightness' || action === 'set-auto-brightness') {
          if (action === 'set-brightness') {
            var target = Number(values.value);
            if (!Number.isInteger(target) || target < 0 || target > 50 || Number(values.expected) !== level) throw new Error('Brightness state changed');
            level = target; automatic = false;
          } else if (action === 'set-auto-brightness') {
            automatic = values.automatic === '1';
            if (automatic) level = automaticLevel;
          }
          return {brightness: String(level), automatic: automatic ? '1' : '0'};
        }
        if (action === 'samsung') { showApp('com.samsung.tv.csfs', 'Samsung UI', 'utility'); return {}; }
        if (action === 'enter-art') { showApp('org.tizen.art-app', 'Art Mode', 'art'); return {}; }
        throw new Error('Unsupported simulated device operation: ' + action);
      });
    },
    launch: function (app, name) {
      return reply('launch', function () {
        var utility = app === 'org.tizen.art-app' || app === 'com.samsung.tv.store' || app === 'org.tizen.menu';
        showApp(app, name, utility ? 'utility' : 'app');
      });
    },
    start: function (callbacks) {
      home = callbacks;
      document.body.classList.add('local-preview');
      screen = document.createElement('section');
      screen.className = 'simulated-screen'; screen.hidden = true;
      screen.setAttribute('aria-label', 'Simulated TV app');
      screen.innerHTML = '<h1></h1><p class="simulated-hint"></p>' +
        '<form class="simulated-install" hidden><p>Add an app to this preview</p><label>Name<input name="appName" required maxlength="80"></label>' +
        '<label>Original app icon<input name="appIcon" type="file" accept="image/png,image/jpeg,image/webp,image/svg+xml" required></label>' +
        '<button type="submit">Add to sidebar</button></form>';
      document.getElementById('frame-home').appendChild(screen);
      screen.querySelector('form').addEventListener('submit', function (event) {
        event.preventDefault();
        var form = event.target, file = form.elements.appIcon.files[0], name = form.elements.appName.value.trim();
        if (!name || !file) return;
        if (file.size > 2 * 1024 * 1024) { form.querySelector('p').textContent = 'Choose an icon under 2 MB.'; return; }
        var reader = new FileReader();
        reader.onload = function () {
          var app = {id: 'preview-' + Date.now(), name: name, iconPath: reader.result, show: true};
          try { localStorage.setItem('framePreviewApps', JSON.stringify(installed.concat([app]))); }
          catch (error) { form.querySelector('p').textContent = 'Preview storage is full.'; return; }
          installed.push(app); apps.push(app); order.push(app.id);
          activate('home', app.id); form.reset();
        };
        reader.onerror = function () { form.querySelector('p').textContent = 'Could not read that icon.'; };
        reader.readAsDataURL(file);
      });
      // Simulate panel backlight across the TV frame, including optimistic updates.
      var track = document.querySelector('[data-ui="brightness-track"]');
      new MutationObserver(function () {
        var value = Number(track.getAttribute('aria-valuenow')) / 100;
        if (!track.hasAttribute('aria-valuenow')) return;
        var filter = 'brightness(' + (0.15 + .85 * value) + ')';
        document.getElementById('frame-home').style.filter = filter;
      }).observe(track, {attributes: true, attributeFilter: ['aria-valuenow']});
      window.addEventListener('focus', function () { if (mode !== 'inactive') home.activate(undefined, mode); });
      document.addEventListener('visibilitychange', function () {
        if (document.hidden) home.suspend(); else if (mode !== 'inactive') home.activate(undefined, mode);
      });
      activate('home');
      // Full-page reload uses the same builder; no hot-module runtime enters TV.
      var revision;
      function checkBuild() {
        fetch('/__dev_version', {cache: 'no-store'}).then(function (response) { return response.ok ? response.json() : null; }).then(function (next) {
          if (!next) return;
          if (revision && revision !== next.revision) location.reload();
          revision = next.revision;
        }).catch(function () {});
      }
      checkBuild(); setInterval(checkBuild, 1000);
    }
  };
}());
