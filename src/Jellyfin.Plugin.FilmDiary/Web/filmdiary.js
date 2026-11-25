/* Film Diary — interfaz web para Jellyfin.
 * Añade a la ficha de cada película: estrellas de 0 a 5 (con medias), "me gusta",
 * fecha de visionado, "volver a ver" y notas. Todo se guarda por usuario en el servidor.
 */
(function () {
  'use strict';
  if (window.__filmDiaryLoaded) return;
  window.__filmDiaryLoaded = true;

  var STYLE = [
    '.fd-panel{margin:1.2em 0 1.4em;padding:1em 1.1em;border-radius:10px;background:rgba(255,255,255,.05);border:1px solid rgba(255,255,255,.09);max-width:46em}',
    '.fd-row{display:flex;flex-wrap:wrap;align-items:center;gap:.8em 1.4em}',
    '.fd-stars{display:inline-flex;gap:.12em;cursor:pointer;user-select:none;touch-action:manipulation}',
    '.fd-star{position:relative;width:1.9em;height:1.9em}',
    '.fd-star svg{position:absolute;inset:0;width:100%;height:100%}',
    '.fd-star .fd-fill{clip-path:inset(0 100% 0 0)}',
    '.fd-star.half .fd-fill{clip-path:inset(0 50% 0 0)}',
    '.fd-star.full .fd-fill{clip-path:inset(0 0 0 0)}',
    '.fd-stars.preview .fd-fill{opacity:.65}',
    '.fd-value{min-width:6.2em;font-variant-numeric:tabular-nums;opacity:.85}',
    '.fd-like{background:none;border:0;cursor:pointer;padding:.2em;line-height:0;color:inherit}',
    '.fd-like svg{width:1.7em;height:1.7em}',
    '.fd-like.on path{fill:#ff6b81;stroke:#ff6b81}',
    '.fd-field{display:flex;align-items:center;gap:.5em;font-size:.95em}',
    '.fd-field input[type=date]{background:rgba(0,0,0,.25);color:inherit;border:1px solid rgba(255,255,255,.18);border-radius:6px;padding:.25em .45em;font:inherit;color-scheme:dark}',
    '.fd-notes{width:100%;box-sizing:border-box;min-height:6em;margin-top:.9em;padding:.7em .8em;border-radius:8px;border:1px solid rgba(255,255,255,.15);background:rgba(0,0,0,.25);color:inherit;font:inherit;line-height:1.45;resize:vertical}',
    '.fd-foot{display:flex;justify-content:space-between;align-items:center;gap:1em;margin-top:.6em;font-size:.85em;opacity:.75}',
    '.fd-foot a{color:inherit;cursor:pointer;text-decoration:underline}',
    '.fd-title{font-weight:600;margin:0 0 .6em;font-size:1.05em;letter-spacing:.02em}'
  ].join('\n');

  var STAR = '<svg viewBox="0 0 24 24"><path d="M12 2.6l2.9 6 6.6.8-4.9 4.6 1.3 6.5L12 17.3 6.1 20.5l1.3-6.5L2.5 9.4l6.6-.8z" fill="none" stroke="currentColor" stroke-width="1.4" stroke-linejoin="round" opacity=".55"/></svg>' +
             '<svg class="fd-fill" viewBox="0 0 24 24"><path d="M12 2.6l2.9 6 6.6.8-4.9 4.6 1.3 6.5L12 17.3 6.1 20.5l1.3-6.5L2.5 9.4l6.6-.8z" fill="#00c030" stroke="#00c030" stroke-width="1.4" stroke-linejoin="round"/></svg>';
  var HEART = '<svg viewBox="0 0 24 24"><path d="M12 20.5s-7.5-4.6-9.3-9.2C1.4 7.9 3.6 4.5 7 4.5c2 0 3.6 1.1 5 3 1.4-1.9 3-3 5-3 3.4 0 5.6 3.4 4.3 6.8-1.8 4.6-9.3 9.2-9.3 9.2z" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linejoin="round"/></svg>';

  function api() { return window.ApiClient; }
  function url(path) { return api().getUrl('FilmDiary/' + path); }
  function headers(json) {
    var h = { 'Authorization': 'MediaBrowser Token="' + api().accessToken() + '"' };
    if (json) h['Content-Type'] = 'application/json';
    return h;
  }
  function today() {
    var d = new Date();
    return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
  }
  function fmt(r) { return r ? String(r).replace('.', ',') + ' / 5' : 'Sin puntuar'; }

  function currentItemId() {
    var m = /[?&]id=([0-9a-fA-F-]{32,36})/.exec(location.hash || location.href);
    return m ? m[1].replace(/-/g, '') : null;
  }

  function visiblePage() {
    var pages = document.querySelectorAll('.itemDetailPage');
    for (var i = 0; i < pages.length; i++) if (!pages[i].classList.contains('hide')) return pages[i];
    return null;
  }

  function buildPanel(itemId, entry) {
    var el = document.createElement('div');
    el.className = 'fd-panel';
    el.dataset.itemId = itemId;
    el.innerHTML =
      '<div class="fd-title">Mi diario</div>' +
      '<div class="fd-row">' +
      '  <div class="fd-stars" role="slider" aria-label="Puntuación" aria-valuemin="0" aria-valuemax="5" tabindex="0">' +
      [1, 2, 3, 4, 5].map(function () { return '<span class="fd-star">' + STAR + '</span>'; }).join('') +
      '  </div><span class="fd-value"></span>' +
      '  <button class="fd-like" type="button" title="Me gusta" aria-pressed="false">' + HEART + '</button>' +
      '  <label class="fd-field">Vista el <input type="date" class="fd-date"></label>' +
      '  <label class="fd-field"><input type="checkbox" class="fd-rewatch"> Volver a ver</label>' +
      '</div>' +
      '<textarea class="fd-notes" placeholder="Tus notas sobre la película…"></textarea>' +
      '<div class="fd-foot"><span class="fd-status"></span><a class="fd-export">Exportar diario (CSV para Letterboxd)</a></div>';

    var state = {
      Rating: entry.Rating || null,
      Liked: !!entry.Liked,
      WatchedDate: entry.WatchedDate || '',
      Rewatch: !!entry.Rewatch,
      Notes: entry.Notes || ''
    };

    var starsEl = el.querySelector('.fd-stars');
    var stars = el.querySelectorAll('.fd-star');
    var valueEl = el.querySelector('.fd-value');
    var likeEl = el.querySelector('.fd-like');
    var dateEl = el.querySelector('.fd-date');
    var rewatchEl = el.querySelector('.fd-rewatch');
    var notesEl = el.querySelector('.fd-notes');
    var statusEl = el.querySelector('.fd-status');

    function paint(v, preview) {
      for (var i = 0; i < 5; i++) {
        stars[i].classList.toggle('full', v >= i + 1);
        stars[i].classList.toggle('half', v >= i + 0.5 && v < i + 1);
      }
      starsEl.classList.toggle('preview', !!preview);
      valueEl.textContent = fmt(v);
      starsEl.setAttribute('aria-valuenow', v || 0);
    }
    function valueAt(ev) {
      var r = starsEl.getBoundingClientRect();
      var x = Math.min(Math.max((ev.clientX - r.left) / r.width, 0), 0.9999) * 5;
      return Math.floor(x * 2) / 2 + 0.5;
    }

    var timer = null;
    function save(now) {
      clearTimeout(timer);
      statusEl.textContent = 'Guardando…';
      timer = setTimeout(function () {
        fetch(url('Items/' + itemId), { method: 'POST', headers: headers(true), body: JSON.stringify(state) })
          .then(function (r) { if (!r.ok) throw new Error(r.status); return r.json(); })
          .then(function () { statusEl.textContent = 'Guardado'; })
          .catch(function () { statusEl.textContent = 'No se pudo guardar'; });
      }, now ? 0 : 700);
    }

    starsEl.addEventListener('mousemove', function (ev) { paint(valueAt(ev), true); });
    starsEl.addEventListener('mouseleave', function () { paint(state.Rating, false); });
    starsEl.addEventListener('click', function (ev) {
      var v = valueAt(ev);
      state.Rating = state.Rating === v ? null : v;
      if (state.Rating && !state.WatchedDate) { state.WatchedDate = today(); dateEl.value = state.WatchedDate; }
      paint(state.Rating, false);
      save(true);
    });
    starsEl.addEventListener('keydown', function (ev) {
      var v = state.Rating || 0;
      if (ev.key === 'ArrowRight' || ev.key === 'ArrowUp') v = Math.min(5, v + 0.5);
      else if (ev.key === 'ArrowLeft' || ev.key === 'ArrowDown') v = Math.max(0, v - 0.5);
      else return;
      ev.preventDefault();
      state.Rating = v || null;
      paint(state.Rating, false);
      save();
    });
    likeEl.addEventListener('click', function () {
      state.Liked = !state.Liked;
      likeEl.classList.toggle('on', state.Liked);
      likeEl.setAttribute('aria-pressed', state.Liked);
      save(true);
    });
    dateEl.addEventListener('change', function () { state.WatchedDate = dateEl.value; save(true); });
    rewatchEl.addEventListener('change', function () { state.Rewatch = rewatchEl.checked; save(true); });
    notesEl.addEventListener('input', function () { state.Notes = notesEl.value; save(); });
    // Que las teclas de Jellyfin (atajos del reproductor, etc.) no se coman lo que escribes.
    notesEl.addEventListener('keydown', function (ev) { ev.stopPropagation(); });

    el.querySelector('.fd-export').addEventListener('click', function () {
      fetch(url('Export/Letterboxd'), { headers: headers(false) })
        .then(function (r) { if (!r.ok) throw new Error(r.status); return r.blob(); })
        .then(function (b) {
          var a = document.createElement('a');
          a.href = URL.createObjectURL(b);
          a.download = 'film-diary-letterboxd.csv';
          document.body.appendChild(a); a.click(); a.remove();
          setTimeout(function () { URL.revokeObjectURL(a.href); }, 5000);
        })
        .catch(function () { statusEl.textContent = 'No se pudo exportar'; });
    });

    paint(state.Rating, false);
    likeEl.classList.toggle('on', state.Liked);
    dateEl.value = state.WatchedDate;
    rewatchEl.checked = state.Rewatch;
    notesEl.value = state.Notes;
    return el;
  }

  var busy = null;
  function render() {
    var page = visiblePage();
    var id = currentItemId();
    if (!page || !id || !api() || !api().accessToken()) return;
    var existing = page.querySelector('.fd-panel');
    if (existing && existing.dataset.itemId === id) return;
    if (busy === id) return;
    busy = id;
    var userId = api().getCurrentUserId();
    api().getItem(userId, id).then(function (item) {
      if (!item || item.Type !== 'Movie') { if (existing) existing.remove(); busy = null; return; }
      return fetch(url('Items/' + id), { headers: headers(false) })
        .then(function (r) { return r.ok ? r.json() : {}; })
        .then(function (entry) {
          busy = null;
          if (currentItemId() !== id) return;
          var p = visiblePage();
          if (!p) return;
          var old = p.querySelector('.fd-panel');
          if (old) old.remove();
          var anchor = p.querySelector('.overview');
          var host = anchor ? anchor.parentElement : p.querySelector('.detailPagePrimaryContainer');
          if (!host) return;
          var panel = buildPanel(id, entry || {});
          if (anchor) host.insertBefore(panel, anchor); else host.appendChild(panel);
        });
    }).catch(function () { busy = null; });
  }

  function start() {
    var s = document.createElement('style');
    s.textContent = STYLE;
    document.head.appendChild(s);
    var pending = null;
    var schedule = function () { clearTimeout(pending); pending = setTimeout(render, 250); };
    window.addEventListener('hashchange', schedule);
    window.addEventListener('popstate', schedule);
    new MutationObserver(schedule).observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['class'] });
    schedule();
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start); else start();
})();
