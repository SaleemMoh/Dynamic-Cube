// Applies the entity's branding from Apps Control to the sign-in pages: logo, sign-in image and theme colour.
// The application's address comes from the sign-in request (redirect_uri) and is remembered for the following pages;
// its /api is the API host behind the application, whose public branding endpoint allows reading from here.
(function () {
  var script = document.currentScript;
  var internal = !!script && script.src.indexOf('/dynamic-cube-internal/') >= 0;
  var KEY = 'dc.login.appOrigin';
  var HEX = /^#[0-9a-f]{6}$/i;

  function appOrigin() {
    try {
      var redirect = new URLSearchParams(window.location.search).get('redirect_uri');
      if (redirect) {
        var origin = new URL(redirect).origin;
        sessionStorage.setItem(KEY, origin);
        return origin;
      }
      return sessionStorage.getItem(KEY);
    } catch (e) {
      return null;
    }
  }

  function mix(color, amount, other) {
    return 'color-mix(in srgb, ' + color + ' ' + amount + '%, ' + other + ')';
  }

  function applyColor(color) {
    var root = document.documentElement.style;
    if (internal) {
      root.setProperty('--dc-brand', color);
      root.setProperty('--dc-brand-600', mix(color, 85, '#000'));
      root.setProperty('--dc-brand-700', mix(color, 62, '#000'));
      root.setProperty('--dc-brand-50', mix(color, 8, '#fff'));
    } else {
      root.setProperty('--dga-green-700', color);
      root.setProperty('--dga-green-800', mix(color, 85, '#000'));
      root.setProperty('--dga-green-950', mix(color, 55, '#000'));
    }
  }

  function apply(origin, branding) {
    var color = internal ? branding.internalThemeColor : branding.portalThemeColor;
    if (color && HEX.test(color)) applyColor(color);
    if (branding.loginImageUrl) {
      document.documentElement.style.setProperty('--dc-login-image', 'url("' + origin + branding.loginImageUrl + '")');
    }
    var header = document.getElementById('kc-header-wrapper') || document.getElementById('kc-header');
    if (branding.logoUrl && header && !document.querySelector('.dc-login-logo')) {
      var logo = document.createElement('img');
      logo.className = 'dc-login-logo';
      logo.src = origin + branding.logoUrl;
      var lang = (document.documentElement.lang || 'ar').slice(0, 2);
      logo.alt = (lang === 'en' ? branding.nameEn : branding.nameAr) || '';
      header.insertBefore(logo, header.firstChild);
    }
  }

  document.addEventListener('DOMContentLoaded', function () {
    var origin = appOrigin();
    if (!origin || !window.fetch) return;
    fetch(origin + '/api/apps-control/v1/branding', { credentials: 'omit' })
      .then(function (response) {
        return response.ok ? response.json() : null;
      })
      .then(function (branding) {
        if (branding) apply(origin, branding);
      })
      .catch(function () {
        // Without branding the pages keep their built-in look.
      });
  });
})();
