/* ═══════════════════════════════════════════════════════════════════════════
   WAMANI · Inglés y francés para la web pública
   ───────────────────────────────────────────────────────────────────────────
   CÓMO FUNCIONA (importante para el que venga después)

   La web mezcla dos cosas: texto escrito en el HTML y texto que llega de la
   base de datos (excursiones, testimonios, equipo). Por eso NO se traduce
   marcando etiquetas una por una, sino buscando el texto en castellano en un
   diccionario. Ventajas:
     · Sirve igual para el HTML y para lo que inyecta el sistema.
     · Si alguien edita un texto desde el panel y no está traducido, se muestra
       en castellano. NUNCA queda vacío ni roto.
     · No hubo que tocar el HTML de la web salvo para agregar el selector.

   El castellano es el original: al volver a ES se restaura lo guardado.

   Las traducciones están en idiomas-textos.js (se carga ANTES que este archivo).
   ═══════════════════════════════════════════════════════════════════════════ */
(function () {
  "use strict";

  var IDIOMAS = window.WAMANI_IDIOMAS || {};
  var actual = "es";
  var trabajando = false;      // evita que el observador se dispare a sí mismo

  // Espacios, saltos de línea y tabs se aplastan a UN espacio antes de buscar.
  // Así un texto de la base con saltos de línea encuentra igual su traducción.
  function norm(s) { return (s || "").replace(/\s+/g, " ").trim(); }

  // Separa un símbolo/emoji de adelante: "🗓️ 3 días", "● Traslado ida y vuelta"
  var RE_PREFIJO = /^([^0-9A-Za-zÀ-ÿ]+)(.+)$/;

  function directo(t, dic) {
    return dic.textos[t] || dic._bajo[t.toLowerCase()] || null;
  }

  function conPrefijo(t, dic) {
    var m = t.match(RE_PREFIJO);
    if (!m) return null;
    var r = directo(m[2], dic);
    return r ? m[1] + r : null;
  }

  // ── Buscar una traducción, con tres intentos ────────────────────────────
  function buscar(txt, dic) {
    var t = norm(txt);
    if (!t) return null;

    // 1) tal cual (sin distinguir mayúsculas: la base tiene "Seguro de viaje"
    //    y "seguro de viaje" mezclados)
    var r = directo(t, dic);
    if (r) return r;

    // 2) con un símbolo adelante: se traduce lo de atrás y se le vuelve a pegar
    r = conPrefijo(t, dic);
    if (r) return r;

    // 2b) entre comillas (los testimonios se dibujan así: “texto”).
    //     Se sacan SOLO las comillas, no cualquier símbolo, para no comerse
    //     el punto o el signo de admiración del final.
    var c = t.match(/^([“"«])([\s\S]+)([”"»])$/);
    if (c) {
      r = directo(c[2].trim(), dic);
      if (r) return c[1] + r + c[3];
    }

    // 3) varios datos pegados con " · " (el panel del mapa los junta así).
    //    Se traduce pedazo por pedazo. Ojo: un dato puede TENER un " · " adentro
    //    ("Salida 9:30 · San Salvador de Jujuy"), así que al cortar quedan
    //    pedazos sueltos; por eso el diccionario también los tiene por separado.
    //    Si alguno no se encuentra se deja ESE en castellano y se traduce el
    //    resto: media línea traducida es mejor que ninguna.
    if (t.indexOf(" · ") > 0) {
      var partes = t.split(" · "), listo = [], alguno = false;
      for (var i = 0; i < partes.length; i++) {
        var sub = directo(partes[i], dic) || conPrefijo(partes[i], dic);
        if (sub) alguno = true; else sub = partes[i];
        listo.push(sub);
      }
      return alguno ? listo.join(" · ") : null;
    }
    return null;
  }

  // ── Elementos que se traducen ENTEROS ───────────────────────────────────
  // Los que llevan <b> o <em> adentro: el orden de las palabras cambia según
  // el idioma, así que traducir pedacito por pedacito daría un desastre.
  var CON_BLOQUE = "h1,h2,h3,h4,p,li,span,small,label,button,div,dd";

  function traducirBloques(raiz, dic) {
    if (!dic.bloques) return;
    var els = raiz.querySelectorAll ? raiz.querySelectorAll(CON_BLOQUE) : [];
    for (var i = 0; i < els.length; i++) {
      var el = els[i];
      if (el.getAttribute("data-w-hecho") === "1") continue;
      var clave = norm(el.textContent);
      if (!clave || !dic.bloques[clave]) continue;

      // GANA EL MÁS CHICO. Un <div> que envuelve al <p> tiene el MISMO texto, así
      // que los dos "coinciden"; si reemplazáramos el de afuera nos llevaríamos
      // puesto lo que hay al lado (una imagen, por ejemplo). Por eso, si adentro
      // hay otro elemento que también coincide, se deja que lo haga él.
      var dentro = el.querySelectorAll(CON_BLOQUE), hayOtro = false;
      for (var j = 0; j < dentro.length; j++) {
        if (dic.bloques[norm(dentro[j].textContent)]) { hayOtro = true; break; }
      }
      if (hayOtro) continue;

      if (el.getAttribute("data-w-es") === null) el.setAttribute("data-w-es", el.innerHTML);
      el.innerHTML = dic.bloques[clave];
      el.setAttribute("data-w-hecho", "1");   // ya traducido: no volver a entrar
    }
  }

  function restaurarBloques(raiz) {
    var els = raiz.querySelectorAll ? raiz.querySelectorAll("[data-w-es]") : [];
    for (var i = 0; i < els.length; i++) {
      els[i].innerHTML = els[i].getAttribute("data-w-es");
      els[i].removeAttribute("data-w-hecho");
    }
  }

  // ── Texto suelto ────────────────────────────────────────────────────────
  var SALTEAR = { SCRIPT: 1, STYLE: 1, NOSCRIPT: 1, TEXTAREA: 1 };

  function recorrerTextos(raiz, fn) {
    if (!raiz || !raiz.nodeType) return;
    var it = document.createTreeWalker(raiz, NodeFilter.SHOW_TEXT, {
      acceptNode: function (n) {
        if (!n.nodeValue || !n.nodeValue.trim()) return NodeFilter.FILTER_REJECT;
        var p = n.parentNode;
        if (!p || SALTEAR[p.nodeName]) return NodeFilter.FILTER_REJECT;
        // adentro de un bloque ya traducido no se toca nada
        if (p.closest && p.closest('[data-w-hecho="1"]')) return NodeFilter.FILTER_REJECT;
        return NodeFilter.FILTER_ACCEPT;
      }
    });
    var n, lista = [];
    while ((n = it.nextNode())) lista.push(n);
    for (var i = 0; i < lista.length; i++) fn(lista[i]);
  }

  // ── Atributos que también se ven ────────────────────────────────────────
  var ATRIBUTOS = ["placeholder", "title", "alt", "aria-label"];

  function traducirAtributos(raiz, dic) {
    var els = raiz.querySelectorAll ? raiz.querySelectorAll("*") : [];
    for (var i = 0; i < els.length; i++) {
      var el = els[i];
      for (var a = 0; a < ATRIBUTOS.length; a++) {
        var nom = ATRIBUTOS[a];
        if (!el.hasAttribute(nom)) continue;
        var marca = "data-w-" + nom;
        var orig = el.hasAttribute(marca) ? el.getAttribute(marca) : el.getAttribute(nom);
        if (dic) {
          var t = buscar(orig, dic);
          if (t) {
            if (!el.hasAttribute(marca)) el.setAttribute(marca, orig);
            el.setAttribute(nom, t);
          }
        } else if (el.hasAttribute(marca)) {
          el.setAttribute(nom, el.getAttribute(marca));
          el.removeAttribute(marca);
        }
      }
    }
  }

  // ── Aplicar un idioma a un pedazo de la página ──────────────────────────
  function aplicar(raiz, dic) {
    traducirBloques(raiz, dic);
    recorrerTextos(raiz, function (n) {
      if (n.__wEs === undefined) n.__wEs = n.nodeValue;
      var t = buscar(n.__wEs, dic);
      if (t === null) return;
      // se respetan los espacios de los costados para no pegar palabras
      var izq = n.__wEs.match(/^\s*/)[0], der = n.__wEs.match(/\s*$/)[0];
      n.nodeValue = izq + t + der;
    });
    traducirAtributos(raiz, dic);
  }

  function restaurar(raiz) {
    restaurarBloques(raiz);
    recorrerTextos(raiz, function (n) {
      if (n.__wEs !== undefined) n.nodeValue = n.__wEs;
    });
    traducirAtributos(raiz, null);
  }

  // ── Cambiar de idioma ───────────────────────────────────────────────────
  function cambiar(lang, guardar) {
    if (lang !== "es" && !IDIOMAS[lang]) lang = "es";
    actual = lang;
    trabajando = true;
    try {
      restaurar(document.body);              // siempre se parte del castellano

      // El título de la pestaña y la descripción para Google. Se traducen por el
      // MISMO diccionario que el resto, así el archivo sirve igual para la web y
      // para /receptivo, que tienen títulos distintos.
      var meta = document.querySelector('meta[name="description"]');
      if (window.__wTituloEs === undefined) window.__wTituloEs = document.title;
      if (meta && window.__wDescEs === undefined) window.__wDescEs = meta.getAttribute("content");

      if (lang === "es") {
        document.documentElement.lang = "es-AR";
        document.title = window.__wTituloEs;
        if (meta) meta.setAttribute("content", window.__wDescEs);
      } else {
        var dic = IDIOMAS[lang];
        aplicar(document.body, dic);
        document.documentElement.lang = lang;
        var tt = buscar(window.__wTituloEs, dic);
        if (tt) document.title = tt;
        if (meta) {
          var td = buscar(window.__wDescEs, dic);
          if (td) meta.setAttribute("content", td);
        }
      }
    } catch (e) { /* nunca romper la página por una traducción */ }
    trabajando = false;

    // marcar el botón activo
    var bs = document.querySelectorAll(".sel-idioma button");
    for (var i = 0; i < bs.length; i++)
      bs[i].classList.toggle("on", bs[i].getAttribute("data-lang") === lang);

    if (guardar !== false) {
      try { localStorage.setItem("wamani_idioma", lang); } catch (e) {}
      try {
        var u = new URL(window.location.href);
        if (lang === "es") u.searchParams.delete("lang");
        else u.searchParams.set("lang", lang);
        history.replaceState(null, "", u.pathname + u.search + u.hash);
      } catch (e) {}
    }
  }

  // ── El sistema y el carrusel inyectan HTML DESPUÉS de cargar la página ──
  // (tarjetas, guía de la excursión, panel del mapa, testimonios, equipo).
  // El observador traduce eso mismo apenas aparece.
  function observar() {
    if (!window.MutationObserver) return;
    var obs = new MutationObserver(function (muts) {
      if (trabajando || actual === "es") return;
      var dic = IDIOMAS[actual];
      if (!dic) return;
      trabajando = true;
      try {
        for (var i = 0; i < muts.length; i++) {
          var m = muts[i];
          for (var j = 0; j < m.addedNodes.length; j++) {
            var n = m.addedNodes[j];
            if (n.nodeType === 1) aplicar(n, dic);
            else if (n.nodeType === 3) {
              if (n.__wEs === undefined) n.__wEs = n.nodeValue;
              var t = buscar(n.__wEs, dic);
              if (t !== null) n.nodeValue = t;
            }
          }
        }
      } catch (e) {}
      trabajando = false;
    });
    obs.observe(document.body, { childList: true, subtree: true });
  }

  // ── Índice en minúsculas (se arma una vez) ──────────────────────────────
  function prepararDiccionarios() {
    for (var k in IDIOMAS) {
      var d = IDIOMAS[k];
      d.textos = d.textos || {};
      d._bajo = {};
      for (var t in d.textos) d._bajo[t.toLowerCase()] = d.textos[t];
    }
  }

  // ── Arranque ────────────────────────────────────────────────────────────
  function arrancar() {
    prepararDiccionarios();
    observar();

    var pedido = null;
    try {
      var qs = new URLSearchParams(window.location.search).get("lang");
      if (qs) pedido = qs.toLowerCase();
      if (!pedido) pedido = localStorage.getItem("wamani_idioma");
    } catch (e) {}

    // Sin elección previa: si el navegador está en inglés o francés, se abre así.
    if (!pedido) {
      var nav = (navigator.language || "").toLowerCase();
      if (nav.indexOf("en") === 0) pedido = "en";
      else if (nav.indexOf("fr") === 0) pedido = "fr";
    }
    cambiar(pedido && IDIOMAS[pedido] ? pedido : "es", false);
  }

  window.WamaniIdioma = {
    cambiar: function (l) { cambiar(l, true); },
    actual: function () { return actual; }
  };

  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", arrancar);
  else arrancar();
})();
