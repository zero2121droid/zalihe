(function () {
  var root = document.documentElement;
  var pinned = root.classList.contains('pinned');

  /* Theme */
  var toggle = document.getElementById('theme-toggle');
  function labelToggle() {
    toggle.setAttribute('aria-label', root.dataset.theme === 'light' ? 'Uključi tamnu temu' : 'Uključi svetlu temu');
  }
  labelToggle();
  toggle.addEventListener('click', function () {
    var next = root.dataset.theme === 'light' ? 'dark' : 'light';
    root.dataset.theme = next;
    labelToggle();
    try { localStorage.setItem('lagerko-theme', next); } catch (e) {}
  });

  if (!pinned) return;

  /* Header logo appears once the large one has left the screen */
  var top = document.querySelector('.top');
  var plays = document.querySelectorAll('[data-play]');
  if ('IntersectionObserver' in window) {
    new IntersectionObserver(function (entries) {
      top.classList.toggle('brand-away', !entries[0].isIntersecting);
    }, { rootMargin: '-' + top.offsetHeight + 'px 0px 0px' }).observe(document.getElementById('hero-brand'));

    /* Drawings play once, when they reach the screen */
    var playOnce = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (!entry.isIntersecting) return;
        entry.target.classList.add('play');
        playOnce.unobserve(entry.target);
      });
    }, { threshold: .4 });
    plays.forEach(function (el) { playOnce.observe(el); });
  } else {
    top.classList.add('brand-away');
    plays.forEach(function (el) { el.classList.add('play'); });
  }

  /* Hero: one order comes in, once. The HTML already holds the end state. */
  var hero = document.getElementById('hero-demo');
  var num = hero.querySelector('.num');
  var delta = hero.querySelector('.delta');
  var sync = hero.querySelector('.sync');
  var syncText = hero.querySelector('.sync-text');
  var move = hero.querySelector('.move-new');
  var syncDone = syncText.textContent;
  var start = 31, end = 29;

  num.textContent = start;
  move.classList.add('pending');

  function countDown() {
    var t0 = performance.now();
    (function tick(now) {
      var t = Math.min((now - t0) / 500, 1);
      num.textContent = Math.round(start + (end - start) * (1 - Math.pow(1 - t, 3)));
      if (t < 1) requestAnimationFrame(tick);
    })(t0);
  }

  setTimeout(function () {
    delta.textContent = '−2';
    delta.classList.add('pop');
    countDown();
    move.classList.remove('pending');
    sync.classList.add('sending');
    syncText.textContent = 'Šalje se u prodavnicu';
    setTimeout(function () {
      sync.classList.remove('sending');
      syncText.textContent = syncDone;
    }, 1200);
  }, 1200);

  /* Excel scene: scroll position picks the step */
  var scene = document.getElementById('od-excela');
  var win = scene.querySelector('.win');
  var sheet = scene.querySelector('.sheet');
  var sheetName = scene.querySelector('.sheet-name');
  var sheetNote = scene.querySelector('.sheet-note');
  var heads = scene.querySelectorAll('.row.head .cell');
  var appLabels = Array.prototype.map.call(heads, function (c) { return c.textContent; });
  var appName = sheetName.textContent;
  var appNote = sheetNote.textContent;
  var excelLabels = ['A', 'B', 'C', 'D'];

  var rows = Array.prototype.map.call(scene.querySelectorAll('.row[data-raw]'), function (el) {
    var cells = el.querySelectorAll('.cell');
    return {
      el: el,
      cells: cells,
      raw: el.dataset.raw,
      rawq: el.dataset.rawq,
      dup: 'dup' in el.dataset,
      flag: 'flag' in el.dataset,
      name: cells[0].textContent,
      sku: cells[1].textContent,
      q: cells[2].textContent,
      status: cells[3].innerHTML
    };
  });

  function applySheet(i) {
    var isApp = i >= 2;
    win.classList.toggle('app', isApp);
    sheet.classList.toggle('flagging', i === 1);
    sheetName.textContent = isApp ? appName : 'lager_FINAL_novo(2).xlsx';
    sheetNote.textContent = isApp ? appNote : 'izmenjeno pre 3 nedelje';
    heads.forEach(function (c, n) {
      c.textContent = isApp ? (n === 3 && i < 4 ? '' : appLabels[n]) : excelLabels[n];
    });
    rows.forEach(function (r) {
      r.el.classList.toggle('gone', isApp && r.dup);
      r.el.classList.toggle('flag', r.flag && !isApp);
      r.cells[0].textContent = isApp && r.name ? r.name : r.raw;
      r.cells[1].textContent = isApp ? r.sku : '';
      r.cells[2].textContent = i >= 3 && r.q ? r.q : r.rawq;
      r.cells[3].innerHTML = i >= 4 ? r.status : '';
    });
  }

  /* Turn the static step list into one current step plus a rail */
  var list = scene.querySelector('.steps');
  var steps = Array.prototype.map.call(list.children, function (li) {
    return { title: li.querySelector('h3').textContent, text: li.querySelector('p').textContent, label: li.dataset.label };
  });
  var now = scene.querySelector('.copy-now');
  var stepCount = now.querySelector('.step-count');
  var stepTitle = now.querySelector('.step-title');
  var stepText = now.querySelector('.step-text');

  var rail = document.createElement('ol');
  rail.className = 'rail';
  rail.setAttribute('aria-label', 'Koraci');
  steps.forEach(function (s, i) {
    var li = document.createElement('li');
    var b = document.createElement('button');
    b.type = 'button';
    b.setAttribute('aria-label', 'Korak ' + (i + 1) + ': ' + s.title);
    b.innerHTML = '<span></span><em></em>';
    b.querySelector('em').textContent = s.label;
    b.addEventListener('click', function () { jumpTo(i); });
    li.appendChild(b);
    rail.appendChild(li);
  });
  list.replaceWith(rail);
  now.hidden = false;
  var railButtons = rail.querySelectorAll('button');

  function headHeight() {
    return document.querySelector('.top').offsetHeight;
  }

  function range() {
    return scene.offsetHeight - scene.querySelector('.pin').offsetHeight;
  }

  function jumpTo(i) {
    var top = scene.getBoundingClientRect().top + window.scrollY - headHeight();
    window.scrollTo({ top: top + range() * (i + .5) / steps.length, behavior: 'smooth' });
  }

  var current = -1;
  function go(i) {
    var s = steps[i];
    current = i;
    stepCount.textContent = 'Korak ' + (i + 1) + ' od ' + steps.length;
    stepTitle.textContent = s.title;
    stepText.textContent = s.text;
    now.classList.remove('swap');
    void now.offsetWidth;
    now.classList.add('swap');
    railButtons.forEach(function (b, n) {
      b.classList.toggle('done', n < i);
      if (n === i) b.setAttribute('aria-current', 'step'); else b.removeAttribute('aria-current');
    });
    applySheet(i);
  }

  function onScroll() {
    var travelled = headHeight() - scene.getBoundingClientRect().top;
    var p = Math.max(0, Math.min(.9999, travelled / range()));
    var i = Math.floor(p * steps.length);
    if (i !== current) go(i);
  }

  window.addEventListener('scroll', onScroll, { passive: true });
  window.addEventListener('resize', onScroll);
  onScroll();
})();
