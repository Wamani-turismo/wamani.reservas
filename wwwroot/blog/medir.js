/* ═══════════════════════════════════════════════════════════════════
   Medición del blog de Wamani.

   Un solo archivo para todas las notas, igual que blog.css: cada nota
   lo linkea con una línea en el <head> y hereda todo. Si alguna vez hay
   que cambiar el ID de Analytics o agregar un evento, se toca ACÁ y no
   en cada nota, que es como se nos escapó la medición las primeras
   semanas del blog (las notas no tenían ningún tag).

   Manda a la MISMA propiedad de GA4 que la web grande, para que el blog
   no quede como un sitio aparte y se pueda ver el recorrido completo:
   entró por una nota → después miró una excursión → consultó.
   ═══════════════════════════════════════════════════════════════════ */
(function () {
  var ID = 'G-X04DXRSG6H';

  // 1) El stub de gtag ANTES de cargar la librería. Las llamadas se
  //    encolan en dataLayer y la librería las procesa cuando llega.
  window.dataLayer = window.dataLayer || [];
  window.gtag = window.gtag || function () { window.dataLayer.push(arguments); };
  gtag('js', new Date());
  gtag('config', ID);

  var s = document.createElement('script');
  s.async = true;
  s.src = 'https://www.googletagmanager.com/gtag/js?id=' + ID;
  document.head.appendChild(s);

  // 2) Qué página es. /blog/puna-de-jujuy/ → "puna-de-jujuy"; el índice
  //    queda como "indice".
  var partes = location.pathname.split('/').filter(Boolean); // ["blog","puna-de-jujuy"]
  var nota = partes.length > 1 ? partes[partes.length - 1] : 'indice';

  // 3) Un solo listener en el documento, en lugar de uno por enlace: así
  //    funciona igual si una nota futura agrega enlaces nuevos.
  document.addEventListener('click', function (ev) {
    var a = ev.target && ev.target.closest ? ev.target.closest('a[href]') : null;
    if (!a) return;
    var href = a.getAttribute('href') || '';

    try {
      // 3.a) De una nota a una ficha de excursión. ESTE es el número que
      //      importa: cuánta gente que vino a leer terminó mirando algo
      //      que vendemos. "lugar" distingue los botones del recuadro
      //      final de los enlaces sueltos en el medio del texto.
      if (href.indexOf('/excursiones/') === 0) {
        gtag('event', 'clic_desde_blog', {
          nota: nota,
          excursion: href.split('/excursiones/')[1] || '(sin nombre)',
          lugar: a.closest('.cierre') ? 'cierre' : 'texto'
        });
        return;
      }

      // 3.b) Del índice (o del pie de una nota) a otra nota. Sirve para
      //      saber qué títulos funcionan y si alguien lee una segunda.
      if (href.indexOf('/blog/') === 0 && href !== '/blog/') {
        gtag('event', 'clic_nota', {
          nota: nota,
          destino: href.replace(/\/$/, '').split('/').pop()
        });
        return;
      }

      // 3.c) Del blog a la web grande (el logo y el "Ir al sitio").
      if (href === '/') {
        gtag('event', 'clic_desde_blog', {
          nota: nota, excursion: '(home)', lugar: 'navegacion'
        });
      }
    } catch (e) { /* medir nunca debe romper la página */ }
  }, true);
})();
