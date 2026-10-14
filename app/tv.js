/* Samsung device operations. Home owns request timing and UI lifecycle. */
(function () {
  'use strict';
  var channels = {};
  function requestedFocus() {
    var request = tizen.application.getCurrentApplication().getRequestedAppControl();
    if (!request || request.callerAppId !== 'org.framehome.overlayprobe') return;
    var data = request.appControl.data.filter(function (item) { return item.key === 'frameFocus'; })[0];
    if (data && data.value.length === 1 && typeof data.value[0] === 'string' && data.value[0].length <= 128) return data.value[0];
  }
  window.frameDevice = {
    screenInset: 0,
    channelKeys: channels,
    key: function (event) { return event.keyCode; },
    back: function () {},
    request: function (action, values) {
      return new Promise(function (resolve, reject) {
        var data = [new tizen.ApplicationControlData('action', [action])];
        Object.keys(values).forEach(function (key) { data.push(new tizen.ApplicationControlData(key, [values[key]])); });
        tizen.application.launchAppControl(new tizen.ApplicationControl(
          'http://tizen.org/appcontrol/operation/default', null, null, null, data
        ), 'org.framehome.overlayprobe', function () {}, reject, {
          onsuccess: function (items) {
            var result = {};
            items.forEach(function (item) { if (item.value.length) result[item.key] = item.value[0]; });
            resolve(result);
          }, onfailure: reject
        });
      });
    },
    launch: function (app) {
      if (!app) return Promise.reject(new Error('App is unavailable.'));
      return new Promise(function (resolve, reject) {
        // Art's default request opens its browser; fullscreen is reserved for idle/Power.
        if (app === 'com.samsung.tv.store' || app === 'org.tizen.menu' || app === 'org.tizen.art-app') {
          var data = app === 'com.samsung.tv.store'
            ? [new tizen.ApplicationControlData('Sub_Menu', ['main'])]
            : app === 'org.tizen.menu' ? [new tizen.ApplicationControlData('uri', ['/opt/apps/org.tizen.menu/res/json/ATSC/menu.json'])] : [];
          tizen.application.launchAppControl(new tizen.ApplicationControl(
            'http://tizen.org/appcontrol/operation/default', null, null, null, data
          ), app, resolve, reject);
        } else tizen.application.launch(app, resolve, reject);
      });
    },
    start: function (home) {
      function activate() { if (!document.hidden) home.activate(); }
      window.addEventListener('appcontrol', function () {
        var id;
        try { id = requestedFocus(); } catch (ignore) {}
        home.activate(id);
      });
      window.addEventListener('focus', activate);
      document.addEventListener('visibilitychange', function () {
        if (document.hidden) home.suspend(); else if (document.hasFocus()) activate();
      });
      if (tizen.package) {
        try { tizen.package.setPackageInfoEventListener({oninstalled: home.catalogueChanged, onupdated: home.catalogueChanged, onuninstalled: home.catalogueChanged}); } catch (ignore) {}
        window.addEventListener('pagehide', function () { try { tizen.package.unsetPackageInfoEventListener(); } catch (ignore) {} });
      }
      if (tizen.tvinputdevice) {
        [['up', 'ChannelUp'], ['down', 'ChannelDown'], ['guide', 'Guide'], ['list', 'ChannelList'], ['previous', 'PreviousChannel']].forEach(function (binding) {
          try { var key = tizen.tvinputdevice.getKey(binding[1]); if (key) { tizen.tvinputdevice.registerKey(binding[1]); channels[binding[0]] = key.code; } } catch (ignore) {}
        });
      }
      var id;
      try { id = requestedFocus(); } catch (ignore) {}
      home.activate(id);
    }
  };
}());
