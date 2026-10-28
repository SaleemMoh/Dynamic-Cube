// Adds the DGA government identification bar (Saudi flag + official-website statement) above the login page.
(function () {
  var script = document.currentScript;
  var flag = script ? script.src.replace(/js\/gov-bar\.js.*$/, 'img/sa-flag.svg') : '';
  var texts = {
    ar: 'موقع حكومي رسمي تابع لحكومة المملكة العربية السعودية',
    en: 'An official website of the Government of Saudi Arabia',
  };

  document.addEventListener('DOMContentLoaded', function () {
    var lang = (document.documentElement.lang || 'ar').slice(0, 2);
    var bar = document.createElement('div');
    bar.className = 'dc-gov-bar';
    var img = document.createElement('img');
    img.src = flag;
    img.width = 24;
    img.height = 16;
    img.alt = '';
    var text = document.createElement('span');
    text.textContent = texts[lang] || texts.ar;
    bar.appendChild(img);
    bar.appendChild(text);
    document.body.insertBefore(bar, document.body.firstChild);
  });
})();
