// Videos: play muted when >= 60 % visible, pause otherwise. Never under prefers-reduced-motion (native controls still work).
// Carousels: mark the dot of the slide in view. Everything works without this file (scroll-snap, :target lightbox, controls).
(function () {
  if (!('IntersectionObserver' in window)) return;
  var reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;
  function init() {
    if (!reduce) {
      var io = new IntersectionObserver(function (entries) {
        entries.forEach(function (e) {
          var v = e.target;
          if (e.intersectionRatio >= 0.6) { var p = v.play(); if (p && p.catch) p.catch(function () {}); } else v.pause();
        });
      }, { threshold: [0, 0.6] });
      document.querySelectorAll('video[data-media-video]:not([data-bound])').forEach(function (v) { v.setAttribute('data-bound', ''); io.observe(v); });
    }
    document.querySelectorAll('[data-media-carousel]:not([data-bound])').forEach(function (ul) {
      ul.setAttribute('data-bound', '');
      var dots = ul.parentNode.querySelectorAll('[data-media-dot]');
      var so = new IntersectionObserver(function (entries) {
        entries.forEach(function (e) {
          if (!e.isIntersecting) return;
          dots.forEach(function (d) {
            if (d.getAttribute('href') === '#' + e.target.id) d.setAttribute('aria-current', 'true'); else d.removeAttribute('aria-current');
          });
        });
      }, { root: ul, threshold: 0.6 });
      ul.querySelectorAll('[data-media-slide]').forEach(function (s) { so.observe(s); });
    });
  }
  init();
  if (window.Blazor && Blazor.addEventListener) Blazor.addEventListener('enhancedload', init);
  else document.addEventListener('DOMContentLoaded', function () { if (window.Blazor && Blazor.addEventListener) Blazor.addEventListener('enhancedload', init); });
})();
